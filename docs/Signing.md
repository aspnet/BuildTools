Signing
=======

KoreBuild supports generating a signing request manfiest. This includes a list of all files that should be signed
and information about the strongname or certificate that should be used.

## Format

The signing request manifest supports three element types. A minimal example looks like this. See [Elements](#Elements) below for details

```xml
<SigningRequest>
  <File Path="MyAssembly.dll" Certificate="MyCert" StrongName="MyStrongName" />
  <File Path="build/Another.dll" Certificate="MyCert" />
  <Container Path="MyLib.1.0.0.nupkg" Type="nupkg" Certificate="NuGetCert">
    <File Path="lib/netstandard2.0/MyLib.dll" Certificate="MyCert" />
  </Container>
  <Container Path="MyVSTool.vsix" Type="vsix" Certificate="VsixCert">
    <File Path="MyVSTool.dll" Certificate="MyCert" />
    <!-- excluded from signing, but useful if you want to assert all files in a container are accounted for. -->
    <ExcludedFile Path="NotMyLib.dll" />
  </Container>
</SigningRequest>
```

## Config

### Assemblies

To sign assemblies, set the AssemblySigningCertName and AssemblySigningStrongName property in the \*.csproj.

```xml
<PropertyGroup>
  <AssemblySigningCertName>MyCert</AssemblySigningCertName>
  <AssemblySigningStrongName>PrivateStrongName</AssemblySigningStrongName>
</PropertyGroup>
```

This will generate a signing request like this:

```xml
<SigningRequest>
  <File Path="MyLib.dll" Certificate="MyCert" StrongName="PrivateStrongName" />
</SigningRequest>
```

### NuGet packages

To sign NuGet packages, set the PackageSigningCertName property in the \*.csproj that produces the nupkg.

```xml
<PropertyGroup>
  <PackageSigningCertName>NuGetCert</PackageSigningCertName>
</PropertyGroup>
```

This will generate a signing request like this:

```xml
<SigningRequest>
  <Container Path="MyLib.1.0.0.nupkg" Type="nupkg" Certificate="NuGetCert" />
</SigningRequest>
```

### NuGet packages with assemblies

For assemblies that ship in a NuGet package, you can specify multiple properties.

```xml
<PropertyGroup>
  <AssemblySigningCertName>MyCert</AssemblySigningCertName>
  <PackageSigningCertName>NuGetCert</PackageSigningCertName>
</PropertyGroup>
```

This will generate a signing request like this:

```xml
<SigningRequest>
  <Container Path="MyLib.1.0.0.nupkg" Type="nupkg" Certificate="NuGetCert">
    <File Path="lib/netstandard2.0/MyLib.dll" Certificate="MyCert" />
  </Container>
</SigningRequest>
```


## Elements

#### `SigningRequest`

Root element. No options.

#### `File`

A file to be signed.

**Path** - file path, relative to the file path. If nested in a `<Container>`, is relative to the organization within the container

**Certificate** - the name of the certificate to use

**StrongName** - for assemblies only. This is used to strong name assemblies that were delay signed in public.

#### `Container`

A container is an archive file, installer, or some kind of bundle that can be signed, or that has files that can be signed
inside it. Nested elements can be added for `<File>` and `<ExcludedFile>`.

**Path** - file path to the container

**Certificate** - the name of the certificate to use

**Type** - The type of the container. Instructs the consumer how to extract the container. Example values:

  - zip
  - tar.gz
  - vsix
  - nupkg
  - msi

#### `ExcludedFile`

This is useful when you want to exclude files within a container from being signed, but want to assert that
all files in a container are accounted for.

**Path** - file path to a file to be ignored by the signing tool

