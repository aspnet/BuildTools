// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Newtonsoft.Json;
using Xunit;

namespace KoreBuild.Tasks.Tests
{
    public class KoreBuildSettingsTest : IDisposable
    {
        private readonly string _configFile;

        public KoreBuildSettingsTest()
        {
            _configFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        }

        public void Dispose()
        {
            if (File.Exists(_configFile))
            {
                File.Delete(_configFile);
            }
        }

        [Fact]
        public void ItDeserializesVisualStudioToolset()
        {
            File.WriteAllText(_configFile, @"
{
  ""toolsets"": {
    ""visualstudio"": {
      ""requiredWorkloads"": [ ""MyTestWorkload"" ],
      ""minVersion"": ""15.4"",
      ""includePrerelease"": false
    }
  }
}");

            var settings = KoreBuildSettings.Load(_configFile);

            var toolset = Assert.Single(settings.Toolsets);

            var vs = Assert.IsType<KoreBuildSettings.VisualStudioToolset>(toolset);
            Assert.Collection(vs.RequiredWorkloads, w => Assert.Equal("MyTestWorkload", w));
            Assert.False(vs.IncludePrerelease);
            Assert.Equal("15.4", vs.MinVersion);
        }

        [Fact]
        public void ItDeserializesEmptyVisualStudioToolset()
        {
            File.WriteAllText(_configFile, @"
{
  ""toolsets"": {
    ""visualstudio"": {}
  }
}
");

            var settings = KoreBuildSettings.Load(_configFile);
            var toolset = Assert.Single(settings.Toolsets);

            var vs = Assert.IsType<KoreBuildSettings.VisualStudioToolset>(toolset);
            Assert.NotNull(vs.RequiredWorkloads);
            Assert.Empty(vs.RequiredWorkloads);
            Assert.True(vs.IncludePrerelease);
            Assert.Null(vs.MinVersion);
        }

        [Theory]
        [InlineData("true", KoreBuildSettings.RequiredPlatforms.All)]
        [InlineData("null", KoreBuildSettings.RequiredPlatforms.None)]
        [InlineData("false", KoreBuildSettings.RequiredPlatforms.None)]
        [InlineData(@"[""windows""]", KoreBuildSettings.RequiredPlatforms.Windows)]
        [InlineData(@"[""linux""]", KoreBuildSettings.RequiredPlatforms.Linux)]
        [InlineData(@"[""osx""]", KoreBuildSettings.RequiredPlatforms.MacOS)]
        [InlineData(@"[""macos""]", KoreBuildSettings.RequiredPlatforms.MacOS)]
        [InlineData(@"[""macos"", ""linux""]", KoreBuildSettings.RequiredPlatforms.MacOS | KoreBuildSettings.RequiredPlatforms.Linux)]
        internal void ParsesPlatforms(string json, KoreBuildSettings.RequiredPlatforms platforms)
        {
            File.WriteAllText(_configFile, @"
{
  ""toolsets"": {
    ""visualstudio"": {
        ""required"": " + json + @"
    }
  }
}
");

            var settings = KoreBuildSettings.Load(_configFile);
            var toolset = Assert.Single(settings.Toolsets);

            var vs = Assert.IsType<KoreBuildSettings.VisualStudioToolset>(toolset);
            Assert.Equal(platforms, vs.Required);
        }

        [Fact]
        public void ItDeserializesNodeJSToolsetWithVersion()
        {
            File.WriteAllText(_configFile, @"
{
  ""toolsets"": {
    ""nodejs"": {
      ""minVersion"": ""8.0""
    }
  }
}");
            var settings = KoreBuildSettings.Load(_configFile);
            var toolset = Assert.Single(settings.Toolsets);

            var node = Assert.IsType<KoreBuildSettings.NodeJSToolset>(toolset);
            Assert.Equal(new Version(8, 0), node.MinVersion);
        }

        [Fact]
        public void ItFailsIfVersionIsNotValid()
        {
            File.WriteAllText(_configFile, @"
{
  ""toolsets"": {
    ""nodejs"": {
      ""minVersion"": ""banana""
    }
  }
}");
            Assert.Throws<JsonSerializationException>(() => KoreBuildSettings.Load(_configFile));
        }
    }
}
