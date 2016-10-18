using System;
using System.Collections.Generic;
using System.IO;
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
            var ApiListingDocument = generator.GenerateApiListing();
            return JObject.FromObject(ApiListingDocument);
        }

        public ApiListing GenerateApiListing()
        {
            var types = _assembly.DefinedTypes;

            var document = new ApiListing();
            document.AssemblyIdentity = _assembly.GetName().ToString();

            foreach (var type in _assembly.DefinedTypes.Where(type => _filters.All(filter => !filter(type))))
            {
                var ApiListingType = GeneratetypeDescriptor(type);
                document.Types.Add(ApiListingType);
            }

            return document;
        }

        private TypeDescriptor GeneratetypeDescriptor(TypeInfo type)
        {
            var typeDescriptor = new TypeDescriptor();

            typeDescriptor.Name = TypeDescriptor.GetTypeNameFor(type);

            typeDescriptor.Kind = type.IsInterface ? TypeKind.Interface :
                !type.IsValueType ? TypeKind.Class :
                type.IsEnum ? TypeKind.Enumeration :
                TypeKind.Struct;

            typeDescriptor.Visibility = type.IsPublic || type.IsNestedPublic ? ApiElementVisibility.Public :
                type.IsNestedFamORAssem ? ApiElementVisibility.ProtectedInternal :
                type.IsNestedFamily ? ApiElementVisibility.Protected :
                type.IsNestedPrivate ? ApiElementVisibility.Private :
                ApiElementVisibility.Internal;

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

            if (type.ImplementedInterfaces?.Count() > 0)
            {
                var interfaces = TypeDescriptor.GetImplementedInterfacesFor(type);
                foreach (var @interface in interfaces.Select(i => TypeDescriptor.GetTypeNameFor(i)))
                {
                    typeDescriptor.ImplementedInterfaces.Add(@interface);
                }
            }

            if (type.IsGenericType)
            {
                var constraints = GetGenericConstraintsFor(type.GetGenericArguments().Select(t => t.GetTypeInfo()));
                foreach (var constraint in constraints)
                {
                    typeDescriptor.GenericConstraints.Add(constraint);
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
                    typeDescriptor.Members.Add(memberApiListing);
                }
            }

            return typeDescriptor;
        }

        private static IEnumerable<GenericConstraintDescriptor> GetGenericConstraintsFor(IEnumerable<TypeInfo> genericArguments)
        {
            foreach (var typeArgument in genericArguments)
            {
                var constraintDescriptor = new GenericConstraintDescriptor();

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
                constraintDescriptor.New = (typeArgument.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint) == GenericParameterAttributes.DefaultConstructorConstraint;
                constraintDescriptor.Class = (typeArgument.GenericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint) == GenericParameterAttributes.ReferenceTypeConstraint;
                constraintDescriptor.Struct = (typeArgument.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) == GenericParameterAttributes.NotNullableValueTypeConstraint;

                if (constraintDescriptor.New || constraintDescriptor.Class || constraintDescriptor.Struct || constraintDescriptor.BaseTypeOrInterfaces.Count > 0)
                {
                    yield return constraintDescriptor;
                }
            }
        }

        private MemberDescriptor GenerateMemberApiListing(TypeInfo type, MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Constructor:
                    var ctor = (ConstructorInfo)member;
                    var constructorDescriptor = new MemberDescriptor();
                    constructorDescriptor.Kind = MemberKind.Constructor;
                    constructorDescriptor.Visibility = ctor.IsPublic ? ApiElementVisibility.Public :
                        ctor.IsFamilyOrAssembly ? ApiElementVisibility.ProtectedInternal :
                        ctor.IsFamily ? ApiElementVisibility.Protected :
                        ctor.IsPrivate ? ApiElementVisibility.Private : ApiElementVisibility.Internal;

                    constructorDescriptor.Name = MemberDescriptor.GetMemberNameFor(ctor);
                    foreach (var parameter in ctor.GetParameters())
                    {
                        var parameterDescriptor = GenerateparameterDescriptor(parameter);
                        constructorDescriptor.Parameters.Add(parameterDescriptor);
                    }

                    return constructorDescriptor;
                case MemberTypes.Method:
                    var name = member.Name;
                    var method = (MethodInfo)member;
                    var methodDescriptor = new MemberDescriptor();

                    methodDescriptor.Kind = MemberKind.Method;

                    methodDescriptor.Visibility = method.IsPublic ? ApiElementVisibility.Public :
                        method.IsFamilyOrAssembly ? ApiElementVisibility.ProtectedInternal :
                        method.IsFamily ? ApiElementVisibility.Protected :
                        method.IsPrivate ? ApiElementVisibility.Private : ApiElementVisibility.Internal;

                    if (!type.IsInterface)
                    {
                        methodDescriptor.ExplicitInterface = GetInterfaceImplementation(method, explicitImplementation: true);
                        methodDescriptor.ImplementedInterface = methodDescriptor.ExplicitInterface ?? GetInterfaceImplementation(method, explicitImplementation: false);
                    }

                    methodDescriptor.Name = MemberDescriptor.GetMemberNameFor(method);

                    if (method.IsGenericMethod)
                    {
                        var constraints = GetGenericConstraintsFor(method.GetGenericArguments().Select(t => t.GetTypeInfo()));
                        foreach (var constraint in constraints)
                        {
                            methodDescriptor.GenericConstraints.Add(constraint);
                        }
                    }

                    methodDescriptor.Static = method.IsStatic;
                    methodDescriptor.Sealed = method.IsFinal;
                    methodDescriptor.Virtual = method.IsVirtual;
                    methodDescriptor.Override = method.IsVirtual && method.GetBaseDefinition() != method;
                    methodDescriptor.Abstract = method.IsAbstract;
                    methodDescriptor.New = !method.IsAbstract && !method.IsVirtual && method.IsHideBySig &&
                        method.DeclaringType.GetMember(method.Name).OfType<MethodInfo>()
                        .Where(m => SameSignature(m, method)).Count() > 1;
                    methodDescriptor.Extension = method.IsDefined(typeof(ExtensionAttribute), false);

                    foreach (var parameter in method.GetParameters())
                    {
                        var parameterDescriptor = GenerateparameterDescriptor(parameter);
                        methodDescriptor.Parameters.Add(parameterDescriptor);
                    }

                    methodDescriptor.ReturnType = TypeDescriptor.GetTypeNameFor(method.ReturnType.GetTypeInfo());

                    return methodDescriptor;
                case MemberTypes.Field:
                    var field = (FieldInfo)member;
                    if (type.IsEnum && !field.IsLiteral)
                    {
                        // Skip storage for enumerations.
                        return null;
                    }

                    var fieldDescriptor = new MemberDescriptor();

                    fieldDescriptor.Visibility = field.IsPublic ? ApiElementVisibility.Public :
                        field.IsFamilyOrAssembly ? ApiElementVisibility.ProtectedInternal :
                        field.IsFamily ? ApiElementVisibility.Protected :
                        field.IsPrivate ? ApiElementVisibility.Private : ApiElementVisibility.Internal;

                    fieldDescriptor.Kind = MemberKind.Field;
                    fieldDescriptor.Name = field.Name;

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
                // There should not be any member passsed into this method that is not a top level type.
                case MemberTypes.Custom:
                // We don't know about custom member types, so better throw if we find something we don't understand.
                case MemberTypes.All:
                    throw new InvalidOperationException($"'{type.MemberType}' [{member}] is not supported.");
                default:
                    return null;
            }
        }

        public static ApiListing LoadFrom(string json, IEnumerable<Func<ApiElement, bool>> oldApiListingFilters)
        {
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

        private ParameterDescriptor GenerateparameterDescriptor(ParameterInfo parameter)
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