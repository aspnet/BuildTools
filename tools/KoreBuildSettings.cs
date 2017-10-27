// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KoreBuild
{
    /// <summary>
    /// Represents the korebuild.json file
    /// /// </summary>
    internal class KoreBuildSettings
    {
        public string Channel { get; set; }
        public string ToolsSource { get; set; }

        [JsonConverter(typeof(KoreBuildToolsetConverter))]
        public IEnumerable<KoreBuildToolset> Toolsets { get; set; } = Array.Empty<KoreBuildToolset>();

        [Flags]
        public enum RequiredPlatforms
        {
            None = 0,
            Windows = 1 << 0,
            MacOS = 1 << 1,
            Linux = 1 << 2,
            All = Windows | MacOS | Linux,
        }

        public abstract class KoreBuildToolset
        {
            [JsonConverter(typeof(RequiredPlatformConverter))]
            public RequiredPlatforms Required { get; set; } = RequiredPlatforms.All;
        }

        public class VisualStudioToolset : KoreBuildToolset
        {
            public bool IncludePrerelease { get; set; } = true;
            public string MinVersion { get; set; }
            public string[] RequiredWorkloads { get; set; } = Array.Empty<string>();
        }
        public static KoreBuildSettings Load(string filePath)
        {
            using (var file = File.OpenText(filePath))
            using (var json = new JsonTextReader(file))
            {
                var serializer = new JsonSerializer();
                return serializer.Deserialize<KoreBuildSettings>(json);
            }
        }

        private class KoreBuildToolsetConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => typeof(KoreBuildToolset).IsAssignableFrom(objectType);
            public override bool CanWrite { get; } = false;

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var toolsets = new List<KoreBuildToolset>();

                var obj = JObject.Load(reader);

                foreach (var prop in obj.Properties())
                {
                    switch (prop.Name.ToLowerInvariant())
                    {
                        case "visualstudio":
                            var vs = prop.Value.ToObject<VisualStudioToolset>();
                            toolsets.Add(vs);
                            break;
                        default:
                            break;
                    }
                }

                return toolsets;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }

        private class RequiredPlatformConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => typeof(RequiredPlatforms).IsAssignableFrom(objectType);
            public override bool CanWrite { get; } = false;

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                switch (reader.TokenType)
                {
                    case JsonToken.Undefined:
                    case JsonToken.Null:
                        return RequiredPlatforms.None;
                    case JsonToken.Boolean:
                        var asBool = (bool)reader.Value;
                        return asBool
                            ? RequiredPlatforms.All
                            : RequiredPlatforms.None;
                    case JsonToken.String:
                        return Parse(reader.Value as string);
                    case JsonToken.StartArray:
                        var jArray = JArray.ReadFrom(reader);
                        var platforms = RequiredPlatforms.None;
                        foreach (var value in jArray)
                        {
                            platforms |= Parse(value.Value<string>());
                        }
                        return platforms;
                    default:
                        throw new JsonReaderException("Unexpected format in korebuild.json at " + reader.Path + ". This should be a boolean or an array.");
                }
            }

            private RequiredPlatforms Parse(string name)
            {
                switch (name.ToLowerInvariant())
                {
                    case "windows":
                        return RequiredPlatforms.Windows;
                    case "linux":
                        return RequiredPlatforms.Linux;
                    case "osx":
                    case "macos":
                        return RequiredPlatforms.MacOS;
                    default:
                        throw new ArgumentException("Unrecognized plaform named " + name);
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
}
