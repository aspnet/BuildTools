// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.AspNetCore.BuildTools;
using Microsoft.DotNet.Cli.Utils;
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
            var muxer = new Muxer().MuxerPath;
            Assert.Equal(muxer, task.ExecutablePath);
            Assert.Equal(Path.GetDirectoryName(muxer) + Path.DirectorySeparatorChar, task.DotNetDirectory);
        }
    }
}
