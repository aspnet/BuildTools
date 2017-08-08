#!/usr/bin/env bash

local command=$1
local repo_path=$2

shift 2

./build.sh /t:PackageKoreBuild

./scripts/bootstrapper/run.sh -Command "$command" -Path "$repo_path" -s ./artifacts/ -u "$@"
