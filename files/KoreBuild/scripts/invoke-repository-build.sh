#!/usr/bin/env bash

__script_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# flow failures thru piped commands, disallow unrecognized variables, and exit on first failure
set -euo pipefail

source "$__script_dir/common.sh"

__usage() {
    echo ""
    echo "Usage: $0 <PATH> [--verbose] [<MSBUILD_ARG>...]"
    echo ""
    echo "       <PATH>          The folder to build"
    echo "       --verbose       Produce more diagnostics about this build"
    echo "       <MSBUILD_ARG>   Argument passed to MSBuild. Multiple arguments allowed."
}

########
# main #
########

repo_path=''
if [[ $# -gt 0 ]]; then
    repo_path=$1
    shift
fi

noop=false
msbuild_args=''
msbuild_props_args=''
while [[ $# -gt 0 ]]; do
    case $1 in
        --verbose)
            __is_verbose=true
            ;;
        /t:[Cc]ow|/t:[Nn]oop)
            noop=true
            msbuild_args+="\"$1\"\n"
            ;;
        /p:*|/property:*|-p:*|-property:*)
            msbuild_args+="\"$1\"\n"
            msbuild_props_args+="\"$1\" "
            ;;
        *)
            msbuild_args+="\"$1\"\n"
            ;;
    esac
    shift
done

if [ "$repo_path" == "" ] || [ ! -d "$repo_path" ]; then
    __error "Error: <PATH> was not provided or not found."
    __usage
    exit 1
fi

repo_path="$(cd "$repo_path" && pwd)"
__verbose "Building $repo_path"

sdk_version="$(__get_dotnet_sdk_version)"
if [ "$sdk_version" != 'latest' ]; then
    echo "{ \"sdk\": { \"version\": \"${sdk_version}\" } }" > "$repo_path/global.json"
else
    __verbose "Skipping global.json generation because the \$sdk_version = $sdk_version"
fi

korebuild_proj="$__script_dir/../KoreBuild.proj"
msbuild_artifacts_dir="$repo_path/artifacts/logs"
msbuild_response_file="$msbuild_artifacts_dir/msbuild.rsp"
msbuild_log_rsp_file="$msbuild_artifacts_dir/msbuild.logger.rsp"
msbuild_log_argument=""

if [ "$__is_verbose" = true ] || [ ! -z "${KOREBUILD_ENABLE_BINARY_LOG:-}" ]; then
    __verbose "Enabling binary logging"
    ms_build_log_file="$msbuild_artifacts_dir/msbuild.binlog"
    msbuild_log_argument="/bl:$ms_build_log_file"
fi

if [ ! -f "$msbuild_artifacts_dir" ]; then
    mkdir -p "$msbuild_artifacts_dir"
fi

korebuild_version="$(__get_korebuild_version)"
cat > "$msbuild_response_file" <<ENDMSBUILDARGS
/nologo
/m
/p:KoreBuildVersion=$korebuild_version
/p:RepositoryRoot="$repo_path/"
"$msbuild_log_argument"
"$korebuild_proj"
ENDMSBUILDARGS
echo -e "$msbuild_args" >> "$msbuild_response_file"

if [ ! -z "$KOREBUILD_TEAMCITY_LOGGER" ]; then
    cat > "$msbuild_log_rsp_file" <<ENDMSBUILDARGS
/noconsolelogger
/verbosity:normal
/logger:TeamCity.MSBuild.Logger.TeamCityMSBuildLogger,$KOREBUILD_TEAMCITY_LOGGER;teamcity
ENDMSBUILDARGS
else
    cat > "$msbuild_log_rsp_file" <<ENDMSBUILDARGS
/clp:Summary
ENDMSBUILDARGS
fi

__verbose "dotnet = $(which dotnet)"

task_proj="$repo_path/build/tasks/RepoTasks.csproj"

__verbose "Noop = $noop"
if [ "${noop}" = true ]; then
    export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true
elif [ -f "$task_proj" ]; then
    sdk_path="-p:RepoTasksSdkPath=$__script_dir/../msbuild/KoreBuild.RepoTasks.Sdk/Sdk/"
    task_publish_dir="$repo_path/build/tasks/bin/publish/"
    __exec dotnet publish "$task_proj" --configuration Release --output "$task_publish_dir" -nologo "$sdk_path" @"$msbuild_log_rsp_file" $msbuild_props_args
fi

__verbose "Invoking msbuild with '$(< "$msbuild_response_file")'"
__exec dotnet msbuild @"$msbuild_response_file" @"$msbuild_log_rsp_file"
