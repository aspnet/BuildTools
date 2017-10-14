#!/usr/bin/env bash

__script_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# flow failures thru piped commands, disallow unrecognized variables, and exit on first failure
set -euo pipefail

source "$__script_dir/common.sh"

dotnet_feed_cdn='https://dotnetcli.azureedge.net/dotnet'
dotnet_feed_uncached='https://dotnetcli.blob.core.windows.net/dotnet'
dotnet_feed_credential=''
[ ! -z "${KOREBUILD_DOTNET_FEED_CDN:-}" ] && dotnet_feed_cdn=${KOREBUILD_DOTNET_FEED_CDN:-}
[ ! -z "${KOREBUILD_DOTNET_FEED_UNCACHED:-}" ] && dotnet_feed_uncached=${KOREBUILD_DOTNET_FEED_UNCACHED:-}
[ ! -z "${KOREBUILD_DOTNET_FEED_CREDENTIAL:-}" ] && dotnet_feed_credential=${KOREBUILD_DOTNET_FEED_CREDENTIAL:-}

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
            --azure-feed "$dotnet_feed_cdn" \
            --uncached-feed "$dotnet_feed_uncached" \
            --feed-credential "$dotnet_feed_credential" \
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

if [ ! -z "${DOTNET_INSTALL_DIR:-}" ] && [ "${DOTNET_INSTALL_DIR:-}" != "$install_dir" ]; then
    __verbose "install_dir = $install_dir"
    __verbose "DOTNET_INSTALL_DIR = $DOTNET_INSTALL_DIR"
    __warn 'The environment variable DOTNET_INSTALL_DIR is deprecated. The recommended alternative is DOTNET_HOME.'
fi

dotnet_in_path="$(which dotnet 2>/dev/null || true )"
# The '-ef' condition tests if files are the same inode. This avoids showing the warning if users symlink dotnet into path
if [ ! -z "$dotnet_in_path" ] && [ ! "$dotnet_in_path" -ef "$install_dir/dotnet" ]; then
    __warn "dotnet found on the system PATH is '$dotnet_in_path' but KoreBuild will use '$install_dir/dotnet'."
    __warn "Adding '$install_dir' to system PATH permanently may be required for applications like Visual Studio for Mac or VS Code to work correctly."
fi

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
        --azure-feed "$dotnet_feed_cdn" \
        --uncached-feed "$dotnet_feed_uncached" \
        --feed-credential "$dotnet_feed_credential" \
        $verbose_flag
else
    echo -e "${GRAY}.NET Core SDK $version is already installed. Skipping installation.${RESET}"
fi
