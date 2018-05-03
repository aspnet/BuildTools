// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace KoreBuild.Tasks.Utilities
{
    public class GlobalJsonFile
    {
        public string Path { get; private set; }

        public IDictionary<string, string> MSBuildSdks
        {
            get
            {
                if (!_content.ContainsKey(MSBuildSDKKey))
                {
                    _content[MSBuildSDKKey] = new Dictionary<string, string>();
                }

                return (IDictionary<string, string>)_content[MSBuildSDKKey];
            }
        }

        public string SDKVersion
        {
            get
            {
                return ((IDictionary<string, string>)_content[SDKKey])["version"];
            }
            set
            {
                if (!_content.ContainsKey(SDKKey))
                {
                    _content[SDKKey] = new Dictionary<string, string>();
                }

                ((IDictionary<string, string>)_content[SDKKey])["version"] = value;
            }
        }

        private IDictionary<string, object> _content;

        private const string SDKKey = "sdk";
        private const string MSBuildSDKKey = "msbuild-sdks";

        public GlobalJsonFile(string globalPath)
        {
            Path = globalPath;
            _content = GetContents(Path);
        }

        public void Save()
        {
            File.WriteAllText(Path, JsonConvert.SerializeObject(_content));
        }

        private static Dictionary<string, object> GetContents(string path)
        {
            if(File.Exists(path))
            {
                throw new FileNotFoundException($"Global json file '{path}' was not found.");
            }

            return JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(path));
        }
    }
}
