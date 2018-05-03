// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ApiCheck.Description;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ApiCheck
{
    public class ApiListingGenerator
    {
        private const BindingFlags SearchFlags = BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        private readonly Assembly _assembly;
        private readonly IEnumerable<Func<MemberInfo, bool>> _filters;

        public ApiListingGenerator(Assembly assembly, IEnumerable<Func<MemberInfo, bool>> filters)
        {
            _assembly = assembly;
            _filters = filters;
        }

        public static JObject GenerateApiListingReport(Assembly assembly, IEnumerable<Func<MemberInfo, bool>> filters = null)
        {
            var generator = new ApiListingGenerator(assembly, filters ?? Enumerable.Empty<Func<MemberInfo, bool>>());
            var apiListingDocument = generator.GenerateApiListing();
            return JObject.FromObject(apiListingDocument);
        }

        public ApiListing GenerateApiListing()
        {
            var types = _assembly.DefinedTypes
#if NETCOREAPP2_0 // Reflection does not provide a hook to enumerate forwarded types in .NET Framework.
                .Concat(_assembly
                    .GetForwardedTypes()
                    .Select(type => type.GetTypeInfo()))
#endif
                .Where(t => t.IsPublic || t.IsNestedPublic || t.IsNestedFamily || t.IsNestedFamORAssem);

            var document = new ApiListing
            {
                AssemblyIdentity = _assembly.GetName().ToString(),
                SourceFilters = _filters
            };
            foreach (var type in types.Where(t => !_filters.Any(filter => filter(t))))
            {
                var apiListingType = GenerateTypeDescriptor(type);
                document.Types.Add(apiListingType);
            }

            return document;
        }

        public static TypeDescriptor GenerateTypeDescriptor(TypeInfo type, IEnumerable<Func<MemberInfo, bool>> filters = null)
        {
            filters = filters ?? Enumerable.Empty<Func<MemberInfo, bool>>();
            var generator = new ApiListingGenerator(type.Assembly, filters);
            return generator.GenerateTypeDescriptor(type);
        }

        private TypeDescriptor GenerateTypeDescriptor(TypeInfo type)
        {
            var typeDescriptor = new TypeDescriptor
            {
                Source = type,
                Name = TypeDescriptor.GetTypeNameFor(type),
                Kind = GetTypeKind(type),
            };
            if (typeDescriptor.Kind == TypeKind.Unknown)
            {
                throw new InvalidOperationException($"Can't determine type for {type.FullName}");
            }

            // At this point we've filtered away any non public or protected member,
            // so we only need to check if something is public
            typeDescriptor.Visibility = type.IsPublic || type.IsNestedPublic ? ApiElementVisibility.Public : ApiElementVisibility.Protected;

            typeDescriptor.Static = typeDescriptor.Kind == TypeKind.Class && type.IsSealed && type.IsAbstract;

            typeDescriptor.Abstract = type.IsAbstract;

            typeDescriptor.Sealed = type.IsSealed;

            if (type.BaseType != null &&
                type.BaseType != typeof(object) &&
                type.BaseType != typeof(ValueType) &&
                !(type.IsEnum && type.GetEnumUnderlyingType() == typeof(int)))
            {
                typeDescriptor.BaseType = !type.IsEnum ?
                    TypeDescriptor.GetTypeNameFor(type.BaseType.GetTypeInfo()) :
                    TypeDescriptor.GetTypeNameFor(type.GetEnumUnderlyingType().GetTypeInfo());
            }

            if (type.ImplementedInterfaces.Any())
            {
                var interfaces = TypeDescriptor.GetImplementedInterfacesFor(type).ToList();
                foreach (var @interface in interfaces.Select(TypeDescriptor.GetTypeNameFor))
                {
                    typeDescriptor.ImplementedInterfaces.Add(@interface);
                }
            }

            if (type.IsGenericType)
            {
                var constraints = GetGenericConstraintsFor(type.GetGenericArguments().Select(t => t.GetTypeInfo()).ToArray());
                foreach (var constraint in constraints)
                {
                    typeDescriptor.GenericParameters.Add(constraint);
                }
            }

            var members = type.GetMembers(SearchFlags);

            foreach (var member in members)
            {
                if (_filters.Any(f => f(member)))
                {
                    continue;
                }

                var memberApiListing = GenerateMemberApiListing(type, member);
                if (memberApiListing != null)
                {
                    memberApiListing.Source = member;
                    typeDescriptor.Members.Add(memberApiListing);
                }
            }

            return typeDescriptor;
        }

        private static TypeKind GetTypeKind(TypeInfo type)
        {
            if (type.IsInterface)
            {
                return TypeKind.Interface;
            }

            if (type.IsEnum)
            {
                return TypeKind.Enumeration;
            }

            if (type.IsValueType)
            {
                return TypeKind.Struct;
            }

            if (type.IsClass)
            {
                return TypeKind.Class;
            }

            return TypeKind.Unknown;
        }

        private static IEnumerable<GenericParameterDescriptor> GetGenericConstraintsFor(TypeInfo[] genericArguments)
        {
            foreach (var typeArgument in genericArguments)
            {
                var constraintDescriptor = new GenericParameterDescriptor
                {
                    Source = typeArgument
                };
                if (typeArgument.IsGenericParameter)
                {
                    if (typeArgument.BaseType != null &&
                        typeArgument.BaseType != typeof(object)
                        && typeArgument.BaseType != typeof(ValueType))
                    {
                        constraintDescriptor.BaseTypeOrInterfaces.Add(TypeDescriptor.GetTypeNameFor(typeArgument.BaseType.GetTypeInfo()));
                    }

                    foreach (var interfaceType in TypeDescriptor.GetImplementedInterfacesFor(typeArgument))
                    {
                        constraintDescriptor.BaseTypeOrInterfaces.Add(TypeDescriptor.GetTypeNameFor(interfaceType));
                    }

                    constraintDescriptor.ParameterName = typeArgument.Name;
                    constraintDescriptor.ParameterPosition = typeArgument.GenericParameterPosition;
                    constraintDescriptor.New = (typeArgument.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint) == GenericParameterAttributes.DefaultConstructorConstraint;
                    constraintDescriptor.Class = (typeArgument.GenericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint) == GenericParameterAttributes.ReferenceTypeConstraint;
                    constraintDescriptor.Struct = (typeArgument.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) == GenericParameterAttributes.NotNullableValueTypeConstraint;
                }
                else
                {
                    constraintDescriptor.ParameterName = TypeDescriptor.GetTypeNameFor(typeArgument);
                }

                yield return constraintDescriptor;
            }
        }

        public static MemberDescriptor GenerateMemberApiListing(TypeInfo type, MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Constructor:
                    var ctor = (ConstructorInfo)member;
                    if (!ctor.IsPublic && !ctor.IsFamily && !ctor.IsFamilyOrAssembly)
                    {
                        return null;
                    }

                    var constructorDescriptor = new MemberDescriptor
                    {
                        Kind = MemberKind.Constructor,
                        Visibility = ctor.IsPublic ? ApiElementVisibility.Public : ApiElementVisibility.Protected,

                        Name = MemberDescriptor.GetMemberNameFor(ctor)
                    };
                    foreach (var parameter in ctor.GetParameters())
                    {
                        var parameterDescriptor = GenerateParameterDescriptor(parameter);
                        constructorDescriptor.Parameters.Add(parameterDescriptor);
                    }

                    return constructorDescriptor;

                case MemberTypes.Method:
                    var method = (MethodInfo)member;
                    if (!method.IsPublic && !method.IsFamily && !method.IsFamilyOrAssembly)
                    {
                        return null;
                    }

                    var methodDescriptor = new MemberDescriptor
                    {
                        Kind = MemberKind.Method,

                        Visibility = method.IsPublic ? ApiElementVisibility.Public : ApiElementVisibility.Protected
                    };
                    if (!type.IsInterface)
                    {
                        methodDescriptor.ExplicitInterface = GetInterfaceImplementation(method, explicitImplementation: true);
                        methodDescriptor.ImplementedInterface = methodDescriptor.ExplicitInterface ?? GetInterfaceImplementation(method, explicitImplementation: false);
                    }
                    else
                    {
                        methodDescriptor.Visibility = null;
                    }

                    methodDescriptor.Name = MemberDescriptor.GetMemberNameFor(method);

                    if (method.IsGenericMethod)
                    {
                        var constraints = GetGenericConstraintsFor(method.GetGenericArguments().Select(t => t.GetTypeInfo()).ToArray());
                        foreach (var constraint in constraints)
                        {
                            methodDescriptor.GenericParameter.Add(constraint);
                        }
                    }

                    methodDescriptor.Static = method.IsStatic;
                    methodDescriptor.Sealed = method.IsFinal;
                    methodDescriptor.Virtual = !type.IsInterface && method.IsVirtual;
                    methodDescriptor.Override = !type.IsInterface && method.IsVirtual && !Equals(method.GetBaseDefinition(), method);
                    methodDescriptor.Abstract = !type.IsInterface && method.IsAbstract;
                    methodDescriptor.New = !method.IsAbstract && !method.IsVirtual && method.IsHideBySig && method
                                               .DeclaringType.GetMember(method.Name)
                                               .OfType<MethodInfo>()
                                               .Count(m => SameSignature(m, method)) > 1;
                    methodDescriptor.Extension = method.IsDefined(typeof(ExtensionAttribute), false);

                    foreach (var parameter in method.GetParameters())
                    {
                        var parameterDescriptor = GenerateParameterDescriptor(parameter);
                        methodDescriptor.Parameters.Add(parameterDescriptor);
                    }

                    methodDescriptor.ReturnType = TypeDescriptor.GetTypeNameFor(method.ReturnType.GetTypeInfo());

                    return methodDescriptor;

                case MemberTypes.Field:
                    var field = (FieldInfo)member;
                    if (!field.IsPublic && !field.IsFamily && !field.IsFamilyOrAssembly)
                    {
                        return null;
                    }

                    if (type.IsEnum && !field.IsLiteral)
                    {
                        // Skip storage for enumerations.
                        return null;
                    }

                    var fieldDescriptor = new MemberDescriptor
                    {
                        Visibility = field.IsPublic ? ApiElementVisibility.Public : ApiElementVisibility.Protected,

                        Kind = MemberKind.Field,
                        Name = field.Name
                    };
                    if (type.IsEnum || field.IsLiteral)
                    {
                        fieldDescriptor.Literal = FormatLiteralValue(field.GetRawConstantValue(), field.FieldType);
                    }

                    if (type.IsEnum)
                    {
                        fieldDescriptor.Visibility = null;
                    }
                    else
                    {
                        fieldDescriptor.Constant = field.IsLiteral;
                        fieldDescriptor.Static = field.IsStatic;
                        fieldDescriptor.ReadOnly = field.IsInitOnly;
                        fieldDescriptor.ReturnType = TypeDescriptor.GetTypeNameFor(field.FieldType.GetTypeInfo());
                    }

                    return fieldDescriptor;
                case MemberTypes.Event:
                case MemberTypes.Property:
                case MemberTypes.NestedType:
                    // All these cases are covered by the methods they implicitly define on the class
                    // (Properties and Events) and when we enumerate all the types in an assembly (Nested types).
                    return null;

                case MemberTypes.TypeInfo:
                // There should not be any member passed into this method that is not a top level type.
                case MemberTypes.Custom:
                // We don't know about custom member types, so better throw if we find something we don't understand.
                case MemberTypes.All:
                    throw new InvalidOperationException($"'{type.MemberType}' [{member}] is not supported.");

                default:
                    return null;
            }
        }

        public static ApiListing LoadFrom(string json, IEnumerable<Func<ApiElement, bool>> oldApiListingFilters = null)
        {
            oldApiListingFilters = oldApiListingFilters ?? Enumerable.Empty<Func<ApiElement, bool>>();
            var oldApiListing = JsonConvert.DeserializeObject<ApiListing>(json);
            foreach (var type in oldApiListing.Types.ToArray())
            {
                if (oldApiListingFilters.Any(filter => filter(type)))
                {
                    oldApiListing.Types.Remove(type);
                }

                foreach (var member in type.Members.ToArray())
                {
                    if (oldApiListingFilters.Any(filter => filter(member)))
                    {
                        type.Members.Remove(member);
                    }
                }
            }
            return oldApiListing;
        }

        private static string GetInterfaceImplementation(MethodInfo method, bool explicitImplementation)
        {
            var typeInfo = method.DeclaringType.GetTypeInfo();
            foreach (var interfaceImplementation in method.DeclaringType.GetInterfaces())
            {
                var map = typeInfo.GetRuntimeInterfaceMap(interfaceImplementation);
                if (map.TargetMethods.Any(m => m.Equals(method)))
                {
                    return !explicitImplementation || (method.IsPrivate && method.IsFinal) ?
                        TypeDescriptor.GetTypeNameFor(interfaceImplementation.GetTypeInfo()) :
                        null;
                }
            }

            return null;
        }

        private static bool SameSignature(MethodInfo candidate, MethodInfo method)
        {
            if (candidate.ReturnType != method.ReturnType)
            {
                return false;
            }

            var candidateParameters = candidate.GetParameters();
            var methodParameters = method.GetParameters();

            if (candidateParameters.Length != methodParameters.Length)
            {
                return false;
            }

            for (var i = 0; i < candidateParameters.Length; i++)
            {
                var candidateParameter = candidateParameters[i];
                var methodParameter = methodParameters[i];
                if (candidateParameter.ParameterType != methodParameter.ParameterType ||
                    candidateParameter.HasDefaultValue != methodParameter.HasDefaultValue ||
                    candidateParameter.IsIn != methodParameter.IsIn ||
                    candidateParameter.IsOut != methodParameter.IsOut ||
                    candidateParameter.IsOptional != methodParameter.IsOptional)
                {
                    return false;
                }
            }

            return true;
        }

        private static ParameterDescriptor GenerateParameterDescriptor(ParameterInfo parameter)
        {
            return new ParameterDescriptor
            {
                Source = parameter,
                Name = parameter.Name,
                Type = TypeDescriptor.GetTypeNameFor(parameter.ParameterType.GetTypeInfo()),
                Direction = parameter.ParameterType.IsByRef && parameter.IsOut ? ParameterDirection.Out :
                    parameter.ParameterType.IsByRef && !parameter.IsOut ? ParameterDirection.Ref :
                    ParameterDirection.In,
                DefaultValue = parameter.HasDefaultValue ? FormatLiteralValue(parameter) : null,
                IsParams = parameter.GetCustomAttribute<ParamArrayAttribute>() != null
            };
        }

        private static string FormatLiteralValue(ParameterInfo parameter)
        {
            return FormatLiteralValue(parameter.RawDefaultValue, parameter.ParameterType);
        }

        private static string FormatLiteralValue(object rawDefaultValue, Type elementType)
        {
            if (rawDefaultValue == null)
            {
                var elementTypeInfo = elementType.GetTypeInfo();
                return elementTypeInfo.IsValueType ? $"default({TypeDescriptor.GetTypeNameFor(elementTypeInfo)})" : "null";
            }

            if (elementType == typeof(string))
            {
                return $"\"{rawDefaultValue}\"";
            }

            if (elementType == typeof(char))
            {
                return $"'{rawDefaultValue}'";
            }

            if (rawDefaultValue is bool ||
                rawDefaultValue is byte ||
                rawDefaultValue is sbyte ||
                rawDefaultValue is short ||
                rawDefaultValue is ushort ||
                rawDefaultValue is int ||
                rawDefaultValue is uint ||
                rawDefaultValue is long ||
                rawDefaultValue is ulong ||
                rawDefaultValue is double ||
                rawDefaultValue is float ||
                rawDefaultValue is decimal)
            {
                return rawDefaultValue.ToString();
            }

            throw new InvalidOperationException("Unsupported default value type");
        }
    }
}
