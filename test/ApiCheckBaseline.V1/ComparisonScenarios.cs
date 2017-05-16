// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// V1
namespace ComparisonScenarios
{
    public class PublicToInternalClass
    {
    }

    public interface TypeToRename
    {
    }

    public struct StructToMakeGeneric
    {
    }

    public struct StructToRemoveFieldsFrom
    {
        public StructToRemoveFieldsFrom(int fieldToIgnore)
        {
            FieldToIgnore = 0;
            FieldToMakeReadonly = 3;
            FieldToRemove = 4;
            FieldToMakeWritable = 5;
        }

        internal int FieldToIgnore;

        public const int ConstToChangeValue = 1;

        public const int ConstToMakeField = 2;

        public int FieldToMakeReadonly;

        public int FieldToRemove;

        public readonly int FieldToMakeWritable;

        public static int StaticFieldToMakeReadonly = 6;

        public static readonly int StaticFieldToMakeConst = 7;

        public static readonly int StaticFieldToMakeWritable = 8;
    }

    public class ClassToRemoveFieldsFrom
    {
        internal int FieldToIgnore = 0;

        public const int ConstToChangeValue = 1;

        public const int ConstToMakeField = 2;

        public int FieldToMakeReadonly = 3;

        public int FieldToRemove = 4;

        public readonly int FieldToMakeWritable = 5;

        public static int StaticFieldToMakeReadonly = 6;

        public static readonly int StaticFieldToMakeConst = 7;

        public static readonly int StaticFieldToMakeWritable = 8;
    }

    public class ClassToChangeNamespaces
    {
    }

    public class ClassToNestContainer
    {
    }

    public class ClassToNest
    {
    }

    public class ClassToUnnestContainer
    {
        public class ClassToUnnest
        {
        }
    }

    public class GenericTypeWithConstraintsToBeAdded<TToConstrain>
    {
    }

    public class ClassWithMethods
    {
        public void MethodToAddParameters() { }
    }

    public class TypeWithExtraInterface
    {
    }

    public abstract class AbstractClassToAddMethodsTo
    {
    }

    public abstract class AbstractClassToAddPropertiesTo
    {
        public abstract int PropertyToAddSetterTo { get; }
    }

    public interface IInterfaceToAddMembersTo
    {
        bool ExistingMember { get; set; }
    }

    public interface IInterfaceWithMembersThatWillGetRenamedRemovedAndAdded
    {
        void MemberToBeRenamed();
        void MemberToBeRemoved();
    }

    public interface IInterfaceWithSameNumberOfRemovedAndAddedMembers
    {
        void FirstMemberToRemove();
        void SecondMemberToRemove();
        void ThirdMemberToRemove();
        void FirstUnchangedMember();
        void SecondUnchangedMember();
    }
}
