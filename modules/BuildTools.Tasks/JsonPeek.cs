// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNetCore.BuildTools
{
#if SDK
    public class Sdk_JsonPeek : Task
#elif BuildTools
    public class JsonPeek : Task
#else
#error This must be built either for an SDK or for BuildTools
#endif

    {
        /// <summary>
        /// Specifies the JSONPath query.
        /// </summary>
        [Required]
        public string Query { get; set; }

        /// <summary>
        /// Specifies the JSON input as a file path.
        /// </summary>
        public string JsonInputPath { get; set; }

        /// <summary>
        /// Specifies the JSON input as a string.
        /// </summary>
        public string JsonContent { get; set; }

        /// <summary>
        /// Contains the results that are returned by this task.
        /// </summary>
        [Output]
        public ITaskItem[] Result { get; set; }

        public override bool Execute()
        {
            // pre-set the result in case we exit early
            Result = Array.Empty<ITaskItem>();

            if (!string.IsNullOrEmpty(JsonInputPath) && !string.IsNullOrEmpty(JsonContent))
            {
                Log.LogError($"Cannot specify both {nameof(JsonInputPath)} and {nameof(JsonContent)}.");
                return false;
            }

            try
            {
                JToken token;
                if (!string.IsNullOrEmpty(JsonContent))
                {
                    token = JToken.Parse(JsonContent);
                }
                else
                {
                    if (!File.Exists(JsonInputPath))
                    {
                        Log.LogError("File does not exist: {path}", JsonInputPath);
                        return false;
                    }

                    using (var stream = File.OpenRead(JsonInputPath))
                    using (var reader = new StreamReader(stream))
                    using (var json = new JsonTextReader(reader))
                    {
                        token = JToken.ReadFrom(json);
                    }
                }

                var results = new List<ITaskItem>();
                foreach (var result in token.SelectTokens(Query))
                {
                    var item = new TaskItem(result.ToString());
                    item.SetMetadata("Type", result.Type.ToString());
                    results.Add(item);
                }
                Result = results.ToArray();
                return true;
            }
            catch (JsonException ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: false);
                return false;
            }
        }
    }
}
