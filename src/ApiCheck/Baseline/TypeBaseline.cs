using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ApiCheck.Baseline
{
    public class TypeBaseline : BaselineItem
    {
        public override string Id => string.Join(" ", GetMembers());

        public string Name { get; set; }

        public BaselineVisibility Visibility { get; set; }

        public BaselineKind Kind { get; set; }

        public bool Abstract { get; set; }

        public bool Static { get; set; }

        public bool Sealed { get; set; }

        public string BaseType { get; set; }

        public IList<string> ImplementedInterfaces { get; } = new List<string>();

        public IList<MemberBaseline> Members { get; set; } = new List<MemberBaseline>();

        public IList<GenericConstraintBaseline> GenericConstraints { get; } = new List<GenericConstraintBaseline>();

        private IEnumerable<string> GetMembers()
        {
            switch (Visibility)
            {
                case BaselineVisibility.Public:
                    yield return "public";
                    break;
                case BaselineVisibility.Protected:
                    yield return "protected";
                    break;
                case BaselineVisibility.Internal:
                    yield return "internal";
                    break;
                case BaselineVisibility.ProtectedInternal:
                    yield return "protected";
                    yield return "internal";
                    break;
                case BaselineVisibility.Private:
                    yield return "private";
                    break;
                default:
                    break;
            }

            if (Static)
            {
                yield return "static";
            }
            else if (Kind == BaselineKind.Class)
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
                case BaselineKind.Struct:
                    yield return "struct";
                    break;
                case BaselineKind.Interface:
                    yield return "interface";
                    break;
                case BaselineKind.Class:
                    yield return "class";
                    break;
                case BaselineKind.Enumeration:
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

        public MemberBaseline FindMember(string id)
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
