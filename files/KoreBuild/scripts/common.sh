#!/usr/bin/env bash

# colors
export GREEN="\033[1;32m"
export MAGENTA="\033[0;95m"
export YELLOW="\033[0;33m"
export CYAN="\033[0;36m"
export RESET="\033[0m"
export RED="\033[0;31m"
export GRAY="\033[0;90m"

__is_verbose=false

__verbose() {
    if [ "$__is_verbose" = true ]; then
        echo -e "${GRAY}debug  : $*${RESET}"
    fi
}

__warn() {
    echo -e "${YELLOW}warning: $*${RESET}"
}

__error() {
    echo -e "${RED}error  : $*${RESET}" 1>&2
}

__machine_has() {
    hash "$1" > /dev/null 2>&1
    return $?
}

__exec() {
    local cmd=$1
    shift
    local cmdname
    cmdname=$(basename "$cmd")
    echo -e "${CYAN}>>> $cmdname $*${RESET}"
    set +e
    $cmd "$@"
    local exit_code=$?
    set -e
    if [ $exit_code -ne 0 ]; then
        __error "$cmdname failed with exit code $exit_code"
    elif [ "$__is_verbose" = true ]; then
        echo -e "${GREEN}<<< $cmdname [$exit_code]${RESET}"
    fi
    return $exit_code
}

__ensure_macos_version() {
    # Check that OS is 10.12 or newer
    local macos_version="$(sw_vers | grep ProductVersion | awk '{print $2}')"
    local minor_version="$(echo "$macos_version" | awk -F '.' '{print $2}')"
    __verbose "Detected macOS version $macos_version"
    if [ "$minor_version" -lt 12 ]; then
        __error ".NET Core 2.0 requires macOS 10.12 or newer. Current version is $macos_version."
        return 1
    fi
}

__get_dotnet_sdk_version() {
    local src="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

    version=$(< "$src/../../../global.json" head -1 | grep -Po '"version":.*?[^\\]",' global.json)
    # environment override
    if [ ! -z "${KOREBUILD_DOTNET_VERSION:-}" ]; then
        version=${KOREBUILD_DOTNET_VERSION:-}
        __warn "Dotnet SDK version changed by KOREBUILD_DOTNET_VERSION"
    fi
    echo $version
}

__fetch() {
    local remote_path=$1
    local local_path=$2

    if [[ "$remote_path" != 'http'* ]]; then
        __verbose "fetching local file $remote_path"
        cp "$remote_path" "$local_path"
        return 0
    fi

    if ! __machine_has 'curl' && ! __machine_has 'wget'; then
        __error 'wget or curl is required to download assets'
        return 1
    fi

    __verbose "Downloading $remote_path"

    local failed=false
    if __machine_has 'wget'; then
        # Try wget first as this has been more reliable than curl.
        # Travis CI frequently has TLS issues with curl on macOS
        # Only show progress bar if shell is interactive
        progress_bar='--quiet'
        [ "$__is_verbose" = true ] && [ -z "${PS1:-}" ] && progress_bar='--progress=bar --show-progress'
        __exec wget $progress_bar --tries 10 -O "$local_path" "$remote_path" || failed=true
    else
        failed=true
    fi

    if [ "$failed" = true ] && __machine_has 'curl'; then
        failed=false
        progress_bar='-s'
        [ "$__is_verbose" = true ] && [ -z "${PS1:-}" ] && progress_bar='-#'
        __exec curl --retry 10 $progress_bar -SL -f --create-dirs -o "$local_path" "$remote_path" || failed=true
    fi

    if [ "$failed" = true ]; then
        __error "Download failed: $remote_path"
        return 1
    fi
}

__get_korebuild_version() {
    local src="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
    local version_file="$src/../.version"
    local korebuild_version=''

    if [ -f "$version_file" ]; then
        korebuild_version="$(grep 'version:*' -m 1 "$version_file")"
        if [[ "$korebuild_version" == '' ]]; then
            echo -e "${GRAY}Failed to parse version from $version_file. Expected a line that begins with 'version:'${RESET}" 1>&2
        else
            korebuild_version="$(echo "$korebuild_version" | sed -e 's/^[[:space:]]*version:[[:space:]]*//' -e 's/[[:space:]]*$//')"
        fi
    fi

    echo $korebuild_version
}
