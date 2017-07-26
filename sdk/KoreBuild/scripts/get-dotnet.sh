#!/usr/bin/env bash

__script_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# flow failures thru piped commands, disallow unrecognized variables, and exit on first failure
set -euo pipefail

source "$__script_dir/common.sh"

__install_shared_runtime() {
    local install_dir=$1
    local version=$2
    local channel=$3

    local runtime_path="$install_dir/shared/Microsoft.NETCore.App/$version"
    if [ ! -d "$runtime_path" ]; then

        __verbose "Installing .NET Core runtime to $runtime_path"

        "$__script_dir/dotnet-install.sh" \
            --install-dir "$install_dir" \
            --architecture x64 \
            --shared-runtime \
            --channel "$channel" \
            --version "$version" \
            $verbose_flag

        return $?
    else
        echo -e "${GRAY}.NET Core runtime $version is already installed. Skipping installation.${RESET}"
    fi
}

#
# Main
#

verbose_flag=''
if [ "$1" = "--verbose" ]; then
    __is_verbose=true
    verbose_flag='--verbose'
    shift
fi

install_dir=$1

if [ ! -z "${KOREBUILD_SKIP_RUNTIME_INSTALL:-}" ]; then
     echo "Skipping runtime installation because KOREBUILD_SKIP_RUNTIME_INSTALL is set"
     exit 0
fi

channel='preview'
version=$(__get_dotnet_sdk_version)
runtime_channel='master'
runtime_version=$(< "$__script_dir/../config/runtime.version" head -1 | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//')

# environment overrides
[ ! -z "${KOREBUILD_DOTNET_CHANNEL:-}" ] && channel=${KOREBUILD_DOTNET_CHANNEL:-}
[ ! -z "${KOREBUILD_DOTNET_SHARED_RUNTIME_CHANNEL:-}" ] && runtime_channel=${KOREBUILD_DOTNET_SHARED_RUNTIME_CHANNEL:-}
[ ! -z "${KOREBUILD_DOTNET_SHARED_RUNTIME_VERSION:-}" ] && runtime_version=${KOREBUILD_DOTNET_SHARED_RUNTIME_VERSION:-}

chmod +x "$__script_dir/dotnet-install.sh"
# Temporarily install these runtimes to prevent build breaks for repos not yet converted
# 1.0.5 - for tools
__install_shared_runtime "$install_dir" "1.0.5" "preview"
# 1.1.2 - for test projects which haven't yet been converted to netcoreapp2.0
__install_shared_runtime "$install_dir" "1.1.2" "release/1.1.0"

if [ "$runtime_version" != "" ]; then
    __install_shared_runtime "$install_dir" "$runtime_version" "$runtime_channel"
fi

__verbose "Installing .NET Core SDK $version"

if [ ! -f "$install_dir/sdk/$version/dotnet.dll" ]; then
    "$__script_dir/dotnet-install.sh" \
        --install-dir "$install_dir" \
        --architecture x64 \
        --channel "$channel" \
        --version "$version" \
        $verbose_flag
else
    echo -e "${GRAY}.NET Core SDK $version is already installed. Skipping installation.${RESET}"
fi
