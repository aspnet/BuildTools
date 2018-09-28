Signing
=======

KoreBuild supports code signing files and using MSBuild to configure the list of files which are code-signed.

## Config via csproj

KoreBuild can generate the sign request using information from MSBuild projects. The following options can be set.

### Assemblies

To sign assemblies, set the AssemblySigningCertName and AssemblySigningStrongName property in the \*.csproj.

```xml
<PropertyGroup>
  <AssemblySigningCertName>MyCert</AssemblySigningCertName>
  <AssemblySigningStrongName>PrivateStrongName</AssemblySigningStrongName>
</PropertyGroup>
```

### NuGet packages

To sign NuGet packages, set the PackageSigningCertName property in the \*.csproj that produces the nupkg.

```xml
<PropertyGroup>
  <PackageSigningCertName>MyNuGetCert</PackageSigningCertName>
</PropertyGroup>
```

### NuGet packages with assemblies

For assemblies that ship in a NuGet package, you can specify multiple properties.

```xml
<PropertyGroup>
  <AssemblySigningCertName>MyCert</AssemblySigningCertName>
  <PackageSigningCertName>MyNuGetCert</PackageSigningCertName>
</PropertyGroup>
```

### Recommended cert names for Microsoft projects

The following certificate names should be used for Microsoft projects. These MSBuild properties are also available by using Internal.AspNetCore.SDK.

```xml
    <AssemblySigningCertName>Microsoft400</AssemblySigningCertName>
    <AssemblySigning3rdPartyCertName>3PartySHA2</AssemblySigning3rdPartyCertName>
    <PowerShellSigningCertName>Microsoft400</PowerShellSigningCertName>
    <PackageSigningCertName>NuGet</PackageSigningCertName>
    <VsixSigningCertName>VsixSHA2</VsixSigningCertName>
    <JarSigningCertName>MicrosoftJAR</JarSigningCertName>
```

### Projects using nuspec

When creating a NuGet package via nuspec + csproj, KoreBuild cannot detect which assemblies
end up in the nuget package. You must explicitly declare which assemblies inside the nupkg
should be signed.

```xml
<PropertyGroup>
  <NuspecFile>MyPackage.nuspec<NuspecFile/>
</PropertyGroup>

<ItemGroup>
  <!-- TargetFileName is a well-known MSBuild property that is set to MyPackage.dll -->
  <SignedPackageFile Include="$(TargetPath)" PackagePath="tools/$(TargetFileName)" Visible="false" />
</ItemGroup>
```

### NuGet packages with signable files

Sometimes other signable assemblies end up in a nupkg. Signing for these file types can be controlled with `SignedPackageFile`, and `ExcludePackageFileFromSigning` items.

```xml
  <ItemGroup>
    <!-- Specifying signing for a file in a package. -->
    <SignedPackageFile Include="tools/Microsoft.Extensions.Configuration.Abstractions.dll" Certificate="$(AssemblySigningCertName)" Visible="false" />

    <!-- Specifying signing for a file in a package using an explicit path within the NuGet package. -->
    <SignedPackageFile Include="$(OutputPath)$(TargetFileName)" Certificate="$(AssemblySigningCertName)"
      PackagePath="tasks/net461/$(TargetFileName)" Visible="false" />

    <!-- Third-party cert -->
    <SignedPackageFile Include="tools/Newtonsoft.Json.dll" Certificate="3PartySHA2" Visible="false" />

    <!-- This should already be signed by the dotnet-core team -->
    <ExcludePackageFileFromSigning Include="tools/System.Runtime.CompilerServices.Unsafe.dll" />
  </ItemGroup>
```

### Disabling signing

You can disable sign request generation on an MSBuild project by setting DisableCodeSigning, or for an entire repo (via repo.props).

```xml
<PropertyGroup>
  <DisableCodeSigning>true</DisableCodeSigning>
</PropertyGroup>
```

## Additional signing files

KoreBuild targets may produce additional artifacts that should be signed by methods not detected from MSBuild project files. These files can be added to the sign request by adding
these elements to the `build/repo.props` file. (See also [KoreBuild.md](./KoreBuild.md#repo-props))

```xml
<!-- build/repo.props -->
<ItemGroup>
  <FilesToSign Include="$(ArtifactsDir)libuv.dll" Certificate="3PartySHA2" />

  <!-- Files can also be listed as "do not sign", for completeness -->
  <FilesToExcludeFromSigning Include="$(ArtifactsDir)my.test.dll" Certificate="3PartySHA2" />
</ItemGroup>
```
