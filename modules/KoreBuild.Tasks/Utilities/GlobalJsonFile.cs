// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace KoreBuild.Tasks.Utilities
{
    public class GlobalJsonFile
    {
        private string _path;

        public GlobalJsonFile(string globalPath)
        {
            _path = globalPath;
        }

        public void SetSdkVersion(string version)
        {
            var globalJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(_path));

            var sdkDict = (Dictionary<string, object>)globalJson["sdk"];
            sdkDict["version"] = version;

            File.WriteAllText(_path, JsonConvert.SerializeObject(sdkDict));
        }
    }
}
