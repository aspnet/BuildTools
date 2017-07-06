#!/usr/bin/env bash

set -euo pipefail

# korebuild config values
channel='rel/2.0.0'
tools_source='https://aspnetcore.blob.core.windows.net/buildtools'

#
# variables
#

RESET="\033[0m"
RED="\033[0;31m"
MAGENTA="\033[0;95m"
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
[ -z "${DOTNET_HOME:-}"] && DOTNET_HOME="$HOME/.dotnet"
verbose=false
update=false
repo_path="$DIR"

#
# Functions
#
__usage() {
    echo "Usage: $(basename ${BASH_SOURCE[0]}) [options] [[--] <MSBUILD_ARG>...]"
    echo ""
    echo "Arguments:"
    echo "    <MSBUILD_ARG>...         Arguments passed to MSBuild. Variable number of arguments allowed."
    echo ""
    echo "Options:"
    echo "    --verbose                Show verbose output."
    echo "    -c|--channel <CHANNEL>   The channel of KoreBuild to download. Defaults to '$channel'."
    echo "    -d|--dotnet-home <DIR>   The directory where .NET Core tools will be stored. Defaults to '\$DOTNET_HOME' or '\$HOME/.dotnet."
    echo "    --path <PATH>            The directory to build. Defaults to the directory containing the script."
    echo "    -s|--tools-source <URL>  The base url where build tools can be downloaded. Defaults to '$tools_source'."
    echo "    -u|--update              Update to the latest KoreBuild even if the lock file is present."
    echo ""
    echo "Description:"
    echo "    This function will create a file \$DIR/korebuild-lock.txt. This lock file can be committed to source, but does not have to be."
    echo "    When the lockfile is not present, KoreBuild will create one using latest available version from \$channel."

    if [[ "${1:-}" != '--no-exit' ]]; then
        exit 2
    fi
}

get_korebuild() {
    local lock_file="$repo_path/korebuild-lock.txt"
    if [ ! -f $lock_file ] || [ "$update" = true ]; then
        __get_remote_file "$tools_source/korebuild/channels/$channel/latest.txt" $lock_file
    fi
    local version=$(cat $lock_file | tail -1 | tr -d '[:space:]')
    local korebuild_path="$DOTNET_HOME/buildtools/korebuild/$version"
    if [ ! -d "$korebuild_path" ]; then
        mkdir -p "$korebuild_path"
        local remote_path="$tools_source/korebuild/artifacts/$version/korebuild.$version.zip"
        tmpfile="$(mktemp)"
        echo -e "${MAGENTA}Downloading KoreBuild ${version}${RESET}"
        __get_remote_file $remote_path $tmpfile
        unzip -q -d "$korebuild_path" $tmpfile
        rm $tmpfile || true
    fi

    source "$korebuild_path/KoreBuild.sh"
}

__fatal() {
    echo -e "${RED}$@${RESET}" 1>&2
    exit 1
}

__machine_has() {
    hash "$1" > /dev/null 2>&1
    return $?
}

__get_remote_file() {
    local remote_path=$1
    local local_path=$2

    if [[ "$remote_path" != 'http'* ]]; then
        cp $remote_path $local_path
        return 0
    fi

    failed=false
    if __machine_has curl ; then
        curl --retry 10 -sSL -f --create-dirs -o $local_path $remote_path || failed=true
    elif __machine_has wget; then
        wget --tries 10 -O $local_path $remote_path || failed=true
    else
        failed=true
    fi

    if [ "$failed" = true ]; then
        __fatal "Download failed: $remote_path" 1>&2
    fi
}

#
# main
#

while [[ $# > 0 ]]; do
    case $1 in
        -\?|-h|--help)
            __usage --no-exit
            exit 0
            ;;
        -c|--channel|-Channel)
            shift
            channel=${1:-}
            [ -z "$channel" ] && __usage
            ;;
        -d|--dotnet-home|-DotNetHome)
            shift
            DOTNET_HOME=${1:-}
            [ -z "$DOTNET_HOME" ] && __usage
            ;;
        --path|-Path)
            shift
            repo_path="${1:-}"
            [ -z "$repo_path" ] && __usage
            ;;
        -s|--tools-source|-ToolsSource)
            shift
            tools_source="${1:-}"
            [ -z "$tools_source" ] && __usage
            ;;
        -u|--update|-Update)
            update=true
            ;;
        --verbose|-Verbose)
            verbose=true
            ;;
        --)
            shift
            break
            ;;
        *)
            break
            ;;
    esac
    shift
done

if ! __machine_has unzip; then
    __fatal 'Missing required command: unzip'
fi

if ! __machine_has curl && ! __machine_has wget; then
    __fatal 'Missing required command. Either wget or curl is required.'
fi

get_korebuild
install_tools "$tools_source" "$DOTNET_HOME"
invoke_repository_build "$repo_path" $@
