// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
}

namespace ComparisonScenarios.ChangedNamespace
{
    public class ClassToChangeNamespaces
    {
    }
}