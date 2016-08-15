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
}

namespace ComparisonScenarios.ChangedNamespace
{
    public class ClassToChangeNamespaces
    {
    }
}