// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
