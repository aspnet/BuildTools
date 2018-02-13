#!/usr/bin/env bash

set -euo pipefail

command='default-build'
repo_path=''
no_build=false
msbuild_args=()

while [[ $# -gt 0 ]]; do
    case $1 in
        --no-build|-NoBuild)
            no_build=true
            ;;
        -r|--repo-path|-RepoPath)
            shift
            repo_path="$1"
            ;;
        -c|--command|-Command)
            shift
            command="$1"
            ;;
        *)
            msbuild_args[${#msbuild_args[*]}]="$1"
            ;;
    esac
    shift
done

if [ -z "$repo_path" ]; then
    echo "Missing required value for --repo-path"
    exit 1
fi

if [ "$no_build" = false ]; then
    ./build.sh /p:SkipTests=true
fi

./scripts/bootstrapper/run.sh \
    "$command" \
    -Path "$repo_path" \
    -ToolsSource ./artifacts/ \
    -Update \
    -Reinstall \
    ${msbuild_args[@]+"${msbuild_args[@]}"}
