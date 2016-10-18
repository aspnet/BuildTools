// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ApiCheck.Description
{
    public class TypeDescriptor : ApiElement
    {
        public override string Id => string.Join(" ", GetSignatureComponents());

        public string Name { get; set; }

        public ApiElementVisibility Visibility { get; set; }

        public TypeKind Kind { get; set; }

        public bool Abstract { get; set; }

        public bool Static { get; set; }

        public bool Sealed { get; set; }

        public string BaseType { get; set; }

        public IList<string> ImplementedInterfaces { get; } = new List<string>();

        public IList<MemberDescriptor> Members { get; set; } = new List<MemberDescriptor>();

        public IList<GenericConstraintDescriptor> GenericConstraints { get; } = new List<GenericConstraintDescriptor>();

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
                case ApiElementVisibility.Internal:
                    yield return "internal";
                    break;
                case ApiElementVisibility.ProtectedInternal:
                    yield return "protected";
                    yield return "internal";
                    break;
                case ApiElementVisibility.Private:
                    yield return "private";
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
                default:
                    throw new InvalidOperationException("Invalid kind");
            }

            yield return Name;

            if (BaseType != null || ImplementedInterfaces.Count > 0)
            {
                yield return ":";
                yield return string.Join(", ", GetBaseTypeAndImplementedInterfaces());
            }

            foreach (var constraint in GenericConstraints)
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
            string typeName = type.FullName;

            if (type.IsGenericParameter)
            {
                typeName = type.Name;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingTypeName = GetTypeNameFor(type.GetGenericArguments().Single().GetTypeInfo());
                typeName = underlyingTypeName + "?";
            }

            if (type.IsGenericType)
            {
                var name = type.GetGenericTypeDefinition().FullName;
                name = name.Substring(0, name.IndexOf('`'));
                typeName = $"{name}<{string.Join(", ", type.GetGenericArguments().Select(ga => GetTypeNameFor(ga.GetTypeInfo())))}>";
            }

            if (type.IsArray)
            {
                var name = GetTypeNameFor(type.GetElementType().GetTypeInfo());
                typeName = $"{name}[]";
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
                var extendedInterfaces = type.ImplementedInterfaces
                    .SelectMany(i => i.GetTypeInfo().ImplementedInterfaces
                        .Select(ii => ii.GetTypeInfo()));

                foreach (var implementedInterface in type.ImplementedInterfaces.Select(t => t.GetTypeInfo()))
                {
                    if (type.BaseType != null && type.BaseType.GetTypeInfo().ImplementedInterfaces.Any(i => i.GetTypeInfo().Equals(implementedInterface)) ||
                        extendedInterfaces.Contains(implementedInterface))
                    {
                        continue;
                    }

                    yield return implementedInterface;
                }

                yield break;
            }

            if (!type.IsInterface)
            {
                var interfaces = type.ImplementedInterfaces;
                foreach (var implementedInterface in interfaces)
                {
                    var mapping = type.GetRuntimeInterfaceMap(implementedInterface);
                    if (type.BaseType.GetTypeInfo().ImplementedInterfaces.Any(i => i.GetTypeInfo().Equals(implementedInterface)) &&
                        !mapping.TargetMethods.Any(t => t.DeclaringType.Equals(type)))
                    {
                        continue;
                    }

                    if (mapping.TargetType.Equals(type))
                    {
                        yield return implementedInterface.GetTypeInfo();
                    }
                }
            }
            else
            {
                var implementedInterfaces = type.ImplementedInterfaces.Select(i => i.GetTypeInfo());
                var includedInterfaces = implementedInterfaces.SelectMany(i => i.ImplementedInterfaces.Select(ii => ii.GetTypeInfo()));
                var directlyImplementedInterfaces = implementedInterfaces.Except(includedInterfaces);

                foreach (var directInterface in directlyImplementedInterfaces)
                {
                    yield return directInterface;
                }
            }
        }
    }
}
