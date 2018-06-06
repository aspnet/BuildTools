// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using BuildTools.Tasks.Tests;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace KoreBuild.Tasks.Tests
{
    public class GenerateSignRequestTests
    {
        private readonly ITestOutputHelper _output;

        public GenerateSignRequestTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ItCreatesSignRequest()
        {
            var rootDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:/" : "/";
            var nupkgPath = Path.Combine(AppContext.BaseDirectory, "build", "MyLib.nupkg");
            var requests = new ITaskItem[]
            {
                new TaskItem(Path.Combine(AppContext.BaseDirectory, "build", "ZZApp.vsix"),
                    new Hashtable
                    {
                        ["IsContainer"] = "true",
                        ["Certificate"] = "Cert4",
                    }),
                new TaskItem(nupkgPath,
                    new Hashtable
                    {
                        ["IsContainer"] = "true",
                        ["Type"] = "nupkg",
                    }),
                new TaskItem(Path.Combine(AppContext.BaseDirectory, "MyLib.dll"),
                    new Hashtable
                    {
                        ["Container"] = nupkgPath,
                        ["PackagePath"] = "lib/netstandard2.0/MyLib.dll",
                        ["Certificate"] = "Cert1",
                        ["StrongName"] = "Key1",
                    }),
                new TaskItem(Path.Combine(AppContext.BaseDirectory, "MyRefLib.dll"),
                    new Hashtable
                    {
                        ["Container"] = nupkgPath,
                        ["PackagePath"] = "ref/netstandard2.0/",
                        ["Certificate"] = "Cert1",
                        ["StrongName"] = "Key1",
                    }),
                new TaskItem(Path.Combine(AppContext.BaseDirectory, "build", "MyLib.dll"),
                    new Hashtable
                    {
                        ["Certificate"] = "Cert1",
                    }),
                new MockTaskItem(Path.Combine(rootDir + "MyProject", "lib", "net461", "MyLib.dll"))
                {
                    ["Container"] = nupkgPath,
                    ["MSBuildSourceProjectFile"] = rootDir + "MyProject/MyProject.csproj",
                    ["Certificate"] = "Cert1",
                },
            };

            var exclusions = new ITaskItem[]
            {
                new TaskItem(Path.Combine(AppContext.BaseDirectory, "NotMyLib.dll"),
                    new Hashtable
                    {
                        ["PackagePath"] = "lib/NotMyLib.dll",
                        ["Container"] = nupkgPath,
                    }),
                new MockTaskItem(Path.Combine(rootDir + "MyProject", "tool", "net461", "NotMyLib.dll"))
                {
                    ["Container"] = nupkgPath,
                    ["MSBuildSourceProjectFile"] = rootDir + "MyProject/MyProject.csproj",
                },
            };

            var task = new GenerateSignRequest
            {
                Requests = requests,
                BasePath = AppContext.BaseDirectory,
                Exclusions = exclusions,
                BuildEngine = new MockEngine(_output),
            };

            var sb = new StringBuilder();

            Assert.True(task.Execute(() => new StringWriter(sb)), "Task should pass");

            var expected = $@"<SignRequest>
  <File Path=`build/MyLib.dll` Certificate=`Cert1` />
  <Nupkg Path=`build/MyLib.nupkg`>
    <ExcludedFile Path=`lib/NotMyLib.dll` />
    <File Path=`lib/net461/MyLib.dll` Certificate=`Cert1` />
    <File Path=`lib/netstandard2.0/MyLib.dll` Certificate=`Cert1` StrongName=`Key1` />
    <File Path=`ref/netstandard2.0/MyRefLib.dll` Certificate=`Cert1` StrongName=`Key1` />
    <ExcludedFile Path=`tool/net461/NotMyLib.dll` />
  </Nupkg>
  <Vsix Path=`build/ZZApp.vsix` Certificate=`Cert4` />
</SignRequest>".Replace('`', '"');
            _output.WriteLine(sb.ToString());

            Assert.Equal(expected, sb.ToString(), ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);
        }
    }
}
