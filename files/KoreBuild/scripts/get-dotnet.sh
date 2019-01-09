#!/usr/bin/env bash

__script_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# flow failures thru piped commands, disallow unrecognized variables, and exit on first failure
set -euo pipefail

source "$__script_dir/common.sh"

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
     __warn "Skipping runtime installation because KOREBUILD_SKIP_RUNTIME_INSTALL is set"
     exit 0
fi

# Call "sync" between "chmod" and execution to prevent "text file busy" error in Docker (aufs)
chmod +x "$__script_dir/dotnet-install.sh"; sync

version=$(__get_dotnet_sdk_version)
__verbose "Installing .NET Core SDK $version"

if [ ! -f "$install_dir/sdk/$version/dotnet.dll" ]; then
    "$__script_dir/dotnet-install.sh" \
        --install-dir "$install_dir" \
        --architecture x64 \
        --version "$version" \
        $verbose_flag
else
    echo -e "${GRAY}.NET Core SDK $version is already installed. Skipping installation.${RESET}"
fi
