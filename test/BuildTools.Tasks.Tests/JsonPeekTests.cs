// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.AspNetCore.BuildTools;
using Xunit;

namespace BuildTools.Tasks.Tests
{
    public class JsonPeekTests
    {
        [Fact]
        public void CanReadJsonFile()
        {
            var filepath = Path.Combine(AppContext.BaseDirectory, "Resources", "sampledata.json");
            var task = new JsonPeek
            {
                BuildEngine = new MockEngine(),
                JsonInputPath = filepath,
                Query = "$.sdk.version"
            };

            Assert.True(task.Execute(), "Task should passed");
            var result = Assert.Single(task.Result);
            Assert.Equal("1.2.3", result.ItemSpec);
            Assert.Equal("String", result.GetMetadata("Type"));
        }

        [Fact]
        public void CanReadContent()
        {
            var filepath = Path.Combine(AppContext.BaseDirectory, "Resources", "sampledata.json");
            var content = File.ReadAllText(filepath);
            var task = new JsonPeek
            {
                BuildEngine = new MockEngine(),
                JsonContent = content,
                Query = "$.runtimes[*].version"
            };

            Assert.True(task.Execute(), "Task should passed");
            Assert.Collection(task.Result,
                i => Assert.Equal("1.0.0-beta", i.ItemSpec),
                i => Assert.Equal("1.0.0-alpha", i.ItemSpec));
        }

        [Fact]
        public void HandlesEmptyQuery()
        {
            var task = new JsonPeek
            {
                BuildEngine = new MockEngine(),
                JsonContent = "{}",
                Query = "$.dne.property.notthere"
            };

            Assert.True(task.Execute(), "Task should passed");
            Assert.Empty(task.Result);
        }

        [Fact]
        public void HandlesBadQuery()
        {
            var engine = new MockEngine { ContinueOnError = true };
            var task = new JsonPeek
            {
                BuildEngine = engine,
                JsonContent = "{}",
                Query = "this is bad JSONPath syntax"
            };

            Assert.False(task.Execute(), "Task should not passed");
            var error = Assert.Single(engine.Errors);
            Assert.Contains("Unexpected character while parsing path", error.Message);
            Assert.NotNull(task.Result);
        }

        [Fact]
        public void HandlesBadJson()
        {
            var engine = new MockEngine { ContinueOnError = true };
            var task = new JsonPeek
            {
                BuildEngine = engine,
                JsonContent = "{ obj: }",
                Query = ""
            };

            Assert.False(task.Execute(), "Task should not passed");
            var error = Assert.Single(engine.Errors);
            Assert.Contains("Unexpected character encountered while parsing value", error.Message);
            Assert.NotNull(task.Result);
        }

        [Fact]
        public void InvalidToSpecifyContentAndPath()
        {
            var task = new JsonPeek
            {
                BuildEngine = new MockEngine { ContinueOnError = true },
                JsonInputPath = Path.Combine(AppContext.BaseDirectory, "Resources", "sampledata.json"),
                JsonContent = "{}",
                Query = ""
            };

            Assert.False(task.Execute(), "Task should not passed");
            Assert.NotNull(task.Result);
        }
    }
}
