#!/bin/bash
set -euo pipefail

source "$(dirname "$0")/shared.sh"
dotnet publish ./src/ElsaMina.Console/ElsaMina.Console.csproj ${BUILD_PROPERTIES} -c "${CONFIGURATION}" --no-restore --no-build -o ./output
