# BuildTools.Tasks

This contains general purpose MSBuild tasks.

## Tasks

### GetGitCommitInfo
Gets information about a git repository.

Usage
```
# Required
[string]
WorkingDirectory = A folder inside or containing a git project.
```

```xml
<GetGitCommitInfo WorkingDirectory="C:\dev\myRepo\src\mycode\folder\">
  <Output TaskParameter="Branch" PropertyName="Branch" />
  <Output TaskParameter="CommitHash" PropertyName="CommitHash" />
  <Output TaskParameter="RepositoryRootPath" PropertyName="RepositoryRootPath" />
</GetGitCommitInfo>

<!-- 
$(Branch) = master (or empty string if not on a branch)
$(CommitHash) = xxxxxxxx (git sha)
$(RepositoryRootPath) = C:\dev\myRepo\ (the folder contain .git, not the .git folder)
-->
```

### GetDotNetHost
*Only available for MSBuild for .NET Core*

Gets the dotnet.exe file used to launch MSBuild.

Usage
```xml
<GetDotNetHost>
  <Output TaskParameter="ExecutablePath" PropertyName="DotNetExe" />
  <Output TaskParameter="DotNetDirectory" PropertyName="DotNetHome" />
</GetDotNetHost>

<!-- 
$(DotNetExe) = C:\Program Files\dotnet\dotnet.exe
$(DotNetHome) = C:\Program Files\dotnet\
-->
```

### GetOSPlatform
Gets the name of the operating system.

Usage:
```
# Output
[string]
PlatformName = name of OS. Windows, Linux, or macOS
```

Example
```xml
<GetOsPlatform>
  <Output TaskParameter="PlatformName" PropertyName="PlatformName" />
</GetDotNetHost>
```

### Run

A friendlier Exec task.

Examples:
```xml
<Run FileName="git" Command="commit -m 'Message'" />

<ItemGroup>
  <GitArgs Include="add" />
  <GitArgs Include="**\*.cs" />
</ItemGroup>
<Run FileName="git" Arguments="@(GitArgs)" />

<ItemGroup>
 <EnvVar Include="USERNAME=user" />
 <EnvVar Include="API_KEY">
   <Value>$(SecretKey)</Value>
 </EnvVar>
</ItemGroup>

<Run FileName="./encrypt-secrets.sh" EnvironmentVariables="@(EnvVar)"/>
```

Arguments:
```
# Required
[string]
FileName = the executable path or name of an executable on the PATH

# Optional

[string]
Command* = a string containing the arguments.
    For easiest use

[items]
Arguments* = an item group of arguments to be contatenated and escaped. 
    Best when arguments may contain a space

(*Command or Arguments can be used, but not both.)

[items]
EnvironmentVariables = an item group of variables to set on the process
    Item spec will be split on '='. If '=' is not present, it will also look for a metadata value named 'Value'

[string]
WorkingDirectory = the directory in which to run

[bool]
IgnoreExitCode = do not fail if exit code is non-zero. Defaults to false

[int]
MaxRetries = repeat the command several times if the process exits with a non-zero code. Defaults to 0.

[bool]
UseShellExecute = use shell to execute the process. Defaults to true on Linux and macOS, false on Windows.

# Output
[int]
ExitCode = the exit code of the process
```

### RunDotNet

Has the same arguments as [Run](#Run) except FileName, which defaults to the "dotnet.exe" muxer.
```xml
<RunDotNet Command="build -c Release" />
<RunDotNet Command="restore" />

<ItemGroup>
 <Packages Include="*.nupkg" />
</ItemGroup>
<RunDotNet Command="nuget push %(Packages.Identity)" MaxRetries="3" />
```

### SetEnvironmentVariable

Sets an environment variable in the current process.

```xml
<SetEnvironmentVariable Variable="MY_USER_NAME" Value="Steve" />
```

### ZipArchive
Produces a .zip file

Usage
```
# Required
[string]
File = the name of the file to produce

[items]
SourceFiles = the fiels to be added to the file

    Metadata: Link = can be used to override the entry path inside the zip archive

[string]
WorkingDirectory = the directory to use as the base directory. The entry path
   for each item in SourceFiles is relative to this.

# Optional
[bool]
Overwrite = overwrite the file if it exists
```


```xml
```

### UnzipArchive
Unzips an archive file.

Usage
```
# Required
[string]
File = The file to unzip.

[string]
Destination = The directory where files will be unzipped.

# Output
[items]
OutputFiles = The files that were unzipped.
```
