#!/usr/bin/env bash

set -euo pipefail

command=$1
repo_path=$2
no_build=false
msbuild_args=()

shift 2

while [[ $# -gt 0 ]]; do
    case $1 in
        --no-build|-NoBuild)
            no_build=true
            ;;
        *)
            msbuild_args[${#msbuild_args[*]}]="$1"
            ;;
    esac
    shift
done

if [ "$no_build" = false ]; then
    ./build.sh /t:Package
fi

./scripts/bootstrapper/run.sh \
    "$command" \
    -Path "$repo_path" \
    -ToolsSource ./artifacts/ \
    -Update \
    -Reinstall \
    ${msbuild_args[@]+"${msbuild_args[@]}"}
