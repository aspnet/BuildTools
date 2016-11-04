// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;

namespace ApiCheck.Description
{
    public class GenericParameterDescriptor : ApiElement
    {
        [JsonIgnore]
        public override string Id => HasConstraints() ? $"T{ParameterPosition}" + " : " + GetConstraints() : ParameterName;

        [JsonIgnore]
        public TypeInfo Source { get; set; }

        public string ParameterName { get; set; }

        public int ParameterPosition { get; set; }

        private string GetConstraints()
        {
            var constraints = new List<string>();
            foreach (var type in BaseTypeOrInterfaces)
            {
                constraints.Add(type);
            }
            if (Class)
            {
                constraints.Add("class");
            }

            if (Struct)
            {
                constraints.Add("struct");
            }

            if (!Struct && New)
            {
                constraints.Add("new()");
            }

            return string.Join(", ", constraints);
        }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool New { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Class { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Struct { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> BaseTypeOrInterfaces { get; } = new List<string>();

        public bool HasConstraints() => New || Class || Struct || BaseTypeOrInterfaces.Count > 0;
    }
}
