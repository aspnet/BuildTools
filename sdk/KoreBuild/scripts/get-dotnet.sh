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

        __verbose "Installing .NET Core runtime $runtime_path"

        $__korebuild_dir/dotnet-install.sh \
            --install-dir $install_dir \
            --architecture x64 \
            --shared-runtime \
            --channel $channel \
            --version $version

        return $?
    fi
}

#
# Main
#

if [ "$1" = "--verbose" ]; then
    verbose=true
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
runtime_version=$(cat "$__korebuild_dir/../config/runtime.version" | head -1 | tr -d '[:space:]')

# environment overrides
[ ! -z "${KOREBUILD_DOTNET_CHANNEL:-}" ] && channel=${KOREBUILD_DOTNET_CHANNEL:-}
[ ! -z "${KOREBUILD_DOTNET_SHARED_RUNTIME_CHANNEL:-}" ] && runtime_channel=${KOREBUILD_DOTNET_SHARED_RUNTIME_CHANNEL:-}
[ ! -z "${KOREBUILD_DOTNET_SHARED_RUNTIME_VERSION:-}" ] && runtime_version=${KOREBUILD_DOTNET_SHARED_RUNTIME_VERSION:-}

chmod +x $__korebuild_dir/dotnet-install.sh
# Temporarily install these runtimes to prevent build breaks for repos not yet converted
# 1.0.5 - for tools
__install_shared_runtime $install_dir "1.0.5" "preview"
# 1.1.2 - for test projects which haven't yet been converted to netcoreapp2.0
__install_shared_runtime $install_dir "1.1.2" "release/1.1.0"

if [ "$runtime_version" != "" ]; then
    __install_shared_runtime $install_dir $runtime_version $runtime_channel
fi

__verbose "Installing .NET Core SDK $version"

$__korebuild_dir/dotnet-install.sh \
    --install-dir $install_dir \
    --architecture x64 \
    --channel $channel \
    --version $version
