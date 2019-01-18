#!/usr/bin/env bash

set -euo pipefail
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
source "$DIR/files/KoreBuild/KoreBuild.sh"

__usage() {
    echo "Usage: $0 [-v|--verbose] [-d|--dotnet-home <DIR>] [-s|--tools-source <URL>] [[--] <MSBUILD_ARG>...]"
    echo ""
    echo "Arguments:"
    echo "    <MSBUILD_ARG>...         Arguments passed to MSBuild. Variable number of arguments allowed."
    echo ""
    echo "Options:"
    echo "    -v|--verbose             Show verbose output."
    echo "    -d|--dotnet-home <DIR>   The directory where .NET Core tools will be stored. Defaults to '$DOTNET_HOME'."
    echo "    -s|--tools-source <URL>  The base url where build tools can be downloaded. Defaults to '$tools_source'."
    echo "    --ci                     Apply CI specific settings and environment variables."
    exit 2
}

#
# main
#

[ -z "${DOTNET_HOME:-}" ] && DOTNET_HOME="$HOME/.dotnet"
config_file="$DIR/korebuild.json"
tools_source='https://aspnetcore.blob.core.windows.net/buildtools'
verbose=false
ci=false
while [[ $# -gt 0 ]]; do
    case $1 in
        -\?|-h|--help)
            __usage
            ;;
        -d|--dotnet-home)
            shift
            DOTNET_HOME=${1:-}
            [ -z "$DOTNET_HOME" ] && __usage
            ;;
        -s|--tools-source)
            shift
            tools_source=${1:-}
            [ -z "$tools_source" ] && __usage
            ;;
        -v|--verbose)
            verbose=true
            ;;
        --ci|-[Cc][Ii])
            ci=true
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

KOREBUILD_SKIP_INSTALL_NETFX=1

set_korebuildsettings "$tools_source" "$DOTNET_HOME" "$DIR" "$config_file" "$ci"

invoke_korebuild_command "default-build" "$@"
