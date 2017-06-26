#!/usr/bin/env bash

set -euo pipefail
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
channel='dev'
tools_source='https://aspnetcore.blob.core.windows.net/buildtools'
[ -z "${DOTNET_HOME:-}"] && DOTNET_HOME="$HOME/.dotnet"
verbose=false
update=false
repo_path="$DIR"

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
    echo "    -p|--path <PATH>         The directory to build. Defaults to the directory containing the script."
    echo "    -s|--tools-source <URL>  The base url where build tools can be downloaded. Defaults to '$tools_source'."
    echo "    -u|--update              Update to the latest KoreBuild even if the lock file is present."
    echo ""
    echo "Description:"
    echo "    This function will create a file \$DIR/korebuild-lock.txt. This lock file can be committed to source, but does not have to be."
    echo "    When the lockfile is not present, KoreBuild will create one using latest available version from \$channel."
    exit 2
}

#
# Functions
#

get_korebuild() {

    local lock_file="$repo_path/korebuild-lock.txt"
    if [ ! -f $lock_file ] || [ "$update" = true ]; then
        __fetch "$tools_source/korebuild/channels/$channel/latest.txt" $lock_file
    fi
    local version=$(cat $lock_file | tail -1 | tr -d '[:space:]')
    local korebuild_path="$DOTNET_HOME/buildtools/korebuild/$version"
    if [ ! -d "$korebuild_path" ]; then
        mkdir -p "$korebuild_path"
        local remote_path="$tools_source/korebuild/artifacts/$version/korebuild.$version.zip"

        if ! __machine_has unzip; then
            echo 'Missing required command: unzip'
            return 1
        fi

        tmpfile="$(mktemp)"
        echo "Downloading KoreBuild $version"
        __fetch $remote_path $tmpfile
        unzip -q -d "$korebuild_path" $tmpfile
        rm $tmpfile || :
    fi

    source "$korebuild_path/KoreBuild.sh"
}

__machine_has() {
    hash "$1" > /dev/null 2>&1
    return $?
}

__fetch() {
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
        echo 'wget or curl is required to download assets' 1>&2
        return 1
    fi

    if [ "$failed" = true ]; then
        echo "Download failed: $remote_path" 1>&2
        return 1
    fi
}

#
# main
#

while [[ $# > 0 ]]; do
    case $1 in
        -\?|-h|--help)
            __usage
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
        -p|--path|-Path)
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

get_korebuild
install_tools "$tools_source" "$DOTNET_HOME"
invoke_repository_build "$repo_path" $@
