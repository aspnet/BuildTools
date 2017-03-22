// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ApiCheck.Description
{
    public class MemberDescriptor : ApiElement
    {
        [JsonIgnore]
        public override string Id => string.Join(" ", GetComponents());

        public MemberKind Kind { get; set; }

        public string Name { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<ParameterDescriptor> Parameters { get; set; } = new List<ParameterDescriptor>();

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string ReturnType { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Sealed { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Static { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Virtual { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Override { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Abstract { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool New { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Extension { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool ReadOnly { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string ExplicitInterface { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string ImplementedInterface { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public ApiElementVisibility? Visibility { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<GenericParameterDescriptor> GenericParameter { get; } = new List<GenericParameterDescriptor>();

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Constant { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Literal { get; set; }

        private IEnumerable<string> GetComponents()
        {
            if (ExplicitInterface == null && Visibility != null)
            {
                switch (Visibility)
                {
                    case ApiElementVisibility.Public:
                        yield return "public";
                        break;
                    case ApiElementVisibility.Protected:
                        yield return "protected";
                        break;
                    default:
                        break;
                }
            }

            if (Constant)
            {
                yield return "const";
            }
            else
            {
                if (Static)
                {
                    yield return "static";
                }

                if (ReadOnly)
                {
                    yield return "readonly";
                }
            }

            if (Abstract)
            {
                yield return "abstract";
            }

            if (Sealed && ImplementedInterface == null)
            {
                yield return "sealed";
            }

            if (!Sealed && Virtual && !Abstract && !Override && ImplementedInterface == null)
            {
                yield return "virtual";
            }

            if (Override)
            {
                yield return "override";
            }

            if (New)
            {
                yield return "new";
            }

            if (ReturnType != null)
            {
                yield return ReturnType;
            }

            if (Kind != MemberKind.Field)
            {
                var name = ExplicitInterface != null ? $"{ExplicitInterface}.{Name}" : Name;
                yield return GetParametersComponent(Name);

                foreach (var constraint in GenericParameter.Where(p => p.HasConstraints()))
                {
                    yield return "where";
                    yield return constraint.Id;
                }
            }
            else
            {
                yield return Name;

                if (Literal != null)
                {
                    yield return "=";
                    yield return Literal;
                }
            }
        }

        private string GetParametersComponent(string name)
        {
            var builder = new StringBuilder();

            builder.Append(name);
            builder.Append("(");
            for (int i = 0; i < Parameters.Count; i++)
            {
                var parameter = Parameters[i];
                if (Extension && i == 0)
                {
                    builder.Append("this ");
                }
                builder.Append(parameter.Id);
                if (i < Parameters.Count - 1)
                {
                    builder.Append(", ");
                }
            }

            builder.Append(")");
            return builder.ToString();
        }
    }
}
