using System;
using System.Linq;
using System.Reflection;
using ApiCheck.Description;
using ApiCheckApiListing.V2;
using Scenarios;
using Xunit;

namespace ApiCheck.Test
{
    public class ApiListingGenerationTests
    {
        public Assembly V1Assembly => typeof(ApiCheckApiListingV1).GetTypeInfo().Assembly;
        public Assembly V2Assembly => typeof(ApiCheckApiListingV2).GetTypeInfo().Assembly;

        [Fact]
        public void DetectsClasses()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            Assert.Single(report.Types, t => t.Id == "public class Scenarios.BasicClass");
        }

        [Fact]
        public void DetectsStructs()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            Assert.Single(report.Types, t => t.Id == "public struct Scenarios.BasicStruct");
        }

        [Fact]
        public void DetectsDerivedClasses()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            Assert.Single(report.Types, t => t.Id == "public class Scenarios.DerivedClass : Scenarios.BasicClass");
        }

        [Fact]
        public void DetectsBasicInterface()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            Assert.Single(report.Types, t => t.Id == "public interface Scenarios.IBasicInterface");
        }

        [Fact]
        public void DetectsComplexInterface()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            Assert.Single(report.Types, t => t.Id == "public interface Scenarios.IComplexInterface : Scenarios.IBasicInterface");
        }

        [Fact]
        public void DetectsInterfacesThatImplicitlyImplementOtherInterfaces()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            Assert.Single(report.Types, t => t.Id == "public interface Scenarios.IMultipleLevelInterface : Scenarios.IComplexInterface");
        }

        [Fact]
        public void DetectsClassImplementingInterface()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            Assert.Single(report.Types, t => t.Id == "public class Scenarios.ClassImplementingInterface : Scenarios.IBasicInterfaceForClass");
        }

        [Fact]
        public void DetectsClassDerivingClassImplementingInterface()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            Assert.Single(report.Types, t => t.Id == "public class Scenarios.ClassDerivingClassImplementingInterface : Scenarios.ClassImplementingInterface");
        }

        [Fact]
        public void ParameterlessVoidReturningMethod()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.MethodTypesClass");
            var method = Assert.Single(type.Members, m => m.Id == "public System.Void ParameterlessVoidReturningMethod()");
        }

        [Fact]
        public void DetectsProtectedIntReturningMethod()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.MethodTypesClass");
            var method = Assert.Single(type.Members, m => m.Id == "protected System.Int32 ProtectedIntReturningMethod()");
        }

        [Fact]
        public void ProtectedInternalStringReturningMethodWithStringParameter()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.MethodTypesClass");
            Assert.False(type.Members.Any(m => m.Id == "protected internal System.String ProtectedInternalStringReturningMethodWithStringParameter(System.String stringParameter)"));
        }

        [Fact]
        public void PublicClassReturningMethodWithOptionalStringParameter()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.MethodTypesClass");
            var method = Assert.Single(type.Members, m => m.Id == "public Scenarios.MethodTypesClass PublicClassReturningMethodWithOptionalStringParameter(System.String defaultParameter = \"hello\")");
        }

        [Fact]
        public void PrivateBoolReturningMethodWithOptionalCharParameter()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.MethodTypesClass");
            Assert.False(type.Members.Any(m => m.Id == "private System.Boolean PrivateBoolReturningMethodWithOptionalCharParameter(System.Char charParameter = 'c')"));
        }

        [Fact]
        public void PublicDecimalReturningMethodWithAllDefaultParameterTypes()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);
            var parameters = string.Join(", ",
                "Scenarios.MethodTypesClass methodTypes = null",
                "System.String nullString = null",
                @"System.String nonNullString = ""string""",
                "System.Char charDefault = 'c'",
                "System.Boolean boolDefault = False",
                "System.Byte byteDefault = 3",
                "System.SByte sbyteDefault = 5",
                "System.Int16 shortDefault = 7",
                "System.UInt16 ushortDefault = 9",
                "System.Int32 intDefault = 11",
                "System.UInt32 uintDefault = 13",
                "System.Int64 longDefault = 15",
                "System.UInt64 ulongDefault = 17",
                "System.Double doubleDefault = 19",
                "System.Single floatDefault = 21",
                "System.Decimal decimalDefault = 23.0",
                "System.Threading.CancellationToken cancellation = default(System.Threading.CancellationToken)");

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.MethodTypesClass");
            var method = Assert.Single(type.Members, m => m.Id == $"public System.Decimal PublicDecimalReturningMethodWithAllDefaultParameterTypes({parameters})");
        }

        [Fact]
        public void DetectsVoidReturningMethodWithParamsArgument()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.MethodTypesClass");
            var method = Assert.Single(type.Members, m => m.Id == "public System.Void VoidReturningMethodWithParamsArgument(params System.String[] stringParams)");
        }

        [Fact]
        public void DetectsStaticVoidReturningMethod()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.MethodTypesClass");
            var method = Assert.Single(type.Members, m => m.Id == "public static System.Void StaticVoidReturningMethod()");
        }

        [Fact]
        public void DetectsPublicNestedClass()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.NestedTypesClass+PublicNestedClass");
        }

        [Fact]
        public void DetectsProtectedNestedClass()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "protected class Scenarios.NestedTypesClass+ProtectedNestedClass");
        }

        [Fact]
        public void DetectsProtectedInternalNestedClass()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            Assert.Single(report.Types, t => t.Id == "protected class Scenarios.NestedTypesClass+ProtectedInternalNestedClass");
        }

        [Fact]
        public void DetectsInternalNestedClass()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            Assert.False(report.Types.Any(t => t.Id == "internal class Scenarios.NestedTypesClass+InternalNestedClass"));
        }

        [Fact]
        public void DetectsPrivateNestedClass()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            Assert.False(report.Types.Any(t => t.Id == "private class Scenarios.NestedTypesClass+PrivateNestedClass"));
        }

        [Fact]
        public void DetectsPublicNestedInterface()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public interface Scenarios.NestedTypesClass+PublicNestedInterface");
        }

        [Fact]
        public void DetectsMultipleLevelsNestedClass()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.NestedTypesClass+IntermediateNestedClass+MultiLevelNestedClass");
        }

        [Fact]
        public void DetectsAbstractClass()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public abstract class Scenarios.HierarchyAbstractClass");
        }

        [Fact]
        public void DetectsAbstractVoidMethod()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public abstract class Scenarios.HierarchyAbstractClass");
            var method = Assert.Single(type.Members, m => m.Id == "public abstract System.Void AbstractVoidMethod()");
        }

        [Fact]
        public void DetectsVirtualVoidMethod()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public abstract class Scenarios.HierarchyAbstractClass");
            var method = Assert.Single(type.Members, m => m.Id == "public virtual System.Void VirtualVoidMethod()");
        }

        [Fact]
        public void DetectsAbstractImplementationMethod()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.HierarchyDerivedClass : Scenarios.HierarchyAbstractClass");
            var method = Assert.Single(type.Members, m => m.Id == "public override System.Void AbstractVoidMethod()");
        }

        [Fact]
        public void DetectsVirtualOverrideMethod()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.HierarchyDerivedClass : Scenarios.HierarchyAbstractClass");
            var method = Assert.Single(type.Members, m => m.Id == "public override System.Void VirtualVoidMethod()");
        }

        [Fact]
        public void DetectsNewMethod()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.HierarchyDerivedClass : Scenarios.HierarchyAbstractClass");
            var method = Assert.Single(type.Members, m => m.Id == "public new System.Void NonVirtualNonAbstractMethod()");
        }

        [Fact]
        public void DetectsSealedClasses()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public sealed class Scenarios.SealedDerivedClass : Scenarios.HierarchyAbstractClass");
        }

        [Fact]
        public void DetectsSealedAbstractImplementationMethod()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public sealed class Scenarios.SealedDerivedClass : Scenarios.HierarchyAbstractClass");
            var method = Assert.Single(type.Members, m => m.Id == "public sealed override System.Void AbstractVoidMethod()");
        }

        [Fact]
        public void DetectsSealedVirtualOverrideMethod()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public sealed class Scenarios.SealedDerivedClass : Scenarios.HierarchyAbstractClass");
            var method = Assert.Single(type.Members, m => m.Id == "public sealed override System.Void VirtualVoidMethod()");
        }

        [Fact]
        public void DetectsStaticClasses()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public static class Scenarios.StaticClass");
        }

        [Fact]
        public void DetectsExtensionMethods()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public static class Scenarios.ExtensionMethodsClass");
            var method = Assert.Single(type.Members, m => m.Id == "public static System.String ExtensionMethod(this System.String self)");
        }

        [Fact]
        public void DetectsExplicitlyImplementedInterfaces()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.ExplicitImplementationClass : Scenarios.IInterfaceForExplicitImplementation");
            Assert.False(type.Members.Any(m => m.Id == "System.Void Scenarios.IInterfaceForExplicitImplementation.ExplicitImplementationMethod()"));
        }

        [Fact]
        public void DetectsReimplementedInterfaceExplicitly()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.ClassDerivingClassReimplementingInterface : Scenarios.OriginalClassImplementingInterface, Scenarios.IBasicInterfaceForInterfaceReimplementation");
            Assert.False(type.Members.Any(m => m.Id == "System.Void Scenarios.IBasicInterfaceForInterfaceReimplementation.A()"));
        }

        [Fact]
        public void DetectsReimplementedInterfaceImplicitly()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.ClassReimplementingInterfaceFromBaseClassWithExplicitImplementedInterface : Scenarios.ExplicitlyImplementedInterfaceBaseClass, Scenarios.IBasicInterfaceForInterfaceReimplementation");
            var method = Assert.Single(type.Members, m => m.Id == "public System.Void A()");
        }

        [Fact]
        public void DetectsGenericTypes()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.GenericType<TGenericArgument>");
        }

        [Fact]
        public void DetectsClosedGenericTypes()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.ClosedGenericType : Scenarios.GenericType<System.Int32>");
        }

        [Fact]
        public void DetectsMultipleGenericTypes()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public interface Scenarios.IMultipleGenericTypes<TFirst, TSecond>");
        }

        [Fact]
        public void DetectsSemiClosedGenericTypes()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.SemiClosedGenericClass<TSecond> : Scenarios.IMultipleGenericTypes<System.String, TSecond>");
        }

        [Fact]
        public void DetectsClassNewGenericTypeConstraints()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public interface Scenarios.IGenericInterfaceWithConstraints<TClassNew> where TClassNew : class, new()");
        }

        [Fact]
        public void DetectsStructGenericTypeConstraints()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            var type = Assert.Single(report.Types, t => t.Id == "public interface Scenarios.IGenericInterfaceWithStructConstraint<TStruct> where TStruct : struct");
        }

        [Fact]
        public void DetectsGenericInterfaceWithMultipleInterfaceConstraints()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public interface Scenarios.IGenericWithMultipleTypesAndConstraints<TKey, TValue, TDictionary> where TKey : Scenarios.BaseClassForConstraint where TValue : Scenarios.BaseClassForConstraint, Scenarios.IInterfaceForConstraint, new() where TDictionary : System.Collections.Generic.IDictionary<TKey, TValue>, new()");
        }

        [Fact]
        public void DetectsGenericClassImplementingGenericInterfaceWithMultipleInterfaceConstraints()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.ClassImplementingGenericWithMultipleTypesAndConstraints<TKey, TValue, TDictionary> : Scenarios.IGenericWithMultipleTypesAndConstraints<TKey, TValue, TDictionary> where TKey : Scenarios.BaseClassForConstraint where TValue : Scenarios.BaseClassForConstraint, Scenarios.IInterfaceForConstraint, new() where TDictionary : System.Collections.Generic.IDictionary<TKey, TValue>, new()");
        }

        [Fact]
        public void DetectsGenericMethod()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.MethodTypesClass");
            var method = Assert.Single(type.Members, m => m.Id == "public TClassType GenericMethod<TClassType>(TClassType typeClassArgument)");
        }

        [Fact]
        public void DetectsGenericMethodWithMultipleGenericArguments()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.MethodTypesClass");
            var method = Assert.Single(type.Members, m => m.Id == "public TClassType GenericMethodWithMultipleGenericParameters<TClassType, TSecond>(TClassType typeClassArgument)");
        }

        [Fact]
        public void DetectsMethodWithGenericArgumentsFromClass()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.GenericClassForGenericMethods<TFirst, TSecond>");
            var method = Assert.Single(type.Members, m => m.Id == "public virtual System.Void MethodWithGenericArgumentsFromClass(TFirst first, TSecond second)");
        }

        [Fact]
        public void DetectsMethodWithGenericArgumentsFromPartiallyClosedClass()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.PartiallyClosedClass<TFirst> : Scenarios.GenericClassForGenericMethods<TFirst, System.String>");
            var method = Assert.Single(type.Members, m => m.Id == "public override System.Void MethodWithGenericArgumentsFromClass(TFirst first, System.String second)");
        }

        [Fact]
        public void DetectsMethodWithGenericArgumentsAndConstraints()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.GenericMethodsWithConstraintsClass");
            var method = Assert.Single(type.Members, m => m.Id == "public System.Void GenericMethod<TClassNew>(TClassNew argument) where TClassNew : class, new()");
        }

        [Fact]
        public void DetectsMethodWithGenericArgumentsAndStructConstraint()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.GenericMethodsWithConstraintsClass");
            var method = Assert.Single(type.Members, m => m.Id == "public System.Void GenericMethodWithStructParameter<TStruct>(TStruct argument) where TStruct : struct");
        }

        [Fact]
        public void DetectsMethodWithGenericArgumentsAndTypeAndImplementedInterfacesConstraints()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.GenericMethodsWithConstraintsClass");
            var method = Assert.Single(type.Members, m => m.Id == "public System.Void GenericMethodWithClassAndInterfacesConstraint<TExtend>(TExtend argument) where TExtend : System.Collections.ObjectModel.Collection<System.Int32>, System.Collections.Generic.IDictionary<System.String, System.Int32>");
        }

        [Fact]
        public void DetectsPublicFields()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.ClassWithFields");
            var member = Assert.Single(type.Members, m => m.Id == "public System.Int32 PublicField");
        }

        [Fact]
        public void DetectsReadonlyFields()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.ClassWithFields");
            var member = Assert.Single(type.Members, m => m.Id == "public readonly System.Boolean ReadonlyField");
        }

        [Fact]
        public void DetectsConstantFields()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.ClassWithFields");
            var member = Assert.Single(type.Members, m => m.Id == "public const System.Char ConstantField = 'c'");
        }

        [Fact]
        public void DetectsStaticFields()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.ClassWithFields");
            var member = Assert.Single(type.Members, m => m.Id == "public static System.String StaticField");
        }

        [Fact]
        public void DetectsStaticReadonlyFields()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.ClassWithFields");
            var member = Assert.Single(type.Members, m => m.Id == "public static readonly System.String StaticReadonlyField");
        }

        [Fact]
        public void DetectsPropertiesAsMethods()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.ClassWithPropertiesAndEvents");
            var getter = Assert.Single(type.Members, m => m.Id == "public System.String get_GetAndSetProperty()");
            var setter = Assert.Single(type.Members, m => m.Id == "public System.Void set_GetAndSetProperty(System.String value)");
        }

        [Fact]
        public void DetectsEventsAsMethods()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.ClassWithPropertiesAndEvents");
            var getter = Assert.Single(type.Members, m => m.Id == "public System.Void add_IntEvent(System.Action<System.Int32> value)");
            var setter = Assert.Single(type.Members, m => m.Id == "public System.Void remove_IntEvent(System.Action<System.Int32> value)");
        }

        [Fact]
        public void DetectsEnumerations()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public enum Scenarios.CanonicalEnumeration");
        }

        [Fact]
        public void DetectsEnumerationsValues()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public enum Scenarios.CanonicalEnumeration");
            var firstValue = Assert.Single(type.Members, m => m.Id == "FirstValue = 0");
            var secondValue = Assert.Single(type.Members, m => m.Id == "SecondValue = 1");
        }

        [Fact]
        public void DetectsEnumerationsWithDifferentSizes()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public enum Scenarios.LongEnumeration : System.Int64");
            var firstValue = Assert.Single(type.Members, m => m.Id == "FirstValue = 0");
            var secondValue = Assert.Single(type.Members, m => m.Id == "ExplicitValue = 5");
            var thirdValue = Assert.Single(type.Members, m => m.Id == "ValueAfterExplicit = 6");
        }

        [Fact]
        public void DetectsConstructors()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.ClassWithConstructors");
            var firstValue = Assert.Single(type.Members, m => m.Id == "public .ctor()");
            var secondValue = Assert.Single(type.Members, m => m.Id == "public .ctor(System.Boolean parameter)");
            var thirdValue = Assert.Single(type.Members, m => m.Id == "public .ctor(System.String parameter = \"default\", params System.Int32[] values)");
        }

        [Fact]
        public void DetectsExplicitConstructorWithParameters()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.ClassWithoutImplicitParameterlessConstructor");
            var secondValue = Assert.Single(type.Members, m => m.Id == "public .ctor(System.String parameter)");
        }

        [Fact]
        public void DetectsMethodParameterDirections()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.MethodTypesClass");
            var secondValue = Assert.Single(type.Members, m => m.Id == "public System.Void MethodWithParametersInDifferentDirections(System.String inParameter, out System.Boolean outParameter, ref System.Int32 refParameter)");
        }

        [Fact]
        public void DetectsArrayParameters()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);

            var type = Assert.Single(report.Types, t => t.Id == "public class Scenarios.MethodTypesClass");
            var secondValue = Assert.Single(type.Members, m => m.Id == "public System.Void MethodWithArrayParameter(System.Linq.Expressions.Expression<System.Func<System.Int32, System.Boolean>>[] arrayExpression)");
        }

        [Fact]
        public void FiltersNonPublicOrProtectedElements()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            foreach (var type in report.Types)
            {
                var typeVisibility = type.Visibility;
                Assert.True(typeVisibility == ApiElementVisibility.Public ||
                    typeVisibility == ApiElementVisibility.Protected);
                Assert.NotNull(type.Members);

                foreach (var member in type.Members)
                {
                    var memberVisibility = member.Visibility;
                    Assert.True(memberVisibility == null ||
                        memberVisibility == ApiElementVisibility.Public ||
                        memberVisibility == ApiElementVisibility.Protected);
                }
            }
        }

        [Fact]
        public void FiltersMembersOnTheInternalNamespace()
        {
            // Arrange
            var generator = CreateGenerator(V1Assembly, ApiListingFilters.InternalNamespaceTypes);

            // Act
            var report = generator.GenerateApiListing();

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            Assert.DoesNotContain(report.Types, type => type.Name == "Scenarios.Internal.ExcludedType");
        }

        [Fact]
        public void LoadFromFiltersMembersOnTheInternalNamespace()
        {
            // Arrange
            var serialized = @"{
  ""AssemblyIdentity"": ""Test"",
  ""Types"": [
    {
      ""Name"": ""Scenarios.Internal.ExcludedType""
    }
  ]
}";

            // Act
            var report = ApiListingGenerator.LoadFrom(serialized, new Func<ApiElement, bool>[] { ApiListingFilters.InternalNamespaceTypes });

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.Types);
            Assert.Empty(report.Types);
        }

        private ApiListingGenerator CreateGenerator(Assembly assembly, params Func<MemberInfo, bool>[] filters)
        {
            filters = filters ?? new Func<MemberInfo, bool>[] { };
            return new ApiListingGenerator(assembly, filters);
        }
    }
}
