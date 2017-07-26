#!/usr/bin/env bash

__script_dir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# flow failures thru piped commands, disallow unrecognized variables, and exit on first failure
set -euo pipefail

source "$__script_dir/common.sh"

if ! __machine_has 'tar'; then
    __error 'Required command not available: tar'
    exit 1
fi

if [ "${1:-}" = "--verbose" ]; then
    __is_verbose=true
    shift
fi

netfx_version=$1
tools_source=$2
install_dir=$3
remote_path="$tools_source/netfx/$netfx_version/netfx.$netfx_version.tar.gz"

if [ -d "$install_dir" ]; then
    echo -e "${GRAY}Using cached .NET Framework reference assemblies from ${install_dir}${RESET}"
    exit 0
fi

tmpfile="$(mktemp)"
tmpdir="$(mktemp -d)"
rm "$tmpfile" >/dev/null 2>&1 || :
echo "Downloading .NET Framework reference assemblies"
__fetch "$remote_path" "$tmpfile"
mkdir -p "$tmpdir"
tar -C "$tmpdir" -xzf "$tmpfile"
rm "$tmpfile" || :

mkdir -p "$(dirname "$install_dir")"
if [ ! -d "$install_dir" ]; then
    echo "Extracting .NET Framework reference assemblies to $install_dir"
    mv "$tmpdir" "$install_dir"
else
    __verbose 'Not copying into place. Looks like someone else already beat us to it.'
    rm -rf "$tmpdir" || :
fi
