KoreBuild
=========

KoreBuild is a set of MSBuild targets and tasks used to define a common set of build and test tasks. The entry point for KoreBuild is defined in [KoreBuild.proj][korebuild-proj].

## KoreBuild Lifecycle

The KoreBuild chains together these targets in this order. Custom targets can chain off these.

1. Prepare - pre-build actions like making directories
1. Restore - NuGet restore
1. Compile - calls /t:Build on \*.sln files
1. Package - NuGet pack and other packaging steps
1. Test - invokes VSTest
1. Verify - post build tests

When not specified, the default target is `/t:Build`, which runs all of these lifecycle targets.

## Other common targets

These targets are also available, but are not run in the default lifecycle.

- Clean - cleans artifacts, executes /t:Clean on solutions
- Rebuild - executes /t:Rebuild on solutions
- Resx - generates resx files
- Noop - a target that does nothing
- Publish - pushes artifacts to NuGet feeds and blob stores

## Extensibility points

### Modules

KoreBuild is designed to be a modular system. It is written as a backbone of default lifecycle targets and imports.
Default functionality, such as building solutions and testing with VSTest, are written as modules in `files/KoreBuild/modules`.

Other default functionality that require tasks are built from `modules/` in this repo.
These include tasks for downloading NuGet packages, creating Zip files, retrieving Git information, and more.

Additional KoreBuild modules can be imported by setting `CustomKoreBuildModulesPath` as a property or environment variable.
Anything matching `$(CustomKoreBuildModulesPath)/*/module.props` and `$(CustomKoreBuildModulesPath)/*/modules.targets` will be imported into KoreBuild.

### RepoTasks

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


### repo.props
```
<root>/build/repo.props
```

If this file exists, it is imported shortly after [KoreBuild.Common.props][../files/KoreBUild/KoreBuild.Common.props].

>**Best practies**: Only define properties and settings in \*.props files.

### repo.targets

```
<root>/build/repo.targets
```

If this file exists, it is imported shortly after [KoreBuild.Common.targets][../files/KoreBUild/KoreBuild.Common.targets].

>**Best practies**: Define custom build steps in \*.targets files.


### version.props

```
<root>/version.props
```

If this file exists, it is imported shortly after [KoreBuild.Common.props][../files/KoreBUild/KoreBuild.Common.props]. It should contain settings like VerisonPrefix, PackageVersion, VerisonSuffix, and others.

These values can be used to ensure that all NuGet packages produced have the same version.

>**Best practies**: This file should contain all settings related to asset versions.

[korebuild-proj]: ../files/KoreBuild/KoreBuild.proj
