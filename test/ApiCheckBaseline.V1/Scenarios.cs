using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Scenarios
{
    public class ApiCheckApiListingV1
    {
    }
    public class BasicClass
    {
    }

    public class DerivedClass : BasicClass
    {
    }

    public struct BasicStruct
    {
    }

    public interface IBasicInterface
    {
    }

    public interface IComplexInterface : IBasicInterface
    {
    }

    public interface IMultipleLevelInterface : IComplexInterface
    {
    }

    public interface IBasicInterfaceForClass { }

    public class ClassImplementingInterface : IBasicInterfaceForClass
    {
    }

    public class ClassDerivingClassImplementingInterface : ClassImplementingInterface
    {
    }

    public class NestedTypesClass
    {
        public class PublicNestedClass
        {
        }

        protected class ProtectedNestedClass
        {
        }

        protected internal class ProtectedInternalNestedClass
        {
        }

        internal class InternalNestedClass
        {
        }

        private class PrivateNestedClass
        {
        }

        public interface PublicNestedInterface
        {
        }

        public class IntermediateNestedClass
        {
            public class MultiLevelNestedClass
            {
            }
        }
    }

    public abstract class HierarchyAbstractClass
    {
        public abstract void AbstractVoidMethod();

        public virtual void VirtualVoidMethod()
        {
        }

        public void NonVirtualNonAbstractMethod()
        {
        }
    }

    public class HierarchyDerivedClass : HierarchyAbstractClass
    {
        public override void AbstractVoidMethod()
        {
        }

        public override void VirtualVoidMethod()
        {
        }

        public new void NonVirtualNonAbstractMethod()
        {
        }
    }

    public sealed class SealedDerivedClass : HierarchyAbstractClass
    {
        public sealed override void AbstractVoidMethod()
        {
        }

        public sealed override void VirtualVoidMethod()
        {
        }
    }

    public static class StaticClass
    {
    }

    public static class ExtensionMethodsClass
    {
        public static string ExtensionMethod(this string self)
        {
            return null;
        }
    }

    public interface IInterfaceForExplicitImplementation
    {
        void ExplicitImplementationMethod();
    }

    public class ExplicitImplementationClass : IInterfaceForExplicitImplementation
    {
        void IInterfaceForExplicitImplementation.ExplicitImplementationMethod()
        {
            throw new NotImplementedException();
        }
    }

    public interface IBasicInterfaceForInterfaceReimplementation
    {
        void A();
    }

    public class OriginalClassImplementingInterface : IBasicInterfaceForInterfaceReimplementation
    {
        public void A()
        {
            throw new NotImplementedException();
        }
    }

    public class ClassDerivingClassReimplementingInterface : OriginalClassImplementingInterface, IBasicInterfaceForInterfaceReimplementation
    {
        void IBasicInterfaceForInterfaceReimplementation.A()
        {
        }
    }

    public class ExplicitlyImplementedInterfaceBaseClass : IBasicInterfaceForInterfaceReimplementation
    {
        void IBasicInterfaceForInterfaceReimplementation.A()
        {
            throw new NotImplementedException();
        }
    }

    public class ClassReimplementingInterfaceFromBaseClassWithExplicitImplementedInterface : ExplicitlyImplementedInterfaceBaseClass, IBasicInterfaceForInterfaceReimplementation
    {
        public void A()
        {
        }
    }

    public class GenericType<TGenericArgument>
    {
    }

    public class ClosedGenericType : GenericType<int>
    {
    }

    public interface IMultipleGenericTypes<TFirst, TSecond>
    {
    }

    public class SemiClosedGenericClass<TSecond> : IMultipleGenericTypes<string, TSecond>
    {
    }

    public interface IGenericInterfaceWithConstraints<TClassNew>
        where TClassNew : class, new()
    {
    }

    public interface IGenericInterfaceWithStructConstraint<TStruct>
    where TStruct : struct
    {
    }

    public class BaseClassForConstraint
    {
    }

    public class DerivedClassForConstraint : BaseClassForConstraint
    {
    }

    public interface IInterfaceForConstraint
    {
    }

    public class ImplementedInterfaceForConstraint : BaseClassForConstraint, IInterfaceForConstraint
    {
    }

    public interface IGenericWithMultipleTypesAndConstraints<TKey, TValue, TDictionary>
        where TKey : BaseClassForConstraint
        where TValue : BaseClassForConstraint, IInterfaceForConstraint, new()
        where TDictionary : IDictionary<TKey, TValue>, new()
    {
    }

    public class ClassImplementingGenericWithMultipleTypesAndConstraints<TKey, TValue, TDictionary> :
        IGenericWithMultipleTypesAndConstraints<TKey, TValue, TDictionary>
        where TKey : BaseClassForConstraint
        where TValue : BaseClassForConstraint, IInterfaceForConstraint, new()
        where TDictionary : IDictionary<TKey, TValue>, new()
    {
    }

    public class MethodTypesClass
    {
        public void ParameterlessVoidReturningMethod()
        {
        }

        protected int ProtectedIntReturningMethod()
        {
            return 0;
        }

        protected internal string ProtectedInternalStringReturningMethodWithStringParameter(string stringParameter)
        {
            return null;
        }

        internal MethodTypesClass InternalClassReturningMethodWithOptionalStringParameter(string defaultParameter = "hello")
        {
            return null;
        }

        private bool PrivateBoolReturningMethodWithOptionalCharParameter(char charParameter = 'c')
        {
            return false;
        }

        public decimal PublicDecimalReturningMethodWithAllDefaultParameterTypes(
            MethodTypesClass methodTypes = null,
            string nullString = null,
            string nonNullString = "string",
            char charDefault = 'c',
            bool boolDefault = false,
            byte byteDefault = 3,
            sbyte sbyteDefault = 5,
            short shortDefault = 7,
            ushort ushortDefault = 9,
            int intDefault = 11,
            uint uintDefault = 13,
            long longDefault = 15,
            ulong ulongDefault = 17,
            double doubleDefault = 19,
            float floatDefault = 21.0f,
            decimal decimalDefault = 23.0M,
            CancellationToken cancellation = default(CancellationToken))
        {
            return 1.0M;
        }

        public void MethodWithParametersInDifferentDirections(string inParameter, out bool outParameter, ref int refParameter)
        {
            outParameter = false;
            refParameter = 0;
        }

        public void VoidReturningMethodWithParamsArgument(params string[] stringParams)
        {
        }

        public static void StaticVoidReturningMethod()
        {
        }

        public TClassType GenericMethod<TClassType>(TClassType typeClassArgument)
        {
            return default(TClassType);
        }

        public TClassType GenericMethodWithMultipleGenericParameters<TClassType, TSecond>(TClassType typeClassArgument)
        {
            return default(TClassType);
        }

        public void MethodWithArrayParameter(Expression<Func<int, bool>>[] arrayExpression)
        {
        }
    }

    public class GenericClassForGenericMethods<TFirst, TSecond>
    {
        public virtual void MethodWithGenericArgumentsFromClass(TFirst first, TSecond second)
        {
        }
    }

    public class PartiallyClosedClass<TFirst> : GenericClassForGenericMethods<TFirst, string>
    {
        public override void MethodWithGenericArgumentsFromClass(TFirst first, string second)
        {
        }
    }

    public class GenericMethodsWithConstraintsClass
    {
        public void GenericMethod<TClassNew>(TClassNew argument) where TClassNew : class, new()
        {
        }

        public void GenericMethodWithStructParameter<TStruct>(TStruct argument) where TStruct : struct
        {
        }

        public void GenericMethodWithClassAndInterfacesConstraint<TExtend>(TExtend argument) where TExtend : Collection<int>, IDictionary<string, int>
        {
        }
    }

    public class ClassWithFields
    {
        public int PublicField;
        public readonly bool ReadonlyField;
        public const char ConstantField = 'c';
        public static string StaticField = "Static";
        public static readonly string StaticReadonlyField = "StaticReadonly";
    }

    public class ClassWithPropertiesAndEvents
    {
        public string GetAndSetProperty { get; set; }
        public event Action<int> IntEvent;
    }

    public enum CanonicalEnumeration
    {
        FirstValue,
        SecondValue
    }

    public enum LongEnumeration : long
    {
        FirstValue,
        ExplicitValue = 5,
        ValueAfterExplicit
    }

    public class ClassWithConstructors
    {
        // Parameterless
        public ClassWithConstructors()
        {
        }

        // Different visibility and a parameter
        public ClassWithConstructors(bool parameter)
        {
        }

        public ClassWithConstructors(string parameter = "default", params int[] values)
        {
        }
    }

    public class ClassWithoutImplicitParameterlessConstructor
    {
        public ClassWithoutImplicitParameterlessConstructor(string parameter)
        {
        }
    }
}

namespace Scenarios.Internal
{
    public class ExcludedType
    {
    }
}