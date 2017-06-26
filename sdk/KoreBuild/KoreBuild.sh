#!/usr/bin/env bash

[ -z "${verbose:-}" ] && verbose=false
__korebuild_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

invoke_repository_build() {
    local repo_path=$1
    shift
    verbose_flag=''
    [ "$verbose" = true ] && verbose_flag='--verbose'

    chmod +x "$__korebuild_dir/scripts/invoke-repository-build.sh"
    "$__korebuild_dir/scripts/invoke-repository-build.sh" $repo_path $verbose_flag $@
    return $?
}

install_tools() {
    local tools_source=$1
    local install_dir=$2
    local tools_home="$install_dir/buildtools"
    local netfx_version='4.6.1'

    # Set environment variables
    export ReferenceAssemblyRoot="$tools_home/netfx/$netfx_version"
    export PATH="$install_dir:$PATH"

    verbose_flag=''
    [ "$verbose" = true ] && verbose_flag='--verbose'

    chmod +x "$__korebuild_dir/scripts/get-netfx.sh"
    "$__korebuild_dir/scripts/get-netfx.sh" $verbose_flag $netfx_version "$tools_source" "$ReferenceAssemblyRoot"
    [ $? -eq 0 ] || return $?

    chmod +x "$__korebuild_dir/scripts/get-dotnet.sh"
    "$__korebuild_dir/scripts/get-dotnet.sh" $verbose_flag "$install_dir"
    return $?
}
