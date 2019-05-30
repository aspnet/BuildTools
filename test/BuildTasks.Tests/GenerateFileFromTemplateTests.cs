// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.BuildTools;
using Xunit;

namespace BuildTools.Tasks.Tests
{
    public class GenerateFileFromTemplateTests
    {
        [Fact]
        public void WarnsForMissingVariableNames()
        {
            var engine = new MockEngine();
            var task = new GenerateFileFromTemplate
            {
                BuildEngine = engine,
            };

            Assert.Equal("Hello !", task.Replace("Hello ${World}!", new Dictionary<string, string>()));
            var warning = Assert.Single(engine.Warnings);
            Assert.Contains("No property value", warning.Message);
        }

        [Fact]
        public void WarnsWhenExpectingClosingBrace()
        {
            var engine = new MockEngine();
            var task = new GenerateFileFromTemplate
            {
                BuildEngine = engine,
            };

            Assert.Equal("Hello ${Greeting!", task.Replace("Hello ${Greeting!", new Dictionary<string, string>()));
            var warning = Assert.Single(engine.Warnings);
            Assert.Contains("Expected closing bracket", warning.Message);
        }

        [Theory]
        [MemberData(nameof(SubsitutionData))]
        public void SubsitutesVariableNames(string template, IDictionary<string, string> variables, string output)
        {
            var engine = new MockEngine();
            var task = new GenerateFileFromTemplate
            {
                BuildEngine = engine,
            };

            Assert.Equal(output, task.Replace(template, variables));
            Assert.Empty(engine.Warnings);
        }

        public static TheoryData<string, IDictionary<string, string>, string> SubsitutionData
            => new TheoryData<string, IDictionary<string, string>, string>
            {
                {"`", new Dictionary<string,string> {}, "`" },
                {"`other", new Dictionary<string,string> {}, @"`other" },
                {"```$", new Dictionary<string,string> {}, "`$" },
                {"``", new Dictionary<string,string> {}, "`" },
                {"`$", new Dictionary<string,string> {}, "$" },
                {@"`${Hello", new Dictionary<string,string> {}, @"${Hello" },
                {"Hello ${Greeting}", new Dictionary<string,string> {["Greeting"] = "World"}, "Hello World" },
                {@"Hello `${Greeting}", new Dictionary<string,string> {["Greeting"] = "World"}, @"Hello ${Greeting}" },
                {"Hello ${Greeting}!", new Dictionary<string,string> {["Greeting"] = "World"}, @"Hello World!" },
                {"${One}${Two}${Three}", new Dictionary<string,string> {["One"] = "1", ["Two"]="2", ["Three"] = "3"}, "123" },
            };
    }
}
