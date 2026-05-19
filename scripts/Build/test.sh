#!/bin/bash
set -euo pipefail

source "$(dirname "$0")/shared.sh"
dotnet test ElsaMina.slnx --no-restore --verbosity normal --logger "trx;LogFileName=test-results.trx"
