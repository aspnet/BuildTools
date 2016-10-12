using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ApiCheck.Baseline;
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
        private readonly IEnumerable<Func<TypeInfo, bool>> _filters;

        public ApiListingGenerator(Assembly assembly, IEnumerable<Func<TypeInfo, bool>> filters)
        {
            _assembly = assembly;
            _filters = filters;
        }

        public static JObject GenerateBaselineReport(Assembly assembly, IEnumerable<Func<TypeInfo, bool>> filters = null)
        {
            var generator = new ApiListingGenerator(assembly, filters ?? Enumerable.Empty<Func<TypeInfo, bool>>());
            var baselineDocument = generator.GenerateBaseline();
            return JObject.FromObject(baselineDocument);
        }

        public ApiListing GenerateBaseline()
        {
            var types = _assembly.DefinedTypes;

            var document = new ApiListing();
            document.AssemblyIdentity = _assembly.GetName().ToString();

            foreach (var type in _assembly.DefinedTypes.Where(type => _filters.All(filter => filter(type))))
            {
                var baselineType = GenerateTypeBaseline(type);
                document.Types.Add(baselineType);
            }

            return document;
        }

        private TypeDescriptor GenerateTypeBaseline(TypeInfo type)
        {
            var typeBaseline = new TypeDescriptor();

            typeBaseline.Name = TypeDescriptor.GetTypeNameFor(type);

            typeBaseline.Kind = type.IsInterface ? TypeKind.Interface :
                !type.IsValueType ? TypeKind.Class :
                type.IsEnum ? TypeKind.Enumeration :
                TypeKind.Struct;

            typeBaseline.Visibility = type.IsPublic || type.IsNestedPublic ? ApiElementVisibility.Public :
                type.IsNestedFamORAssem ? ApiElementVisibility.ProtectedInternal :
                type.IsNestedFamily ? ApiElementVisibility.Protected :
                type.IsNestedPrivate ? ApiElementVisibility.Private :
                ApiElementVisibility.Internal;

            typeBaseline.Static = typeBaseline.Kind == TypeKind.Class && type.IsSealed && type.IsAbstract;

            typeBaseline.Abstract = type.IsAbstract;

            typeBaseline.Sealed = type.IsSealed;

            if (type.BaseType != null &&
                type.BaseType != typeof(object) &&
                type.BaseType != typeof(ValueType) &&
                !(type.IsEnum && type.GetEnumUnderlyingType() == typeof(int)))
            {
                typeBaseline.BaseType = !type.IsEnum ?
                    TypeDescriptor.GetTypeNameFor(type.BaseType.GetTypeInfo()) :
                    TypeDescriptor.GetTypeNameFor(type.GetEnumUnderlyingType().GetTypeInfo());
            }

            if (type.ImplementedInterfaces?.Count() > 0)
            {
                var interfaces = TypeDescriptor.GetImplementedInterfacesFor(type);
                foreach (var @interface in interfaces.Select(i => TypeDescriptor.GetTypeNameFor(i)))
                {
                    typeBaseline.ImplementedInterfaces.Add(@interface);
                }
            }

            if (type.IsGenericType)
            {
                var constraints = GetGenericConstraintsFor(type.GetGenericArguments().Select(t => t.GetTypeInfo()));
                foreach (var constraint in constraints)
                {
                    typeBaseline.GenericConstraints.Add(constraint);
                }
            }

            var members = type.GetMembers(SearchFlags);

            foreach (var member in members)
            {
                var memberBaseline = GenerateMemberBaseline(type, member);
                if (memberBaseline != null)
                {
                    typeBaseline.Members.Add(memberBaseline);
                }
            }

            return typeBaseline;
        }

        private static IEnumerable<GenericConstraintDescriptor> GetGenericConstraintsFor(IEnumerable<TypeInfo> genericArguments)
        {
            foreach (var typeArgument in genericArguments)
            {
                var constraintBaseline = new GenericConstraintDescriptor();

                if (typeArgument.BaseType != null &&
                    typeArgument.BaseType != typeof(object)
                    && typeArgument.BaseType != typeof(ValueType))
                {
                    constraintBaseline.BaseTypeOrInterfaces.Add(TypeDescriptor.GetTypeNameFor(typeArgument.BaseType.GetTypeInfo()));
                }

                foreach (var interfaceType in TypeDescriptor.GetImplementedInterfacesFor(typeArgument))
                {
                    constraintBaseline.BaseTypeOrInterfaces.Add(TypeDescriptor.GetTypeNameFor(interfaceType));
                }

                constraintBaseline.ParameterName = typeArgument.Name;
                constraintBaseline.New = (typeArgument.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint) == GenericParameterAttributes.DefaultConstructorConstraint;
                constraintBaseline.Class = (typeArgument.GenericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint) == GenericParameterAttributes.ReferenceTypeConstraint;
                constraintBaseline.Struct = (typeArgument.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) == GenericParameterAttributes.NotNullableValueTypeConstraint;

                if (constraintBaseline.New || constraintBaseline.Class || constraintBaseline.Struct || constraintBaseline.BaseTypeOrInterfaces.Count > 0)
                {
                    yield return constraintBaseline;
                }
            }
        }

        private MemberDescriptor GenerateMemberBaseline(TypeInfo type, MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Constructor:
                    var ctor = (ConstructorInfo)member;
                    var constructorBaseline = new MemberDescriptor();
                    constructorBaseline.Kind = MemberKind.Constructor;
                    constructorBaseline.Visibility = ctor.IsPublic ? ApiElementVisibility.Public :
                        ctor.IsFamilyOrAssembly ? ApiElementVisibility.ProtectedInternal :
                        ctor.IsFamily ? ApiElementVisibility.Protected :
                        ctor.IsPrivate ? ApiElementVisibility.Private : ApiElementVisibility.Internal;

                    constructorBaseline.Name = MemberDescriptor.GetMemberNameFor(ctor);
                    foreach (var parameter in ctor.GetParameters())
                    {
                        var parameterBaseline = GenerateParameterBaseline(parameter);
                        constructorBaseline.Parameters.Add(parameterBaseline);
                    }

                    return constructorBaseline;
                case MemberTypes.Method:
                    var name = member.Name;
                    var method = (MethodInfo)member;
                    var methodBaseline = new MemberDescriptor();

                    methodBaseline.Kind = MemberKind.Method;

                    methodBaseline.Visibility = method.IsPublic ? ApiElementVisibility.Public :
                        method.IsFamilyOrAssembly ? ApiElementVisibility.ProtectedInternal :
                        method.IsFamily ? ApiElementVisibility.Protected :
                        method.IsPrivate ? ApiElementVisibility.Private : ApiElementVisibility.Internal;

                    methodBaseline.ExplicitInterface = GetInterfaceImplementation(method, explicitImplementation: true);
                    methodBaseline.ImplementedInterface = methodBaseline.ExplicitInterface ?? GetInterfaceImplementation(method, explicitImplementation: false);
                    methodBaseline.Name = MemberDescriptor.GetMemberNameFor(method);

                    if (method.IsGenericMethod)
                    {
                        var constraints = GetGenericConstraintsFor(method.GetGenericArguments().Select(t => t.GetTypeInfo()));
                        foreach (var constraint in constraints)
                        {
                            methodBaseline.GenericConstraints.Add(constraint);
                        }
                    }

                    methodBaseline.Static = method.IsStatic;
                    methodBaseline.Sealed = method.IsFinal;
                    methodBaseline.Virtual = method.IsVirtual;
                    methodBaseline.Override = method.IsVirtual && method.GetBaseDefinition() != method;
                    methodBaseline.Abstract = method.IsAbstract;
                    methodBaseline.New = !method.IsAbstract && !method.IsVirtual && method.IsHideBySig &&
                        method.DeclaringType.GetMember(method.Name).OfType<MethodInfo>()
                        .Where(m => SameSignature(m, method)).Count() > 1;
                    methodBaseline.Extension = method.IsDefined(typeof(ExtensionAttribute), false);

                    foreach (var parameter in method.GetParameters())
                    {
                        var parameterBaseline = GenerateParameterBaseline(parameter);
                        methodBaseline.Parameters.Add(parameterBaseline);
                    }

                    methodBaseline.ReturnType = TypeDescriptor.GetTypeNameFor(method.ReturnType.GetTypeInfo());

                    return methodBaseline;
                case MemberTypes.Field:
                    var field = (FieldInfo)member;
                    if (type.IsEnum && !field.IsLiteral)
                    {
                        // Skip storage for enumerations.
                        return null;
                    }

                    var fieldBaseline = new MemberDescriptor();

                    fieldBaseline.Visibility = field.IsPublic ? ApiElementVisibility.Public :
                        field.IsFamilyOrAssembly ? ApiElementVisibility.ProtectedInternal :
                        field.IsFamily ? ApiElementVisibility.Protected :
                        field.IsPrivate ? ApiElementVisibility.Private : ApiElementVisibility.Internal;

                    fieldBaseline.Kind = MemberKind.Field;
                    fieldBaseline.Name = field.Name;

                    if (type.IsEnum || field.IsLiteral)
                    {
                        fieldBaseline.Literal = FormatLiteralValue(field.GetRawConstantValue(), field.FieldType);
                    }

                    if (type.IsEnum)
                    {
                        fieldBaseline.Visibility = null;
                    }
                    else
                    {
                        fieldBaseline.Constant = field.IsLiteral;
                        fieldBaseline.Static = field.IsStatic;
                        fieldBaseline.ReadOnly = field.IsInitOnly;
                        fieldBaseline.ReturnType = TypeDescriptor.GetTypeNameFor(field.FieldType.GetTypeInfo());
                    }

                    return fieldBaseline;
                case MemberTypes.Event:
                case MemberTypes.Property:
                case MemberTypes.NestedType:
                    // All these cases are covered by the methods they implicitly define on the class
                    // (Properties and Events) and when we enumerate all the types in an assembly (Nested types).
                    return null;
                case MemberTypes.TypeInfo:
                // There should not be any member passsed into this method that is not a top level type.
                case MemberTypes.Custom:
                // We don't know about custom member types, so better throw if we find something we don't understand.
                case MemberTypes.All:
                    throw new InvalidOperationException($"'{type.MemberType}' [{member}] is not supported.");
                default:
                    return null;
            }
        }

        public static ApiListing LoadFrom(string path)
        {
            return JsonConvert.DeserializeObject<ApiListing>(File.ReadAllText(path));
        }

        private string GetInterfaceImplementation(MethodInfo method, bool explicitImplementation)
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

        private bool SameSignature(MethodInfo candidate, MethodInfo method)
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

            for (int i = 0; i < candidateParameters.Length; i++)
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

        private ParameterDescriptor GenerateParameterBaseline(ParameterInfo parameter)
        {
            return new ParameterDescriptor
            {
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
                if (elementTypeInfo.IsValueType)
                {
                    return $"default({TypeDescriptor.GetTypeNameFor(elementTypeInfo)})";
                }

                return "null";
            }

            if (elementType == typeof(string))
            {
                return $"\"{rawDefaultValue}\"";
            }

            if (elementType == typeof(char))
            {
                return $"'{rawDefaultValue}'";
            }

            if (rawDefaultValue.GetType() == typeof(bool) ||
                rawDefaultValue.GetType() == typeof(byte) ||
                rawDefaultValue.GetType() == typeof(sbyte) ||
                rawDefaultValue.GetType() == typeof(short) ||
                rawDefaultValue.GetType() == typeof(ushort) ||
                rawDefaultValue.GetType() == typeof(int) ||
                rawDefaultValue.GetType() == typeof(uint) ||
                rawDefaultValue.GetType() == typeof(long) ||
                rawDefaultValue.GetType() == typeof(ulong) ||
                rawDefaultValue.GetType() == typeof(double) ||
                rawDefaultValue.GetType() == typeof(float) ||
                rawDefaultValue.GetType() == typeof(decimal))
            {
                return rawDefaultValue.ToString();
            }

            throw new InvalidOperationException("Unsupported default value type");
        }
    }
}