#!/bin/bash
# Builds the whole solution with SonarAnalyzer.CSharp (the rule engine SonarCloud runs, referenced in
# Directory.Build.props) and fails when any analyzer reports an issue. SonarCloud also imports the issues of the
# other Roslyn analyzers (CA, NUnit...), info-level ones included, which never show up as build warnings: every
# diagnostic is read from the SARIF logs of the build instead. Rules turned off for the repository live in
# .editorconfig; Migrations are skipped, as sonar.exclusions does in .github/workflows/sonar.yml.
set -euo pipefail

work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

if ! dotnet build ElsaMina.slnx --no-restore --no-incremental -p:AnalysisLogDirectory="$work_dir" \
    > "$work_dir/build.log" 2>&1; then
  cat "$work_dir/build.log"
  exit 1
fi

issues="$(python3 - "$work_dir" "$PWD" <<'PYTHON'
import glob, json, os, sys
from urllib.parse import unquote, urlparse

log_directory, repository_root = sys.argv[1], sys.argv[2]
issues = set()
for log_file in glob.glob(os.path.join(log_directory, "*.sarif")):
    with open(log_file, encoding="utf-8-sig") as stream:
        log = json.load(stream)
    for run in log.get("runs", []):
        for result in run.get("results", []):
            if result.get("suppressions") or not result.get("locations"):
                continue
            location = result["locations"][0]["physicalLocation"]
            path = os.path.relpath(unquote(urlparse(location["artifactLocation"]["uri"]).path), repository_root)
            if "/Migrations/" in f"/{path}" or path.startswith(".."):
                continue
            region = location.get("region", {})
            issues.add(f"{path}({region.get('startLine', 0)},{region.get('startColumn', 0)}): "
                       f"{result.get('level', 'warning')} {result['ruleId']}: {result['message']['text']}")
print("\n".join(sorted(issues)))
PYTHON
)"

if [[ -n "$issues" ]]; then
  echo "$issues"
  echo
  echo "Code analysis failed: $(echo "$issues" | wc -l) issue(s). Fix them, or suppress one with a justified #pragma."
  exit 1
fi

echo "Code analysis passed: no issues."
