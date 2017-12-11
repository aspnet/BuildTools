#!/usr/bin/env bash

[ -z "${verbose:-}" ] && verbose=false
__korebuild_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
source "$__korebuild_dir/scripts/common.sh"

# functions

invoke_repository_build() {
    local repo_path=$1
    shift
    verbose_flag=''
    [ "$verbose" = true ] && verbose_flag='--verbose'

    chmod +x "$__korebuild_dir/scripts/invoke-repository-build.sh"
    "$__korebuild_dir/scripts/invoke-repository-build.sh" "$repo_path" $verbose_flag "$@"
    return $?
}

install_tools() {
    local tools_source=$1
    local install_dir=$2
    local tools_home="$install_dir/buildtools"
    local netfx_version='4.6.1'

    verbose_flag=''
    [ "$verbose" = true ] && verbose_flag='--verbose'

    # Instructs MSBuild where to find .NET Framework reference assemblies
    export ReferenceAssemblyRoot="$tools_home/netfx/$netfx_version"
    chmod +x "$__korebuild_dir/scripts/get-netfx.sh"
    "$__korebuild_dir/scripts/get-netfx.sh" $verbose_flag $netfx_version "$tools_source" "$ReferenceAssemblyRoot" \
        || return 1

    chmod +x "$__korebuild_dir/scripts/get-dotnet.sh"
    "$__korebuild_dir/scripts/get-dotnet.sh" $verbose_flag "$install_dir" \
        || return 1

    # Set environment variables
    export PATH="$install_dir:$PATH"
}

__show_version_info() {
    MAGENTA="\033[0;95m"
    RESET="\033[0m"

    __korebuild_version="$(__get_korebuild_version)"
    if [[ "$__korebuild_version" != '' ]]; then
        echo -e "${MAGENTA}Using KoreBuild ${__korebuild_version}${RESET}"
    fi
}

# Try to show version on console, but don't fail if this is broken
__show_version_info || true

if [ "$(uname)" = "Darwin" ]; then
    __ensure_macos_version
    # increase file descriptor limit
    ulimit -n 5000
fi

# Set required environment variables

# This disables automatic rollforward to /usr/local/dotnet and other global locations.
# We want to ensure are tests are running against the exact runtime specified by the project.
export DOTNET_MULTILEVEL_LOOKUP=0
