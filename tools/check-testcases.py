#!/usr/bin/env python3
"""
check-testcases.py — static validation of ConfIT test definition files.

Catches, without a build or a test run, the errors ConfIT raises at load time or on the
first failing assertion:

  * a response with no expected body (the structural diff always reports a difference)
  * depends: naming an unknown test, a later test, or a test in another file
  * {{variables}} that are never extracted, extracted later, or ambiguous
  * graphql blocks with neither query nor queryFromFile
  * bodyFromFile / queryFromFile pointing at a file that does not exist
  * mock: blocks in an integration suite, or with no mock server configured
  * test files and fixtures missing their <None Update> csproj registration
  * untagged tests in a suite that filters by tag

Usage:
    python3 check-testcases.py [TEST_PROJECT_DIR]      # defaults to the current directory

Exit code 1 when any ERROR is reported. PyYAML is used when installed; otherwise a bundled
subset parser handles the YAML dialect the ConfIT DSL uses.
"""

import fnmatch
import json
import os
import re
import sys

# --------------------------------------------------------------------------- YAML subset


class YamlError(Exception):
    pass


def _strip_comment(line):
    out, quote = [], None
    for i, ch in enumerate(line):
        if quote:
            out.append(ch)
            if ch == quote and line[i - 1: i] != "\\":
                quote = None
        elif ch in "\"'":
            quote = ch
            out.append(ch)
        elif ch == "#" and (i == 0 or line[i - 1] in " \t"):
            break
        else:
            out.append(ch)
    return "".join(out).rstrip()


def _scalar(text):
    text = text.strip()
    if len(text) >= 2 and text[0] == text[-1] and text[0] in "\"'":
        return text[1:-1]
    if text in ("", "~", "null", "Null", "NULL"):
        return None
    if text in ("true", "True", "TRUE"):
        return True
    if text in ("false", "False", "FALSE"):
        return False
    try:
        return int(text)
    except ValueError:
        pass
    try:
        return float(text)
    except ValueError:
        pass
    return text


def _split_key(text):
    """Split 'key: value' at the first structural colon. Returns (key, rest) or None."""
    quote, depth = None, 0
    for i, ch in enumerate(text):
        if quote:
            if ch == quote:
                quote = None
        elif ch in "\"'":
            quote = ch
        elif ch in "{[":
            depth += 1
        elif ch in "}]":
            depth -= 1
        elif ch == ":" and depth == 0 and (i + 1 == len(text) or text[i + 1] in " \t"):
            key = text[:i].strip()
            if len(key) >= 2 and key[0] == key[-1] and key[0] in "\"'":
                key = key[1:-1]
            return key, text[i + 1:].strip()
    return None


def _parse_flow(text, pos=0, top=False):
    """Parse a single-line flow collection or scalar. Returns (value, next_pos).

    `top` marks a value that is not inside a flow collection, where a plain scalar runs to end of
    line — `hasLength(1, 100)` must not be truncated at the comma.
    """
    while pos < len(text) and text[pos] in " \t":
        pos += 1
    if pos >= len(text):
        return None, pos
    if text[pos] == "{":
        out, pos = {}, pos + 1
        while True:
            while pos < len(text) and text[pos] in " \t,":
                pos += 1
            if pos < len(text) and text[pos] == "}":
                return out, pos + 1
            if pos >= len(text):
                raise YamlError("unterminated flow mapping")
            key, pos = _parse_flow_scalar(text, pos, stop=":")
            while pos < len(text) and text[pos] in " \t:":
                pos += 1
            val, pos = _parse_flow(text, pos)
            out[str(key)] = val
    if text[pos] == "[":
        out, pos = [], pos + 1
        while True:
            while pos < len(text) and text[pos] in " \t,":
                pos += 1
            if pos < len(text) and text[pos] == "]":
                return out, pos + 1
            if pos >= len(text):
                raise YamlError("unterminated flow sequence")
            val, pos = _parse_flow(text, pos)
            out.append(val)
    return _parse_flow_scalar(text, pos, stop="" if top else ",}]")


def _parse_flow_scalar(text, pos, stop):
    if text[pos] in "\"'":
        quote, pos, buf = text[pos], pos + 1, []
        while pos < len(text) and text[pos] != quote:
            buf.append(text[pos])
            pos += 1
        return "".join(buf), pos + 1
    start = pos
    while pos < len(text) and text[pos] not in stop:
        pos += 1
    return _scalar(text[start:pos]), pos


class _Block:
    """Indentation-driven parser for the block YAML the ConfIT DSL uses."""

    def __init__(self, text):
        self.lines = []
        for n, raw in enumerate(text.splitlines(), start=1):
            body = _strip_comment(raw)
            if not body.strip():
                continue
            self.lines.append({"n": n, "indent": len(body) - len(body.lstrip()), "text": body.strip()})
        self.anchors = {}
        self.lineno = {}

    def parse(self):
        if not self.lines:
            return {}
        value, idx = self.block(0, self.lines[0]["indent"])
        if idx != len(self.lines):
            raise YamlError("line %d: unexpected indentation" % self.lines[idx]["n"])
        return value

    def block(self, idx, indent):
        if self.lines[idx]["text"] == "-" or self.lines[idx]["text"].startswith("- "):
            return self.sequence(idx, indent)
        return self.mapping(idx, indent)

    def mapping(self, idx, indent):
        out = {}
        while idx < len(self.lines) and self.lines[idx]["indent"] == indent:
            line = self.lines[idx]
            if line["text"].startswith("- "):
                break
            split = _split_key(line["text"])
            if split is None:
                raise YamlError("line %d: expected 'key: value'" % line["n"])
            key, rest = split
            value, idx = self.value(rest, idx + 1, indent, line["n"])
            out[key] = value
            self.lineno.setdefault(id(out), {})[key] = line["n"]
        return out, idx

    def sequence(self, idx, indent):
        out = []
        while idx < len(self.lines) and self.lines[idx]["indent"] == indent:
            line = self.lines[idx]
            if not (line["text"] == "-" or line["text"].startswith("- ")):
                break
            rest = line["text"][2:].strip() if line["text"] != "-" else ""
            if not rest:
                idx += 1
                if idx < len(self.lines) and self.lines[idx]["indent"] > indent:
                    value, idx = self.block(idx, self.lines[idx]["indent"])
                else:
                    value = None
                out.append(value)
                continue
            # Item content sits on the dash line: re-enter with a virtual line at indent + 2.
            self.lines[idx] = {"n": line["n"], "indent": indent + 2, "text": rest}
            if _split_key(rest) is not None:
                value, idx = self.mapping(idx, indent + 2)
            else:
                value, idx = self.value(rest, idx + 1, indent + 2, line["n"])
            out.append(value)
        return out, idx

    def value(self, rest, idx, indent, lineno):
        anchor = None
        match = re.match(r"^&(\S+)\s*(.*)$", rest)
        if match:
            anchor, rest = match.group(1), match.group(2).strip()
        alias = re.match(r"^\*(\S+)$", rest)
        if alias:
            if alias.group(1) not in self.anchors:
                raise YamlError("line %d: unknown alias '*%s'" % (lineno, alias.group(1)))
            return json.loads(json.dumps(self.anchors[alias.group(1)])), idx

        if rest in ("|", "|-", "|+", ">", ">-", ">+"):
            buf, fold = [], rest[0] == ">"
            base = None
            while idx < len(self.lines) and self.lines[idx]["indent"] > indent:
                if base is None:
                    base = self.lines[idx]["indent"]
                buf.append(" " * max(0, self.lines[idx]["indent"] - base) + self.lines[idx]["text"])
                idx += 1
            value = (" " if fold else "\n").join(buf)
            if rest.endswith("-"):
                value = value.rstrip("\n")
            else:
                value += "" if fold else "\n"
        elif rest == "":
            if idx < len(self.lines) and self.lines[idx]["indent"] > indent:
                value, idx = self.block(idx, self.lines[idx]["indent"])
            else:
                value = None
        else:
            value, _ = _parse_flow(rest, top=True)

        if anchor:
            self.anchors[anchor] = value
        return value, idx


def load_yaml(text):
    try:
        import yaml  # noqa: F401
        return yaml.safe_load(text)
    except ImportError:
        return _Block(text).parse()


def load_file(path):
    text = open(path, encoding="utf-8-sig").read()
    if path.lower().endswith(".json"):
        return json.loads(text)
    return load_yaml(text)


# ------------------------------------------------------------------------------- report

RESET, RED, YELLOW, DIM = "\033[0m", "\033[31m", "\033[33m", "\033[2m"


class Report:
    def __init__(self):
        self.items = []

    def error(self, path, test, message):
        self.items.append(("ERROR", path, test, message))

    def warn(self, path, test, message):
        self.items.append(("WARN", path, test, message))

    def print(self, root):
        colour = sys.stdout.isatty()
        by_path = {}
        for level, path, test, message in self.items:
            by_path.setdefault(path, []).append((level, test, message))
        for path in sorted(by_path):
            print("\n" + os.path.relpath(path, root))
            for level, test, message in by_path[path]:
                tag = level.ljust(5)
                if colour:
                    tag = (RED if level == "ERROR" else YELLOW) + tag + RESET
                where = (DIM + test + RESET) if colour and test else test
                print("  %s  %s%s%s" % (tag, where, "  " if test else "", message))
        errors = sum(1 for i in self.items if i[0] == "ERROR")
        warns = len(self.items) - errors
        print("\n%d error(s), %d warning(s)" % (errors, warns))
        return errors


# -------------------------------------------------------------------------- suite config


def find_suite_config(root):
    for base, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in ("bin", "obj", ".git")]
        if "suite.config.yaml" in files:
            return os.path.join(base, "suite.config.yaml")
    return None


def read_suite(root):
    """Returns (kind, mock_url, folders, filter_strategy, config_path)."""
    path = find_suite_config(root)
    if not path:
        return None, None, {}, None, None
    try:
        cfg = load_yaml(open(path, encoding="utf-8-sig").read()) or {}
    except Exception as exc:                                          # noqa: BLE001
        print("could not parse %s: %s" % (path, exc), file=sys.stderr)
        return None, None, {}, None, path
    if "component" in cfg:
        section = cfg["component"] or {}
        kind = "component"
    elif "integration" in cfg:
        integration = cfg["integration"] or {}
        env = os.environ.get("TEST_ENVIRONMENT") or integration.get("default")
        section = (integration.get(env) or {}) if env else {}
        kind = "integration"
    else:
        return None, None, {}, None, path
    mock = (section.get("mock") or {}).get("url")
    folders = section.get("folders") or {}
    strategy = (section.get("filter") or {}).get("strategy")
    return kind, mock, folders, strategy, path


# ---------------------------------------------------------------------------- discovery


def is_test_file(data):
    return (
        isinstance(data, dict)
        and len(data) > 0
        and all(isinstance(v, dict) and "api" in v for v in data.values())
    )


def discover(root, report):
    """Returns [(path, ordered [(name, case)])] sorted by folder then filename.

    Groups candidates by directory first: a directory with at least one file shaped like tests
    is a test directory, and any dict-shaped sibling there that is NOT correctly shaped gets
    reported instead of silently vanishing — ConfIT's own TestReader loads every file in a wired
    folder unconditionally, so a malformed sibling fails at load time, not skips quietly.
    Directories with no test-shaped file at all (fixture folders such as RequestBody/ResponseBody)
    are left alone.
    """
    by_dir = {}
    for base, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in ("bin", "obj", ".git", "node_modules")]
        for name in sorted(files):
            if not name.lower().endswith((".yaml", ".yml", ".json")):
                continue
            if name in ("suite.config.yaml", "packages.lock.json") or name.startswith("appsettings"):
                continue
            path = os.path.join(base, name)
            try:
                data = load_file(path)
            except Exception as exc:                                  # noqa: BLE001
                text = open(path, encoding="utf-8-sig", errors="replace").read()
                if re.search(r"^\s*api\s*:", text, re.M):
                    report.error(path, "", "could not parse this file: %s" % exc)
                continue
            by_dir.setdefault(base, []).append((path, data))

    found = []
    for entries in by_dir.values():
        test_shaped = [(path, data) for path, data in entries if is_test_file(data)]
        if not test_shaped:
            continue
        test_paths = {path for path, _ in test_shaped}
        for path, data in entries:
            if path in test_paths or not isinstance(data, dict) or not data:
                continue
            for case_name, case in data.items():
                if not (isinstance(case, dict) and "api" in case):
                    report.error(path, case_name, "not a valid test case — missing an 'api' section")
        found += [(path, list(data.items())) for path, data in test_shaped]
    return sorted(found, key=lambda item: (os.path.dirname(item[0]), os.path.basename(item[0])))


# ------------------------------------------------------------------------------- checks

VAR_REF = re.compile(r"\{\{\s*([A-Za-z_][\w]*)(?:\.([A-Za-z_][\w]*))?\s*\}\}")

# Built-in matcher names are read from the library rather than hardcoded here, so adding a
# matcher upstream never leaves this checker rejecting valid tests.
SEMANTIC_SOURCE = "src/ConfIT/Matching/SemanticMatcher.cs"


def builtin_matchers():
    """Names from SemanticMatcher's BuiltIns dictionary, or None when source is unavailable."""
    root = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
    path = os.path.join(root, SEMANTIC_SOURCE)
    if not os.path.isfile(path):
        return None
    text = open(path, encoding="utf-8-sig").read()
    names = re.findall(r'\["([A-Za-z][A-Za-z0-9_]*)"\]\s*=', text)
    return set(names) or None


def custom_matchers(root):
    """Keys inside a 'Dictionary<string, SemanticMatcherFunc>' initializer in the project's C#,
    so registered custom matchers pass. Scoped to that type specifically — not every dictionary-key
    string literal in the project — so an unrelated dictionary cannot accidentally whitelist a
    typo'd or unregistered matcher name."""
    found = set()
    block_re = re.compile(r'Dictionary\s*<\s*string\s*,\s*SemanticMatcherFunc\s*>.*?\n[ \t]*\}\s*;', re.S)
    key_re = re.compile(r'\["([A-Za-z][A-Za-z0-9_]*)"\]\s*=')
    for base, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in ("bin", "obj", ".git")]
        for name in files:
            if not name.endswith(".cs"):
                continue
            try:
                text = open(os.path.join(base, name), encoding="utf-8-sig", errors="replace").read()
            except OSError:
                continue
            for block in block_re.findall(text):
                found.update(key_re.findall(block))
    return found


def walk_strings(node):
    if isinstance(node, dict):
        for value in node.values():
            yield from walk_strings(value)
    elif isinstance(node, list):
        for value in node:
            yield from walk_strings(value)
    elif isinstance(node, str):
        yield node


def check_structure(path, name, case, suite_kind, mock_url, folders, strategy, root, report):
    api = case.get("api")
    if not isinstance(api, dict):
        report.error(path, name, "no 'api' section")
        return

    request = api.get("request")
    if not isinstance(request, dict):
        report.error(path, name, "api.request is missing")
    else:
        graphql = request.get("graphql")
        if graphql is not None:
            if not isinstance(graphql, dict):
                report.error(path, name, "api.request.graphql must be a mapping")
            elif not graphql.get("query") and not graphql.get("queryFromFile"):
                report.error(path, name, "graphql block sets neither 'query' nor 'queryFromFile'")
        elif not request.get("method"):
            report.error(path, name, "api.request.method is missing")
        if not request.get("path"):
            report.error(path, name, "api.request.path is missing")

    response = api.get("response")
    if not isinstance(response, dict):
        report.error(path, name, "api.response is missing")
        return
    if response.get("statusCode") is None:
        report.error(path, name, "api.response.statusCode is missing")
    if "body" not in response and "bodyFromFile" not in response:
        report.error(
            path, name,
            "api.response has no 'body' — the structural diff compares the response against "
            "nothing and always fails. Use 'body: {}' when every field is claimed by a matcher.")

    if case.get("mock") is not None:
        if suite_kind == "integration":
            report.error(path, name, "'mock:' block in an integration suite — no mock server runs there")
        elif suite_kind == "component" and not mock_url:
            report.error(path, name, "'mock:' block but suite.config.yaml sets no component.mock.url")

    if strategy == "tags" and not case.get("tags"):
        report.warn(path, name, "no 'tags:' — this test is skipped whenever the tag filter is active")

    check_fixture_files(path, name, case, folders, root, report)


def check_fixture_files(path, name, case, folders, root, report):
    request_dir = folders.get("requestBody")
    response_dir = folders.get("responseBody")

    def exists(folder, filename):
        if not folder:
            return None
        return os.path.isfile(os.path.join(root, folder, filename))

    def check(payload, folder, label, key="bodyFromFile"):
        if not isinstance(payload, dict):
            return
        filename = payload.get(key)
        if not filename:
            return
        if not folder:
            report.error(path, name, "%s: '%s: %s' but suite.config.yaml sets no matching folders entry"
                         % (label, key, filename))
        elif exists(folder, filename) is False:
            report.error(path, name, "%s: '%s: %s' not found under %s/" % (label, key, filename, folder))

    api = case.get("api") or {}
    check(api.get("request"), request_dir, "api.request")
    check(api.get("response"), response_dir, "api.response")
    graphql = (api.get("request") or {}).get("graphql")
    if isinstance(graphql, dict):
        check(graphql, request_dir, "api.request.graphql", key="queryFromFile")
    for interaction in (case.get("mock") or {}).get("interactions") or []:
        if isinstance(interaction, dict):
            check(interaction.get("request"), request_dir, "mock.request")
            check(interaction.get("response"), response_dir, "mock.response")


def check_matchers(path, name, case, known, report):
    matcher = (((case.get("api") or {}).get("response")) or {}).get("matcher")
    if not isinstance(matcher, dict):
        return

    semantic = matcher.get("semantic") or {}
    if isinstance(semantic, dict):
        for field, spec in semantic.items():
            if not isinstance(spec, str):
                report.error(path, name, "matcher.semantic['%s'] must be a matcher name" % field)
                continue
            if "(" in spec and not spec.endswith(")"):
                report.error(path, name, "matcher.semantic['%s']: '%s' is missing its closing parenthesis"
                             % (field, spec))
                continue
            base = spec.split("(", 1)[0]
            if known is not None and base not in known:
                report.error(path, name, "matcher.semantic['%s']: '%s' is not a built-in matcher and is not "
                                         "registered as a custom matcher in this project" % (field, base))
            if "*" in str(field):
                report.error(path, name, "matcher.semantic['%s']: wildcards are not supported by semantic "
                                         "matchers — use ignore or pattern for per-element fields" % field)
            elif any(seg.isdigit() for seg in str(field).split("__")):
                report.error(path, name, "matcher.semantic['%s']: array indexes do not resolve in semantic "
                                         "paths — assert the element literally, or use pattern/ignore" % field)

    for kind in ("ignore", "pattern"):
        block = matcher.get(kind)
        paths = block if isinstance(block, list) else list(block) if isinstance(block, dict) else []
        for field in paths:
            if str(field).split("__")[-1] == "*":
                report.error(path, name, "matcher.%s['%s']: '*' cannot be the final segment — it selects an "
                                         "array to reach into, not the field being matched" % (kind, field))


def check_depends(path, cases, report):
    order = {name: i for i, (name, _) in enumerate(cases)}
    for index, (name, case) in enumerate(cases):
        depends = case.get("depends") or []
        if isinstance(depends, str):
            depends = [depends]
        for prerequisite in depends:
            if prerequisite not in order:
                report.error(path, name, "depends: '%s' — no test with that name in this file "
                                         "(depends is file-scoped)" % prerequisite)
            elif order[prerequisite] >= index:
                report.error(path, name, "depends: '%s' is defined later in the file — "
                                         "prerequisites must come first" % prerequisite)


def check_variables(files, report):
    extracted = {}          # short name -> [(position, test name)]
    for position, (path, name, case) in enumerate(flatten(files)):
        block = ((case.get("api") or {}).get("response") or {}).get("extract") or {}
        if isinstance(block, dict):
            for variable in block:
                extracted.setdefault(variable, []).append((position, name))

    for position, (path, name, case) in enumerate(flatten(files)):
        seen = set()
        for text in walk_strings(case):
            for match in VAR_REF.finditer(text):
                prefix, suffix = match.group(1), match.group(2)
                reference = match.group(0)
                if reference in seen:
                    continue
                seen.add(reference)
                if suffix:                                    # {{TestName.var}}
                    sources = [s for s in extracted.get(suffix, []) if s[1] == prefix]
                    if not sources:
                        report.error(path, name, "%s — test '%s' does not extract '%s'"
                                     % (reference, prefix, suffix))
                    elif sources[0][0] >= position:
                        report.error(path, name, "%s is extracted by a test that runs later"
                                     % reference)
                    continue
                sources = extracted.get(prefix, [])
                if not sources:
                    report.error(path, name, "%s is never extracted by any test" % reference)
                elif len(sources) > 1:
                    report.error(path, name, "%s is extracted by %s — ambiguous; use {{TestName.%s}}"
                                 % (reference, " and ".join(s[1] for s in sources), prefix))
                elif sources[0][0] >= position:
                    report.error(path, name, "%s is extracted by '%s', which runs later"
                                 % (reference, sources[0][1]))


def check_duplicate_names(files, report):
    seen = {}
    for path, name, _ in flatten(files):
        if name in seen:
            report.error(path, name, "duplicate test name — also defined in %s"
                         % os.path.basename(seen[name]))
        else:
            seen[name] = path


def flatten(files):
    for path, cases in files:
        for name, case in cases:
            yield path, name, case


# -------------------------------------------------------------------------- csproj wiring


def check_registration(root, files, folders, config_path, report):
    projects = []
    for base, dirs, names in os.walk(root):
        dirs[:] = [d for d in dirs if d not in ("bin", "obj", ".git")]
        projects += [os.path.join(base, n) for n in sorted(names) if n.endswith(".csproj")]
    if not projects:
        return
    project = projects[0]
    text = open(project, encoding="utf-8-sig").read()
    registered = []
    for match in re.finditer(r'<None\s+Update="([^"]+)"\s*(?:/>|>(.*?)</None>)', text, re.S):
        registered.append((match.group(1).replace("\\", "/"), match.group(2) or ""))

    def is_registered(relative):
        for pattern, body in registered:
            normalized = pattern.replace("**/", "*/").replace("**", "*")
            if pattern == relative or fnmatch.fnmatch(relative, normalized):
                if "CopyToOutputDirectory" not in body:
                    report.error(project, "", "%s is listed but has no <CopyToOutputDirectory>" % relative)
                return True
        return False

    targets = [path for path, _ in files]
    if config_path:
        targets.append(config_path)
    for folder in {folders.get("requestBody"), folders.get("responseBody")} - {None}:
        directory = os.path.join(root, folder)
        if os.path.isdir(directory):
            targets += [os.path.join(directory, f) for f in sorted(os.listdir(directory))
                        if os.path.isfile(os.path.join(directory, f))]

    for target in targets:
        relative = os.path.relpath(target, os.path.dirname(project)).replace(os.sep, "/")
        if relative.startswith(".."):
            continue
        if not is_registered(relative):
            report.error(project, "", "%s is not registered — add a <None Update> entry with "
                                      "<CopyToOutputDirectory>Always</CopyToOutputDirectory>" % relative)


# ----------------------------------------------------------------------------------- main


def main():
    root = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")
    report = Report()

    kind, mock_url, folders, strategy, config_path = read_suite(root)
    if kind is None:
        print("no suite.config.yaml with a 'component:' or 'integration:' section under %s — "
              "suite-level checks skipped" % root, file=sys.stderr)

    files = discover(root, report)
    if not files:
        print("no ConfIT test definition files found under %s" % root)
        if kind is not None:
            report.error(config_path, "", "suite.config.yaml declares a '%s:' section but no "
                                          "test definition files were found" % kind)
        return 1 if report.print(root) else 0

    known = builtin_matchers()
    if known is not None:
        known |= custom_matchers(root)

    for path, cases in files:
        for name, case in cases:
            check_structure(path, name, case, kind, mock_url, folders, strategy, root, report)
            check_matchers(path, name, case, known, report)
        check_depends(path, cases, report)

    check_duplicate_names(files, report)
    check_variables(files, report)
    check_registration(root, files, folders, config_path, report)

    print("checked %d test file(s), %d test(s)"
          % (len(files), sum(len(cases) for _, cases in files)))
    return 1 if report.print(root) else 0


if __name__ == "__main__":
    sys.exit(main())
