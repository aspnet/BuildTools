// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;

namespace ApiCheck.Description
{
    public class ParameterDescriptor : ApiElement
    {
        [JsonIgnore]
        public override string Id => string.Join(" ", GetComponents());

        [JsonIgnore]
        public ParameterInfo Source { get; set; }

        public string Name { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Type { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ParameterDirection Direction { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string DefaultValue { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool IsParams { get; set; }

        private IEnumerable<string> GetComponents()
        {
            switch (Direction)
            {
                case ParameterDirection.In:
                    break;
                case ParameterDirection.Out:
                    yield return "out";
                    break;
                case ParameterDirection.Ref:
                    yield return "ref";
                    break;
            }

            if (IsParams)
            {
                yield return "params";
            }

            yield return Type;
            yield return Name;

            if (DefaultValue != null)
            {
                yield return "=";
                yield return DefaultValue;
            }
        }
    }
}
