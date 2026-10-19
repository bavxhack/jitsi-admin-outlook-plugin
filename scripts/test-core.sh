#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
command -v dotnet >/dev/null || { echo 'Erforderlich: .NET SDK 8 (dotnet fehlt).' >&2; exit 1; }
dotnet test tests/JitsiAdmin.OutlookPlugin.Core.Tests/JitsiAdmin.OutlookPlugin.Core.Tests.csproj --configuration Release --logger "trx;LogFileName=core.trx" --results-directory artifacts/test-results
