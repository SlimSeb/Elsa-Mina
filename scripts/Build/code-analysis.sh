#!/bin/bash
# Builds the whole solution with SonarAnalyzer.CSharp (the rule engine SonarCloud runs, referenced in
# Directory.Build.props) and fails when any Sonar rule reports an issue. Rules turned off for the repository
# live in .editorconfig.
set -euo pipefail

build_log="$(mktemp)"
trap 'rm -f "$build_log"' EXIT

if ! dotnet build ElsaMina.slnx --no-restore --no-incremental > "$build_log" 2>&1; then
  cat "$build_log"
  exit 1
fi

# MSBuild repeats each warning in its summary: keep one line per issue, without the project suffix
issues="$(grep -E ': warning S[0-9]+: ' "$build_log" | sed -E 's# \[[^]]*\]$##' | sort -u || true)"
if [[ -n "$issues" ]]; then
  echo "$issues"
  echo
  echo "Sonar analysis failed: $(echo "$issues" | wc -l) issue(s). Fix them, or suppress one with a justified #pragma."
  exit 1
fi

echo "Sonar analysis passed: no issues."
