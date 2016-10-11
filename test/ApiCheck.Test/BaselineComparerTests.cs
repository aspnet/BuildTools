using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ApiCheck.Baseline;
using ApiCheckBaseline.V2;
using Scenarios;
using Xunit;

namespace ApiCheck.Test
{
    public class BaselineComparerTests
    {
        public Assembly V1Assembly => typeof(ApiCheckBaselineV1).GetTypeInfo().Assembly;
        public Assembly V2Assembly => typeof(ApiCheckBaselineV2).GetTypeInfo().Assembly;

        public IEnumerable<Func<TypeInfo, bool>> TypeFilters => new Func<TypeInfo, bool>[]{
            ti => !ti.Namespace.Equals("Scenarios")
        };


        [Fact]
        public void Compare_Detects_ChangesInTypeVisibility()
        {
            // Arrange
            var v1Baseline = CreateBaselineDocument(V1Assembly);
            var v2Baseline = CreateBaselineDocument(V2Assembly);
            var exceptions = CreateDefault();

            var comparer = new BaselineComparer(v1Baseline, v2Baseline, exceptions);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes, bc => bc.Item.Id == "public class ComparisonScenarios.PublicToInternalClass");
        }

        [Fact]
        public void Compare_Detects_TypeRenames()
        {
            // Arrange
            var v1Baseline = CreateBaselineDocument(V1Assembly);
            var v2Baseline = CreateBaselineDocument(V2Assembly);
            var exceptions = CreateDefault();

            var comparer = new BaselineComparer(v1Baseline, v2Baseline, exceptions);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes, bc => bc.Item.Id == "public interface ComparisonScenarios.TypeToRename");
        }

        [Fact]
        public void Compare_Detects_TypeGenericityChanges()
        {
            // Arrange
            var v1Baseline = CreateBaselineDocument(V1Assembly);
            var v2Baseline = CreateBaselineDocument(V2Assembly);
            var exceptions = CreateDefault();

            var comparer = new BaselineComparer(v1Baseline, v2Baseline, exceptions);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes, bc => bc.Item.Id == "public struct ComparisonScenarios.StructToMakeGeneric");
        }

        [Fact]
        public void Compare_Detects_NamespaceChanges()
        {
            // Arrange
            var v1Baseline = CreateBaselineDocument(V1Assembly);
            var v2Baseline = CreateBaselineDocument(V2Assembly);
            var exceptions = CreateDefault();

            var comparer = new BaselineComparer(v1Baseline, v2Baseline, exceptions);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes, bc => bc.Item.Id == "public class ComparisonScenarios.ClassToChangeNamespaces");
        }

        [Fact]
        public void Compare_Detects_ClassBeingNested()
        {
            // Arrange
            var v1Baseline = CreateBaselineDocument(V1Assembly);
            var v2Baseline = CreateBaselineDocument(V2Assembly);
            var exceptions = CreateDefault();

            var comparer = new BaselineComparer(v1Baseline, v2Baseline, exceptions);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes, bc => bc.Item.Id == "public class ComparisonScenarios.ClassToNest");
        }

        [Fact]
        public void Compare_Detects_ClassBeingUnnested()
        {
            // Arrange
            var v1Baseline = CreateBaselineDocument(V1Assembly);
            var v2Baseline = CreateBaselineDocument(V2Assembly);
            var exceptions = CreateDefault();

            var comparer = new BaselineComparer(v1Baseline, v2Baseline, exceptions);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes, bc => bc.Item.Id == "public class ComparisonScenarios.ClassToUnnestContainer+ClassToUnnest");
        }

        [Fact]
        public void Compare_Detects_GenericTypeConstraintsBeingAdded()
        {
            // Arrange
            var v1Baseline = CreateBaselineDocument(V1Assembly);
            var v2Baseline = CreateBaselineDocument(V2Assembly);
            var exceptions = CreateDefault();

            var comparer = new BaselineComparer(v1Baseline, v2Baseline, exceptions);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes, bc => bc.Item.Id == "public class ComparisonScenarios.GenericTypeWithConstraintsToBeAdded<TToConstrain>");
        }

        [Fact]
        public void Compare_Detects_MethodParametersBeingAdded()
        {
            // Arrange
            var v1Baseline = CreateBaselineDocument(V1Assembly);
            var v2Baseline = CreateBaselineDocument(V2Assembly);
            var exceptions = CreateDefault();

            var comparer = new BaselineComparer(v1Baseline, v2Baseline, exceptions);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes, bc => bc.Item.Id == "public System.Void MethodToAddParameters()");
        }

        private static IList<Func<BreakingChangeCandidateContext, bool>> CreateDefault(params Func<BreakingChangeCandidateContext, bool>[] additionalHandlers)
        {
            return new List<Func<BreakingChangeCandidateContext, bool>>().Concat(additionalHandlers).ToList();
        }

        private BaselineDocument CreateBaselineDocument(Assembly assembly, IEnumerable<Func<TypeInfo, bool>> additionalFilters = null)
        {
            additionalFilters = additionalFilters ?? Enumerable.Empty<Func<TypeInfo, bool>>();
            var generator = new BaselineGenerator(assembly, TypeFilters.Concat(additionalFilters));

            return generator.GenerateBaseline();
        }
    }
}
