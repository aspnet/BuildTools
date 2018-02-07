// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.AspNetCore.BuildTools;
using Xunit;

namespace BuildTools.Tasks.Tests
{
    public class RunTaskTests
    {
        [Fact]
        public void ExitCodeIsNonZeroIfFailedToStart()
        {
            var engine = new MockEngine { ContinueOnError = true };
            var task = new Run
            {
                FileName = "sdfkjskldfsjdflkajsdas",
                BuildEngine = engine,
            };
            Assert.False(task.Execute());
            Assert.NotEqual(0, task.ExitCode);
        }
    }
}
