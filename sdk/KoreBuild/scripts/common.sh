#!/usr/bin/env bash

__korebuild_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# increase file descriptor limit
if [ "$(uname)" == "Darwin" ]; then
    ulimit -n 5000
fi

# colors
GREEN="\033[1;32m"
CYAN="\033[0;36m"
RESET="\033[0m"
RED="\033[0;31m"
GRAY="\033[0;90m"

__is_verbose=false

__verbose() {
    if [ "$__is_verbose" = true ]; then
        echo -e "${GRAY}debug: $@${RESET}"
    fi
}

__machine_has() {
    hash "$1" > /dev/null 2>&1
    return $?
}

__error() {
    echo -e "${RED}$@${RESET}" 1>&2
}

__exec() {
    local cmd=$1
    shift
    local cmdname=$(basename $cmd)
    echo -e "${CYAN}>>> $cmdname $@${RESET}"
    set +e
    $cmd "$@"
    local exit_code=$?
    set -e
    if [ $exit_code -ne 0 ]; then
        __error "<<< $cmdname failed with exit code $exit_code"
    elif [ "$__is_verbose" = true ]; then
        echo -e "${GREEN}<<< $cmdname [$exit_code]${RESET}"
    fi
    return $exit_code
}

__ensure_osx_version() {
        # Check that OS is 10.12 or newer
    osx_version="$(sw_vers | grep ProductVersion | awk '{print $2}')"
    minor_version="$(echo $osx_version | awk -F '.' '{print $2}')"
    __verbose "Detected macOS version $osx_version"
    if [ $minor_version -lt 12 ]; then
        __error ".NET Core 2.0 requires OSX 10.12 or newer. Current version is $osx_version."
        return 1
    fi
}

__get_dotnet_sdk_version() {
    version=$(cat "$__korebuild_dir/../config/sdk.version" | head -1 | tr -d '[:space:]')
    # environment override
    [ ! -z "${KOREBUILD_DOTNET_VERSION:-}" ] && version=${KOREBUILD_DOTNET_VERSION:-}
    echo $version
}

__fetch() {
    local remote_path=$1
    local local_path=$2

    if [[ "$remote_path" != 'http'* ]]; then
        __verbose "fetching local file $remote_path"
        cp $remote_path $local_path
        return 0
    fi

    __verbose "Downloading $remote_path"
    failed=false
    if __machine_has 'curl'; then
        # Only show progress bar if shell is interactive
        progress_bar=''
        [ -z "${PS1:-}" ] && progress_bar='-#'
        __exec curl $progress_bar --retry 10 -sSL -f --create-dirs -o $local_path $remote_path || failed=true
    elif __machine_has 'wget'; then
        __exec wget -v --tries 10 -O $local_path $remote_path || failed=true
    else
        __error 'wget or curl is required to download assets'
        return 1
    fi

    if [ "$failed" = true ]; then
        __error "Download failed: $remote_path"
        return 1
    fi
}
