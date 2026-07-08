#!/bin/bash
set -euo pipefail

source "$(dirname "$0")/shared.sh"
dotnet build ./src/ElsaMina.Console/ElsaMina.Console.csproj ${BUILD_PROPERTIES} -c "${CONFIGURATION}" -r "${RUNTIME_ID}" --no-restore
