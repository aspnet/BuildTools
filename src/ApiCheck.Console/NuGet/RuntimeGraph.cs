// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.PlatformAbstractions;
using Newtonsoft.Json.Linq;

namespace ApiCheck.NuGet
{
    public partial class RuntimeGraph
    {
        private static readonly RuntimeGraph Instance;

        static RuntimeGraph()
        {
            var graph = JObject.Parse(RuntimeCompatibility);
            var runtimes = (IDictionary<string, JToken>)graph["runtimes"];
            var runtimesList = runtimes.Select(kvp => new RuntimeDefinition
            {
                Name = kvp.Key,
                Fallbacks = CreateRuntimeDefinitions((IDictionary<string, JToken>)kvp.Value, runtimes).ToArray()
            }).ToArray();

            Instance = new RuntimeGraph(runtimesList);
        }

        private static IEnumerable<RuntimeDefinition> CreateRuntimeDefinitions(
            IDictionary<string, JToken> definition,
            IDictionary<string, JToken> runtimes)
        {
            if (!definition.ContainsKey("#import"))
            {
                return Enumerable.Empty<RuntimeDefinition>();
            }
            var fallbacks = (JArray)definition["#import"];
            return fallbacks
                .Select(fr => (string)fr)
                .Select(fr =>
                {
                    if (runtimes.FirstOrDefault(r => r.Key.Equals(fr)).Key == null)
                    {
                        return new RuntimeDefinition
                        {
                            Name = fr,
                            Fallbacks = Enumerable.Empty<RuntimeDefinition>()
                        };
                    }
                    return new RuntimeDefinition
                    {
                        Name = runtimes.First(r => r.Key.Equals(fr)).Key,
                        Fallbacks = CreateRuntimeDefinitions(
                                   (IDictionary<string, JToken>)runtimes.First(r => r.Key.Equals(fr)).Value,
                                   runtimes).ToArray()
                    };
                });
        }

        private RuntimeGraph(IEnumerable<RuntimeDefinition> runtimes)
        {
            Runtimes = runtimes;
        }

        public IEnumerable<RuntimeDefinition> Runtimes { get; set; }

        public static IEnumerable<string> GetCompatibleRuntimes(string runtimeId)
        {
            var runtimes = new HashSet<string>();
            var runtime = Instance.Runtimes.FirstOrDefault(r => r.Name.Equals(runtimeId));
            if (runtime != null)
            {
                var pendingRuntimes = new Stack<RuntimeDefinition>();
                pendingRuntimes.Push(runtime);
                while (pendingRuntimes.Count > 0)
                {
                    var currentRuntime = pendingRuntimes.Pop();
                    runtimes.Add(currentRuntime.Name);
                    foreach (var fallback in currentRuntime.Fallbacks)
                    {
                        pendingRuntimes.Push(fallback);
                    }
                }
            }

            return runtimes;
        }

        public static string GetCurrentRuntimeId()
        {
            if (RuntimeEnvironment.OperatingSystemPlatform != Platform.Windows)
            {
                return RuntimeEnvironment.GetRuntimeIdentifier();
            }
            var arch = RuntimeEnvironment.RuntimeArchitecture.ToLowerInvariant();
            if (RuntimeEnvironment.OperatingSystemVersion.StartsWith("6.1", StringComparison.Ordinal))
            {
                return "win7-" + arch;
            }
            if (RuntimeEnvironment.OperatingSystemVersion.StartsWith("6.2", StringComparison.Ordinal))
            {
                return "win8-" + arch;
            }
            if (RuntimeEnvironment.OperatingSystemVersion.StartsWith("6.3", StringComparison.Ordinal))
            {
                return "win81-" + arch;
            }
            if (RuntimeEnvironment.OperatingSystemVersion.StartsWith("10.0", StringComparison.Ordinal))
            {
                return "win10-" + arch;
            }

            throw new InvalidOperationException("Runtime not supported");
        }
    }
}
