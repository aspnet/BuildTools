KoreBuild
---------

KoreBuild is a set of MSBuild targets, tasks, and console commands used to define a common set of build and test tasks.

## Usage

### KoreBuild commands

KoreBuild commands allow you to take wider and more granular actions.
Previously repositories were runable in only one way, by doing `.\build.cmd`. But now, you can run multiple options.

Command      | Purpose                                                          | Example
-------------|------------------------------------------------------------------|----------
install-tools| Installs dotnet, CLI and Shared runtimes.                        | .\run.ps1 install-tools
docker-build | Runs the build inside docker.                                    | .\run.ps1 docker-build {jessie\|winservercore} /t:SomeTarget /p:Parameters
default-build| Runs install-tools followed by msbuild (like build.cmd used to). | .\run.ps1 default-build /t:SomeTarget /p:Parameters
msbuild      | Runs the build normally.                                         | .\run.ps1 msbuild /t:SomeTarget /p:Parameters
upgrade deps | Upgrade the dependencies.props of this project.                  | .\run.ps1 upgrade deps
generate deps| Generate a dependencies.props for this project.                  | .\run.ps1 generate deps

### KoreBuild config

KoreBuild can be configured by adding a 'korebuild.json' file into the root folder of your repository.

Example:
```js
// NB: Don't actually use comments in JSON files. PowerShell's ConvertFrom-Json will throw an error.

{
  // add this for editor auto-completion :)
  "$schema": "https://raw.githubusercontent.com/aspnet/BuildTools/dev/tools/korebuild.schema.json",

  // specifies the channel used to update KoreBuild to new versions when you attempt to upgrade KoreBuild
  "channel": "dev",

  "toolsets": {
      // All toolsets listed in this section are treated as required toolsets

      "visualstudio": {
          // defaults to `true`
          "includePrerelease": false,

          // see https://aka.ms/vs/workloads
          "requiredWorkloads": [
            "Microsoft.VisualStudio.Component.VSSDK"
          ],

          // Default = no minimum version
          "minVersion": "15.4",

          // This tool is only required on Windows.
          "required": [ "windows" ]
      },

      "nodejs": {
        "required": true,
        "minVersion": "8.0"
      }
  }
}
```

## MSBuild

The `msbuild` command runs an MSBuild process that executes a series of targets against the entire repository. The entry point for this MSBuild process is defined in [KoreBuild.proj][korebuild-proj].

### KoreBuild Lifecycle

The KoreBuild lifecycle chains together these targets in this order. Custom targets can chain off these.

1. Prepare - pre-build actions like making directories
1. Restore - NuGet restore
1. Compile - calls /t:Build on \*.sln files
1. Package - NuGet pack and other packaging steps
1. Test - invokes VSTest
1. Verify - post build tests

When not specified, the default target is `/t:Build`, which runs all of these lifecycle targets.

### Other common targets

These targets are also available, but are not run in the default lifecycle.

- Clean - cleans artifacts, executes /t:Clean on solutions
- Rebuild - executes /t:Rebuild on solutions
- Resx - generates resx files
- Noop - a target that does nothing
- Publish - pushes artifacts to NuGet feeds and blob stores


### Extensibility points

#### Modules

KoreBuild is designed to be a modular system. It is written as a backbone of default lifecycle targets and imports.
Default functionality, such as building solutions and testing with VSTest, are written as modules in `files/KoreBuild/modules`.

Other default functionality that require tasks are built from `modules/` in this repo.
These include tasks for downloading NuGet packages, creating Zip files, retrieving Git information, and more.

Additional KoreBuild modules can be imported by setting `CustomKoreBuildModulesPath` as a property or environment variable.
Anything matching `$(CustomKoreBuildModulesPath)/*/module.props` and `$(CustomKoreBuildModulesPath)/*/modules.targets` will be imported into KoreBuild.

#### RepoTasks

RepoTasks is a C# project that can be used to define MSBuild tasks that apply only to a specific repository.

```
build/tasks/RepoTasks.csproj
```

If this file exists, KoreBuild will restore and publish it to build/tasks/bin/publish/

```
build/tasks/RepoTasks.tasks
```

If this file exists, KoreBuild will import it.

Sample contents:
```xml
<!-- build/tasks/RepoTasks.tasks -->

<Project>
  <UsingTask TaskName="RepoTasks.MyCustomTask" AssemblyFile="$(MSBuildThisFileDirectory)bin\publish\RepoTasks.dll" />
</Project>
```


#### repo.props
```
<root>/build/repo.props
```

If this file exists, it is imported shortly after [KoreBuild.Common.props][../files/KoreBuild/KoreBuild.Common.props].

>**Best practices**: Only define properties and settings in \*.props files.

#### repo.targets

```
<root>/build/repo.targets
```

If this file exists, it is imported shortly after [KoreBuild.Common.targets][../files/KoreBuild/KoreBuild.Common.targets].

>**Best practices**: Define custom build steps in \*.targets files.


#### version.props

```
<root>/version.props
```

If this file exists, it is imported shortly after [KoreBuild.Common.props][../files/KoreBuild/KoreBuild.Common.props]. It should contain settings like VerisonPrefix, PackageVersion, VerisonSuffix, and others.

These values can be used to ensure that all NuGet packages produced have the same version.

>**Best practices**: This file should contain all settings related to asset versions.

[korebuild-proj]: ../files/KoreBuild/KoreBuild.proj
