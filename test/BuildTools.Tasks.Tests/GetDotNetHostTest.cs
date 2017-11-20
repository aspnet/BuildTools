// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore.BuildTools;
using Xunit;

namespace BuildTools.Tasks.Tests
{
    public class GetDotNetHostTest
    {
        [Fact]
        public void FindsMuxer()
        {
            var task = new GetDotNetHost
            {
                BuildEngine = new MockEngine()
            };

            Assert.True(task.Execute(), "Task failed");
            var muxer = Process.GetCurrentProcess().MainModule.FileName;
            Assert.Equal(muxer, task.ExecutablePath);
            Assert.Equal(Path.GetDirectoryName(muxer) + Path.DirectorySeparatorChar, task.DotNetDirectory);
        }
    }
}
