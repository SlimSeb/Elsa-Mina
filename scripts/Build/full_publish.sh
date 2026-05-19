#!/usr/bin/env bash
set -euo pipefail

echo "=== Restore ==="
./scripts/Build/restore.sh

echo "=== Build ==="
./scripts/Build/build.sh

echo "=== Test ==="
./scripts/Build/test.sh

echo "=== Publish ==="
./scripts/Build/publish.sh

echo "=== Done ==="
