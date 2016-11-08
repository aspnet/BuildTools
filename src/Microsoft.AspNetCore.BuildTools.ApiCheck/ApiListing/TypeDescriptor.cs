// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace ApiCheck.Description
{
    public class TypeDescriptor : ApiElement
    {
        [JsonIgnore]
        public override string Id => string.Join(" ", GetSignatureComponents());

        [JsonIgnore]
        public TypeInfo Source { get; set; }

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

        public static string GetTypeNameFor(TypeInfo type)
        {
            string typeName = type.FullName ?? type.Name;

            if (type.IsGenericParameter)
            {
                typeName = $"T{type.GenericParameterPosition}";
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingTypeName = GetTypeNameFor(type.GetGenericArguments().Single().GetTypeInfo());
                typeName = underlyingTypeName + "?";
            }

            if (type.IsGenericType)
            {
                if (type.DeclaringType == null || type.DeclaringType.GetTypeInfo() == type)
                {
                    var name = type.GetGenericTypeDefinition().FullName;
                    typeName = name.Substring(0, name.IndexOf('`'));
                    typeName = $"{typeName}<{string.Join(", ", type.GetGenericArguments().Select(ga => GetTypeNameFor(ga.GetTypeInfo())))}>";
                }
                else
                {
                    var container = type.DeclaringType.GetTypeInfo();
                    var prefix = GetTypeNameFor(container);
                    var name = type.GetGenericTypeDefinition().FullName;
                    var currentTypeGenericArguments = type.GetGenericTypeDefinition().GetGenericArguments()
                    .Where(p => !container.GetGenericArguments().Any(cp => cp.Name == p.Name))
                    .Select(p => type.GetGenericArguments()[p.GenericParameterPosition])
                    .ToArray();

                    if (currentTypeGenericArguments.Length == 0)
                    {
                        var nestedClassSeparatorIndex = name.LastIndexOf("+");
                        name = name.Substring(nestedClassSeparatorIndex + 1, name.Length - nestedClassSeparatorIndex - 1);
                    }
                    else
                    {
                        var lastGenericArityIndex = name.LastIndexOf('`');
                        var nestedClassSeparatorIndex = name.LastIndexOf("+");
                        name = name.Substring(nestedClassSeparatorIndex + 1, lastGenericArityIndex - nestedClassSeparatorIndex - 1);
                        name = $"{name}<{string.Join(", ", currentTypeGenericArguments.Select(ga => GetTypeNameFor(ga.GetTypeInfo())))}>";
                    }

                    typeName = $"{prefix}+{name}";
                }
            }

            if (type.IsArray)
            {
                var name = GetTypeNameFor(type.GetElementType().GetTypeInfo());
                typeName = $"{name}[]";
            }

            if (type.IsByRef)
            {
                typeName = GetTypeNameFor(type.GetElementType().GetTypeInfo());
            }

            // Parameters passed by reference through out or ref modifiers have an & at the end of their
            // name to indicate they are pointers to a given type.
            typeName = typeName.TrimEnd('&');

            return typeName;
        }

        public static IEnumerable<TypeInfo> GetImplementedInterfacesFor(TypeInfo type)
        {
            if (type.IsGenericParameter)
            {
                var interfaces = type.ImplementedInterfaces.ToArray();
                for (var i = 0; i < interfaces.Length; i++)
                {
                    var implementedInterface = interfaces[i].GetTypeInfo();
                    var implementedOnBaseType = type.BaseType != null &&
                        InterfaceIsImplementedOnBaseType(type.BaseType.GetTypeInfo(), implementedInterface);

                    if (!implementedOnBaseType && !InterfaceIsTransitivelyImplemented(type, implementedInterface))
                    {
                        yield return implementedInterface;
                    }
                }
            }
            else if (!type.IsInterface)
            {
                var interfaces = type.ImplementedInterfaces.ToArray();
                for (var i = 0; i < interfaces.Length; i++)
                {
                    var implementedInterface = interfaces[i].GetTypeInfo();
                    if ((!InterfaceIsImplementedOnBaseType(type.BaseType.GetTypeInfo(), implementedInterface) &&
                        !InterfaceIsTransitivelyImplemented(type, implementedInterface)) ||
                        InterfaceIsReimplementedOnCurrentType(type, implementedInterface))
                    {
                        yield return implementedInterface;
                    }
                }
            }
            else
            {
                var interfaces = type.ImplementedInterfaces.ToArray();
                for (var i = 0; i < interfaces.Length; i++)
                {
                    var implementedInterface = interfaces[i].GetTypeInfo();
                    if (!InterfaceIsTransitivelyImplemented(type, implementedInterface))
                    {
                        yield return implementedInterface;
                    }
                }
            }
        }

        private static bool InterfaceIsReimplementedOnCurrentType(TypeInfo type, TypeInfo implementedInterface)
        {
            var mapping = type.GetRuntimeInterfaceMap(implementedInterface.AsType());
            return InterfaceIsImplementedOnBaseType(type.BaseType.GetTypeInfo(), implementedInterface) &&
                mapping.TargetMethods.Any(tm => tm.DeclaringType.GetTypeInfo().Equals(type) &&
                (tm.IsPrivate || tm.Equals(tm.GetBaseDefinition())));
        }

        private static bool InterfaceIsTransitivelyImplemented(TypeInfo type, TypeInfo implementedInterface)
        {
            return type.ImplementedInterfaces
                .SelectMany(ii => ii.GetTypeInfo().ImplementedInterfaces)
                .Any(bii => bii.GetTypeInfo().Equals(implementedInterface));
        }

        private static bool InterfaceIsImplementedOnBaseType(TypeInfo typeInfo, TypeInfo implementedInterface)
        {
            return typeInfo.ImplementedInterfaces.Any(ii => ii.Equals(implementedInterface));
        }
    }
}
