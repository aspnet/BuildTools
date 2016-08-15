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
}
