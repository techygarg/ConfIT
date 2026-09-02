#!/usr/bin/env bash
#
# Prints the absolute path of the ConfIT checkout this skill ships inside — the reference the
# skill reads (example/, doc/, src/).
#
# Skills live at <repo-root>/skills/<name>/, so the reference sits alongside them. Rather than
# counting "../.." levels — which breaks when the skill is reached through a symlink — this
# resolves the script's own physical location and walks up for the repository markers.
#
#   bash reference-path.sh           print the root, or exit 1 with guidance
#   bash reference-path.sh --check   also list what was found
#
# Exit 1 means the reference is missing: the plugin install is broken or partial. Do not write
# tests from memory in that case — report it.

set -u

if [ -t 1 ]; then RED=$'\033[31m'; OFF=$'\033[0m'; else RED=""; OFF=""; fi

# A directory is the ConfIT root when it holds all three. Requiring skills/ too keeps this from
# matching some unrelated repository that happens to have example/ and doc/.
is_root() {
    [ -d "$1/example" ] && [ -d "$1/doc" ] && [ -d "$1/skills" ]
}

ROOT=""

# The plugin host may name the root outright.
if [ -n "${CLAUDE_PLUGIN_ROOT:-}" ] && is_root "$CLAUDE_PLUGIN_ROOT"; then
    ROOT="$(cd "$CLAUDE_PLUGIN_ROOT" && pwd -P)"
fi

# Otherwise walk up from this script's real location. pwd -P resolves any symlink in the path.
if [ -z "$ROOT" ]; then
    dir="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")" && pwd -P)"
    while [ "$dir" != "/" ]; do
        if is_root "$dir"; then ROOT="$dir"; break; fi
        dir="$(dirname "$dir")"
    done
fi

if [ -z "$ROOT" ]; then
    printf '%serror%s ConfIT reference not found.\n' "$RED" "$OFF" >&2
    cat >&2 <<'GUIDANCE'

This skill reads ConfIT's own example suites and documentation, which ship in the same
repository. Neither example/ nor doc/ could be located from the skill's install path, so the
plugin install is broken or partial.

Reinstall the plugin, then retry:

  Claude Code   /plugin marketplace add techygarg/ConfIT
                /plugin install confit@confit

  Working in the ConfIT repository itself:
                claude --plugin-dir .

Do not fall back to writing tests from memory — ConfIT's DSL, matcher set and config schema
move between releases.
GUIDANCE
    exit 1
fi

echo "$ROOT"

if [ "${1:-}" = "--check" ]; then
    {
        echo
        echo "reference contents:"
        for d in example doc src/ConfIT tools; do
            if [ -e "$ROOT/$d" ]; then echo "  ok      $d"; else echo "  missing $d"; fi
        done
        echo
        echo "suites available:"
        find "$ROOT/example" -maxdepth 2 -name suite.config.yaml -not -path '*/bin/*' 2>/dev/null \
            | sed "s|$ROOT/|  |;s|/suite.config.yaml||"
    } >&2
fi
