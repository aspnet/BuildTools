Microsoft.DotNet.GlobalTools.Sdk
================================

Provides additional support to .NET Core teams producing global CLI tools. This package is only intended for internal Microsoft use.

## Usage
Projects that need to bundle and sign the global CLI tool shim should add this to their .csproj file. This will include files in the .nupkg.

```xml
<!-- In MyTool.csproj -->
<Project>
  <Sdk Name="Microsoft.NET.Sdk" />
  <Sdk Name="Microsoft.DotNet.GlobalTools.Sdk" />

  <PropertyGroup>
    <PackAsTool>true</PackAsTool>
    <GenerateToolShims>true</GenerateToolShims>
  </PropertyGroup>
</Project>
```

```js
// in global.json
{
    "msbuild-sdks": {
        "Microsoft.DotNet.GlobalTools.Sdk": "2.1.0-rtm-12345"
    }
}
```

### Additional options

#### `GenerateToolShims` (property)

A boolean flag. When `true`, tool shims will be generated for each RID listed in `GeneratedShimRuntimeIdentifiers` and included in the .nupkg.

Default value = `false`

#### `GeneratedShimRuntimeIdentifiers` (property)

A semi-colon separate list of RIDs for which to generate and pack the shim.

Default value = `win-x86;win-x64`
