The Internal ASP.NET Core SDK
=============================

The internal ASP.NET Core SDK provides features essential to producing ASP.NET Core itself.
It is not indended for general consumption beyond the ASP.NET Core team.

## How to use it

1. Change the first line of your .csproj files to

```xml
<Project Sdk="Internal.AspNetCore.Sdk">
```

2. Add this to your NuGet.config
```xml
<configuration>
  <packageSources>
    <add key="myget.org aspnetcore-tools" value="https://dotnet.myget.org/F/aspnetcore-tools/api/v3/index.json" />
  </packageSources>
</configuration>
```

3. Add a global.json file to your project with contents like this:
```json
{
    "msbuild-sdks": {
        "Internal.AspNetCore.Sdk": "2.2.0-preview1-1234"
    }
}
```
