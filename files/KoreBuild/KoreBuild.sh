#!/usr/bin/env bash

[ -z "${verbose:-}" ] && verbose=false
__korebuild_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
source "$__korebuild_dir/scripts/common.sh"

# functions
default_tools_source='https://aspnetcore.blob.core.windows.net/buildtools'

set_korebuildsettings() {
    tools_source=$1
    dotnet_home=$2
    repo_path=$3
    local config_file="${4:-}" # optional. Not used yet.

    [ -z "${dot_net_home:-}" ] && dot_net_home="$HOME/.dotnet"
    [ -z "${tools_source:-}" ] && tools_source="$default_tools_source"

    return 0
}

invoke_korebuild_command(){
    local command="${1:-}"
    shift

    if [ "$command" = "default-build" ]; then
        install_tools "$tools_source" "$dot_net_home"
        invoke_repository_build "$repo_path" "$@"
    elif [ "$command" = "msbuild" ]; then
        invoke_repository_build "$repo_path" "$@"
    elif [ "$command" = "install-tools" ]; then
        install_tools "$tools_source" "$dot_net_home"
    else
        ensure_dotnet

        kore_build_console_dll="$__korebuild_dir/tools/KoreBuild.Console.dll"

        __exec dotnet "$kore_build_console_dll" "$command" \
            --tools-source "$tools_source" \
            --dotnet-home "$dot_net_home" \
            --repo-path "$repo_path" \
            "$@"
    fi
}

ensure_dotnet() {
    if ! __machine_has dotnet; then
        install_tools
    fi
}

invoke_repository_build() {
    local repo_path=$1
    shift
    verbose_flag=''
    [ "$verbose" = true ] && verbose_flag='--verbose'

    # Call "sync" between "chmod" and execution to prevent "text file busy" error in Docker (aufs)
    chmod +x "$__korebuild_dir/scripts/invoke-repository-build.sh"; sync
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

    # Call "sync" between "chmod" and execution to prevent "text file busy" error in Docker (aufs)
    chmod +x "$__korebuild_dir/scripts/get-netfx.sh"; sync
    # we don't include netfx in the BuildTools artifacts currently, it ends up on the blob store through other means, so we'll only look for it in the default_tools_source
    "$__korebuild_dir/scripts/get-netfx.sh" $verbose_flag $netfx_version "$default_tools_source" "$ReferenceAssemblyRoot" \
        || return 1

    # Call "sync" between "chmod" and execution to prevent "text file busy" error in Docker (aufs)
    chmod +x "$__korebuild_dir/scripts/get-dotnet.sh"; sync
    "$__korebuild_dir/scripts/get-dotnet.sh" $verbose_flag "$install_dir" \
        || return 1

    # Set environment variables
    export PATH="$install_dir:$PATH"
}

__show_version_info() {
    MAGENTA="\033[0;95m"
    RESET="\033[0m"
    version_file="$__korebuild_dir/.version"
    if [ -f "$version_file" ]; then
        __korebuild_version="$(grep 'version:*' -m 1 "$version_file")"
        if [[ "$__korebuild_version" == '' ]]; then
            echo "Failed to parse version from $version_file. Expected a line that begins with 'version:'" 1>&2
        else
            __korebuild_version="$(echo "$__korebuild_version" | sed -e 's/^[[:space:]]*version:[[:space:]]*//' -e 's/[[:space:]]*$//')"
            echo -e "${MAGENTA}Using KoreBuild ${__korebuild_version}${RESET}"
        fi
    fi
}

# Try to show version on console, but don't fail if this is broken
__show_version_info || true

if [ "$(uname)" = "Darwin" ]; then
    __ensure_macos_version
    # increase file descriptor limit
    ulimit -n 5000
fi
