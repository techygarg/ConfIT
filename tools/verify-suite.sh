#!/usr/bin/env bash
# verify-suite.sh — wiring diagnostics for a ConfIT test project.
#
# Reports the setup faults that surface later as confusing runtime symptoms: a missing package
# reference, unregistered config or test files, a fixture that does not match the config
# section, a hardcoded filter, a framework mismatch.
#
# Usage:  bash verify-suite.sh [TEST_PROJECT_DIR]     # defaults to the current directory
# Exit:   1 when any ERROR is reported.

set -u

DIR="${1:-.}"
ERRORS=0
WARNS=0

if [ -t 1 ]; then RED=$'\033[31m'; YEL=$'\033[33m'; GRN=$'\033[32m'; OFF=$'\033[0m'
else RED=""; YEL=""; GRN=""; OFF=""; fi

err()   { printf '  %sERROR%s  %s\n' "$RED" "$OFF" "$1"; ERRORS=$((ERRORS + 1)); }
warn()  { printf '  %sWARN %s  %s\n' "$YEL" "$OFF" "$1"; WARNS=$((WARNS + 1)); }
ok()    { printf '  %sok%s     %s\n' "$GRN" "$OFF" "$1"; }
section() { printf '\n%s\n' "$1"; }

[ -d "$DIR" ] || { echo "no such directory: $DIR" >&2; exit 2; }
DIR="$(cd "$DIR" && pwd)"

CSPROJ="$(find "$DIR" -maxdepth 2 -name '*.csproj' -not -path '*/bin/*' -not -path '*/obj/*' | head -1)"
[ -n "$CSPROJ" ] || { echo "no .csproj found under $DIR" >&2; exit 2; }

# Ground truth comes from the ConfIT checkout this script ships inside, never from a value
# hardcoded here. pwd -P so a symlinked invocation still lands on the real root.
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." 2>/dev/null && pwd -P || true)"
LIB_CSPROJ="$REPO_ROOT/src/ConfIT/ConfIT.csproj"
SUPPORTED_TFM=""
if [ -n "$REPO_ROOT" ] && [ -f "$LIB_CSPROJ" ]; then
    SUPPORTED_TFM="$(sed -n 's/.*<TargetFrameworks*>\([^<]*\)<.*/\1/p' "$LIB_CSPROJ" | head -1)"
fi

# Every <None Update="..."> path that carries a <CopyToOutputDirectory>, separators normalised.
# Splitting on '<' turns each XML tag into its own line, which keeps this awk-only.
REGISTERED="$(tr '<' '\n' < "$CSPROJ" | awk '
    /^None[[:space:]]+Update=/      { p = (match($0, /Update="[^"]*"/) ? substr($0, RSTART + 8, RLENGTH - 9) : ""); next }
    /^CopyToOutputDirectory>/       { if (p != "") { print p; p = "" } next }
    /^\/None>/                      { p = "" }
' | tr '\\' '/')"

registered() {
    local rel="$1" pattern
    while IFS= read -r pattern; do
        [ -z "$pattern" ] && continue
        [ "$pattern" = "$rel" ] && return 0
        case "$pattern" in
            *'*'*) case "$rel" in ${pattern//\*\*/\*}) return 0 ;; esac ;;
        esac
    done <<< "$REGISTERED"
    return 1
}

sources()    { find "$DIR" -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*'; }
in_sources() { sources | tr '\n' '\0' | xargs -0 grep -l "$1" 2>/dev/null | head -1; }

echo "ConfIT suite check: $(basename "$CSPROJ")"

# --------------------------------------------------------------------- project references
section "project"
if grep -Eq '<(Package|Project)Reference[^>]*"[^"]*ConfIT' "$CSPROJ"; then
    ok "ConfIT is referenced"
else
    err "no ConfIT reference — run: dotnet add package ConfIT"
fi

if grep -q '<PackageReference[^>]*"xunit' "$CSPROJ"; then
    ok "xunit is referenced"
else
    warn "no xunit reference — BaseTest is built around xUnit's [Theory] / [MemberData]"
fi

TFM="$(sed -n 's/.*<TargetFrameworks*>\([^<]*\)<.*/\1/p' "$CSPROJ" | head -1)"
if [ -z "$TFM" ]; then
    warn "no TargetFramework found in the csproj"
elif [ -z "$SUPPORTED_TFM" ]; then
    ok "target framework: $TFM"
    warn "could not read ConfIT's supported frameworks from src/ConfIT/ConfIT.csproj — not verified"
else
    _matched=0
    for _want in $(printf '%s' "$TFM" | tr ';' ' '); do
        for _have in $(printf '%s' "$SUPPORTED_TFM" | tr ';' ' '); do
            [ "$_want" = "$_have" ] && _matched=1
        done
    done
    if [ "$_matched" -eq 1 ]; then
        ok "target framework: $TFM (ConfIT ships $SUPPORTED_TFM)"
    else
        err "target framework '$TFM' — ConfIT ships $SUPPORTED_TFM"
    fi
fi

# ----------------------------------------------------------------------------- suite config
section "suite.config.yaml"
CONFIG="$DIR/suite.config.yaml"
SUITE_SECTION=""
if [ -f "$CONFIG" ]; then
    ok "present"
    if registered "suite.config.yaml"; then
        ok "registered with CopyToOutputDirectory"
    else
        err "not registered — add <None Update=\"suite.config.yaml\"> with <CopyToOutputDirectory>Always</CopyToOutputDirectory>"
    fi

    if grep -q '^component:' "$CONFIG"; then
        SUITE_SECTION="component"
    elif grep -q '^integration:' "$CONFIG"; then
        SUITE_SECTION="integration"
    else
        err "neither a 'component:' nor an 'integration:' section"
    fi
    [ -n "$SUITE_SECTION" ] && ok "section: $SUITE_SECTION"

    grep -Eq '^[[:space:]]+url:' "$CONFIG" || err "no 'api.url' — required in every mode"

    if [ "$SUITE_SECTION" = "component" ]; then
        MODE="$(sed -n 's/^[[:space:]]*mode:[[:space:]]*\([a-z-]*\).*/\1/p' "$CONFIG" | head -1)"
        case "${MODE:-in-process}" in
            in-process)
                SETTINGS="$(sed -n 's/^[[:space:]]*settings:[[:space:]]*\([^[:space:]]*\).*/\1/p' "$CONFIG" | head -1)"
                if [ -z "$SETTINGS" ]; then
                    err "mode is in-process but 'startup.settings' is not set"
                elif [ -f "$DIR/$SETTINGS" ]; then
                    ok "startup.settings: $SETTINGS"
                    registered "$SETTINGS" || err "$SETTINGS is not registered with CopyToOutputDirectory"
                else
                    err "startup.settings names '$SETTINGS', which is not in the project"
                fi
                ;;
            command)
                grep -q 'command:' "$CONFIG" || err "mode is command but 'startup.command' is not set"
                if grep -q 'readiness:' "$CONFIG"; then
                    if grep -Eq '^[[:space:]]+(port|url):' "$CONFIG"; then
                        ok "readiness probe declared"
                    else
                        err "readiness: needs exactly one of 'port' or 'url'"
                    fi
                else
                    err "mode is command but there is no 'readiness:' block — the launcher cannot tell when the app is up"
                fi
                grep -q 'stopCommand:' "$CONFIG" \
                    || warn "no 'stopCommand' — add one if the port is not released between runs"
                ;;
            *) err "unknown startup mode '$MODE' — expected in-process or command" ;;
        esac
    fi

    grep -Eq 'strategy:[[:space:]]*tags' "$CONFIG" \
        && ok "tag filter configured — every test then needs a 'tags:' entry"
    grep -q '\${' "$CONFIG" \
        && ok "uses \${ENV_VAR} interpolation — export those variables before running"
else
    err "suite.config.yaml not found in $DIR"
fi

# ---------------------------------------------------------------------------------- fixture
section "fixture"
FIXTURE="$(in_sources 'SuiteBootstrapper\.For')"
if [ -n "$FIXTURE" ]; then
    ok "bootstrapped in $(basename "$FIXTURE")"
    CALL="$(grep -o 'SuiteBootstrapper\.For[A-Za-z]*' "$FIXTURE" | head -1)"
    ok "$CALL"
    case "$SUITE_SECTION:$CALL" in
        component:SuiteBootstrapper.ForIntegration)
            err "config declares a 'component:' section but the fixture calls ForIntegration" ;;
        integration:SuiteBootstrapper.ForComponent|integration:SuiteBootstrapper.ForCommand)
            err "config declares an 'integration:' section but the fixture calls ${CALL#SuiteBootstrapper.}" ;;
    esac
    grep -q 'Dispose' "$FIXTURE" \
        || err "the fixture never disposes the suite — the summary will not print and infrastructure will leak"
    grep -q 'TestSuiteContext' "$FIXTURE" \
        || warn "the fixture exposes no TestSuiteContext property for the test class to consume"
elif [ -n "$(in_sources 'new TestSuiteContext')" ]; then
    ok "manually wired TestSuiteContext (adapter-chain or manual setup)"
else
    err "no fixture found — neither a SuiteBootstrapper.For* call nor a TestSuiteContext construction"
fi

# ------------------------------------------------------------------------------- test class
section "test class"
TESTCLASS="$(in_sources ': BaseTest')"
if [ -n "$TESTCLASS" ]; then
    ok "BaseTest subclass: $(basename "$TESTCLASS")"
    grep -q 'IClassFixture<' "$TESTCLASS" \
        || warn "no IClassFixture<> — the suite would start once per test instead of once per class"
    grep -q 'TestReader\.GetTestsFor' "$TESTCLASS" \
        || err "no TestReader.GetTestsForAFolder / GetTestsForAFile — nothing will be discovered"
    grep -q 'MemberData' "$TESTCLASS" \
        || err "no [MemberData] — the theory has no rows to run"
else
    err "no class deriving from BaseTest"
fi

[ -n "$(in_sources 'ITestOutputLogger')" ] \
    && ok "ITestOutputLogger adapter present" \
    || warn "no ITestOutputLogger implementation — ConfIT log output will not reach xUnit"

[ -n "$(in_sources 'Environment\.SetEnvironmentVariable')" ] \
    && warn "Environment.SetEnvironmentVariable in test code — a hardcoded filter hides tests from CI"

# ------------------------------------------------------------------------- test definitions
section "test definitions"
FOLDER=""
for candidate in TestCase TestCases Tests; do
    [ -d "$DIR/$candidate" ] && { FOLDER="$candidate"; break; }
done
if [ -z "$FOLDER" ]; then
    err "no TestCase folder found — create one and point [MemberData] at it"
else
    COUNT=$(find "$DIR/$FOLDER" -maxdepth 1 \( -name '*.yaml' -o -name '*.yml' -o -name '*.json' \) | wc -l | tr -d ' ')
    if [ "$COUNT" -gt 0 ]; then
        ok "$COUNT test definition file(s) in $FOLDER/"
    else
        err "$FOLDER/ contains no .yaml / .yml / .json files"
    fi

    MISSING=0
    while IFS= read -r file; do
        [ -z "$file" ] && continue
        rel="${file#"$DIR"/}"
        registered "$rel" || { err "$rel is not registered with CopyToOutputDirectory"; MISSING=$((MISSING + 1)); }
    done <<< "$(find "$DIR/$FOLDER" -type f \( -name '*.yaml' -o -name '*.yml' -o -name '*.json' -o -name '*.graphql' \))"
    [ "$MISSING" -eq 0 ] && ok "all test definitions and body fixtures are registered"
fi

OUT="$(find "$DIR/bin" -maxdepth 2 -type d -name 'net*' 2>/dev/null | head -1)"
if [ -n "$OUT" ]; then
    [ -f "$OUT/suite.config.yaml" ] \
        && ok "suite.config.yaml reached the output directory" \
        || warn "suite.config.yaml is not in $OUT — rebuild, then re-check"
fi

printf '\n%d error(s), %d warning(s)\n' "$ERRORS" "$WARNS"
[ "$ERRORS" -eq 0 ] || exit 1
