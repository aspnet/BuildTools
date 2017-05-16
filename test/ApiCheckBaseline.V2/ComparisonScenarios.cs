// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

// V2
namespace ComparisonScenarios
{
    internal class PublicToInternalClass
    {
    }

    public interface TypeToRenameRenamed
    {
    }

    public struct StructToMakeGeneric<TGenericType>
    {
    }

    public struct StructToRemoveFieldsFrom
    {
        public StructToRemoveFieldsFrom(int fieldToIgnore)
        {
            FieldToMakeReadonly = 3;
            FieldToMakeWritable = 5;
        }

        public const int ConstToChangeValue = 0;

        public static readonly int ConstToMakeField = 2;

        public readonly int FieldToMakeReadonly;

        public int FieldToMakeWritable;

        public static readonly int StaticFieldToMakeReadonly = 6;

        public const int StaticFieldToMakeConst = 7;

        public static int StaticFieldToMakeWritable = 8;
    }

    public class ClassToRemoveFieldsFrom
    {
        public const int ConstToChangeValue = 0;

        public static readonly int ConstToMakeField = 2;

        public readonly int FieldToMakeReadonly = 3;

        public int FieldToMakeWritable = 5;

        public static readonly int StaticFieldToMakeReadonly = 6;

        public const int StaticFieldToMakeConst = 7;

        public static int StaticFieldToMakeWritable = 8;
    }

    public class ClassToNestContainer
    {
        public class ClassToNest
        {
        }
    }

    public class ClassToUnnestContainer
    {
    }

    public class ClassToUnnest
    {
    }

    public class GenericTypeWithConstraintsToBeAdded<TToConstrain> where TToConstrain : IEnumerable<TToConstrain>, new()
    {
    }

    public class ClassWithMethods
    {
        public void MethodToAddParameters(int addedParameter) { }
    }

    public class TypeWithExtraInterface : IExtraInterface
    {
        public int Property { get; set; }
    }

    public interface IExtraInterface
    {
        int Property { get; set; }
    }

    public abstract class AbstractClassToAddMethodsTo
    {
        public abstract void NewAbstractMethod();

        public virtual void NewVirtualMethod() { }

        public void NewMethod() { }

        internal abstract void NewInternalMethod();
    }

    public abstract class AbstractClassToAddPropertiesTo
    {
        public abstract int NewAbstractProperty { get; }

        public abstract int PropertyToAddSetterTo { get; set; }

        public int NewProperty => 0;

        public virtual int NewVirtualProperty => 0;

        internal abstract int NewInternalProperty { get; }
    }

    public interface IInterfaceToAddMembersTo
    {
        bool ExistingMember { get; set; }
        int NewMember { get; set; }
    }

    public interface IInterfaceWithMembersThatWillGetRenamedRemovedAndAdded
    {
        void RenamedMember();
        void AddedMember();
    }

    public interface IInterfaceWithSameNumberOfRemovedAndAddedMembers
    {
        void FirstUnchangedMember();
        void SecondUnchangedMember();
        void FirstAddedMember();
        void SecondAddedMember();
        void ThirdAddedMember();
    }
}

namespace ComparisonScenarios.ChangedNamespace
{
    public class ClassToChangeNamespaces
    {
    }
}
