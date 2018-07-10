PackageReference management
---------------------------

## Usage

KoreBuild includes tools to help you automatically update your `dependencies.props` files.

#### Generating a dependencies.props file

On an existing project, you can execute the following command:
```
run.ps1 generate deps
```

This will update csproj files and overwrite your build/dependencies.props file with variables.

#### Updating dependencies.props

KoreBuild can help you automatically update the `build/dependencies.props` file in your repo by using a lineup package.

On command line, you can then execute
```
run.ps1 upgrade deps
```

This command requires you set a few properties so the command can download a remote package and use that as the source
of version information. Most aspnetcore repos will set this in `build/repo.props`

```xml
<PropertyGroup>
  <LineupPackageId>Internal.AspNetCore.Universe.Lineup</LineupPackageId>
  <!-- Optional, and can float -->
  <LineupPackageVersion>2.1.0-*</LineupPackageVersion>
  <LineupPackageRestoreSource>https://dotnet.myget.org/F/aspnetcore-dev/api/v3/index.json</LineupPackageRestoreSource>
</PropertyGroup>
```

The lineup package itself contains a file that lists all version, and is itself also packaged under `build/dependencies.props`. The `upgrade deps` command will update any matching variables from the lineup package in the local copy of build/dependencies.props.

## Restrictions on PackageReference usage

To manage the complexity of keeping PackageReference versions consistent within a repo and between multiple repos, KoreBuild will enforce the following patterns for using PackageReference.

#### 1. build/dependencies.props

Each repository should have this file, and it should look like this.

```xml
<Project>
  <PropertyGroup>
    <MSBuildAllProjects>$(MSBuildAllProjects);$(MSBuildThisFileFullPath)</MSBuildAllProjects>
  </PropertyGroup>
  <PropertyGroup Label="Package Versions: Auto">
    <NewtonsoftJsonPackageVersion>10.0.1</NewtonsoftJsonPackageVersion>
    <MicrosoftNETTestSdkPackageVersion>15.3.0</MicrosoftNETTestSdkPackageVersion>
    <MoqPackageVersion>4.7.49</MoqPackageVersion>
    <XunitPackageVersion>2.3.0</XunitPackageVersion>
  </PropertyGroup>
  <Import Project="$(DotNetPackageVersionPropsPath)" Condition=" '$(DotNetPackageVersionPropsPath)' != '' " />
  <PropertyGroup Label="Package Versions: Pinned">
    <StablePackageVersion>10.0.1</StablePackageVersion>
  </PropertyGroup>
</Project>
```

The `<PropertyGroup Label="Package Versions: Auto">` section is for variables which should be automatically updated.

The `<PropertyGroup Label="Package Versions: Pinned">` section is for variables which upgrade automation should not touch.

### 2. PackageReference's should use variables to set versions

All .csproj files should set the version of a package reference like this:

```xml
<ItemGroup>
  <PackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
</ItemGroup>
```

#### Opt-out of restrictions

To opt-out of these restrictions, projects should add this to the `build/repo.props` file in their repository.
```xml
<PropertyGroup>
  <DisablePackageReferenceRestrictions>true</DisablePackageReferenceRestrictions>
</PropertyGroup>
```
