// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ApiCheck.Description
{
    public class TypeDescriptor : ApiElement
    {
        [JsonIgnore]
        public override string Id => string.Join(" ", GetSignatureComponents());

        public string Name { get; set; }

        public ApiElementVisibility Visibility { get; set; }

        public TypeKind Kind { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Abstract { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Static { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Sealed { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string BaseType { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<string> ImplementedInterfaces { get; } = new List<string>();

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<MemberDescriptor> Members { get; } = new List<MemberDescriptor>();

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public IList<GenericParameterDescriptor> GenericParameters { get; } = new List<GenericParameterDescriptor>();

        private IEnumerable<string> GetSignatureComponents()
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

            if (Static)
            {
                yield return "static";
            }
            else if (Kind == TypeKind.Class)
            {
                if (Abstract)
                {
                    yield return "abstract";
                }

                if (Sealed)
                {
                    yield return "sealed";
                }
            }

            switch (Kind)
            {
                case TypeKind.Struct:
                    yield return "struct";
                    break;
                case TypeKind.Interface:
                    yield return "interface";
                    break;
                case TypeKind.Class:
                    yield return "class";
                    break;
                case TypeKind.Enumeration:
                    yield return "enum";
                    break;
                case TypeKind.Unknown:
                    Console.WriteLine($"Undefined kind for: {Name}");
                    break;
                default:
                    throw new InvalidOperationException("Invalid kind");
            }

            yield return Name;

            if (BaseType != null || ImplementedInterfaces.Count > 0)
            {
                yield return ":";
                yield return string.Join(", ", GetBaseTypeAndImplementedInterfaces());
            }

            foreach (var constraint in GenericParameters.Where(p => p.HasConstraints()))
            {
                yield return "where";
                yield return constraint.Id;
            }
        }

        public MemberDescriptor FindMember(string id)
        {
            foreach (var member in Members)
            {
                if (string.Equals(id, member.Id, StringComparison.Ordinal))
                {
                    return member;
                }
            }

            return null;
        }

        private IEnumerable<string> GetBaseTypeAndImplementedInterfaces()
        {
            if (BaseType != null)
            {
                yield return BaseType;
            }

            foreach (var @interface in ImplementedInterfaces)
            {
                if (@interface != null)
                {
                    yield return @interface;
                }
            }
        }
    }
}
