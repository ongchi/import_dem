#!/usr/bin/env bash
# Builds the DEM import plugin and runs the unit tests.
#
# Usage:
#   ./build.sh            # build the plugin in Release and run the tests
#   ./build.sh Debug      # build in Debug

set -euo pipefail

CONFIGURATION="${1:-Release}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOTNET="${DOTNET:-dotnet}"

if ! command -v "${DOTNET}" >/dev/null 2>&1; then
    if [ -x /run/current-system/sw/bin/dotnet ]; then
        DOTNET=/run/current-system/sw/bin/dotnet
    else
        echo "The .NET SDK is not on the PATH. Set the DOTNET variable to the dotnet executable." >&2
        exit 1
    fi
fi

echo "Building Import_DEM (${CONFIGURATION})"
"${DOTNET}" build "${ROOT_DIR}/src/Import_DEM/Import_DEM.csproj" -c "${CONFIGURATION}"

echo "Running the reader tests"
"${DOTNET}" test "${ROOT_DIR}/tests/Import_DEM.Tests/Import_DEM.Tests.csproj" -c "${CONFIGURATION}"

PLUGIN_PATH="${ROOT_DIR}/src/Import_DEM/bin/${CONFIGURATION}/net7.0/Import_DEM.rhp"
echo
echo "Plugin: ${PLUGIN_PATH}"
echo "Install it: ./tools/install_mac.sh, then restart Rhino 8."
