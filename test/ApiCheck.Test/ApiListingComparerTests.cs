using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ApiCheck.Description;
using ApiCheck.IO;
using ApiCheckApiListing.V2;
using Scenarios;
using Xunit;

namespace ApiCheck.Test
{
    public class ApiListingComparerTests
    {
        public Assembly V1Assembly => typeof(ApiCheckApiListingV1).GetTypeInfo().Assembly;
        public Assembly V2Assembly => typeof(ApiCheckApiListingV2).GetTypeInfo().Assembly;

        public IEnumerable<Func<MemberInfo, bool>> TestFilters => new Func<MemberInfo, bool>[]
        {
            ti => ti is TypeInfo && !((TypeInfo)ti).Namespace.StartsWith("ComparisonScenarios")
        };


        [Fact]
        public void Compare_Detects_ChangesInTypeVisibility()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "public class ComparisonScenarios.PublicToInternalClass");
        }

        [Fact]
        public void Compare_Detects_TypeRenames()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "public interface ComparisonScenarios.TypeToRename");
        }

        [Fact]
        public void Compare_Detects_TypeGenericArityChanges()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "public struct ComparisonScenarios.StructToMakeGeneric");
        }

        [Fact]
        public void Compare_Detects_NamespaceChanges()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "public class ComparisonScenarios.ClassToChangeNamespaces");
        }

        [Fact]
        public void Compare_Detects_ClassBeingNested()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "public class ComparisonScenarios.ClassToNest");
        }

        [Fact]
        public void Compare_Detects_ClassBeingUnnested()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "public class ComparisonScenarios.ClassToUnnestContainer+ClassToUnnest");
        }

        [Fact]
        public void Compare_Detects_GenericTypeConstraintsBeingAdded()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "public class ComparisonScenarios.GenericTypeWithConstraintsToBeAdded<T0>");
        }

        [Fact]
        public void Compare_Detects_MethodParametersBeingAdded()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            var change = Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "public System.Void MethodToAddParameters()");
        }

        [Fact]
        public void Compare_DoesNotFailForTypeAddingAnInterface()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            Assert.Null(changes.BreakingChanges.FirstOrDefault(bc => bc.Item.Id == "public ComparisonScenarios.TypeWithExtraInterface"));
        }

        [Fact]
        public void Compare_DetectsNewMembersBeingAddedToAnInterface()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "public interface ComparisonScenarios.IInterfaceToAddMembersTo");
        }

        [Fact]
        public void Compare_AllowsExclusionsOnNewInterfaceMembers()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing, new[] {
                new ApiChangeExclusion
                {
                    OldTypeId = "public interface ComparisonScenarios.IInterfaceToAddMembersTo",
                    OldMemberId = null,
                    NewTypeId = "public interface ComparisonScenarios.IInterfaceToAddMembersTo",
                    NewMemberId = "System.Int32 get_NewMember()",
                    Kind = ChangeKind.Addition
                },
                new ApiChangeExclusion
                {
                    OldTypeId = "public interface ComparisonScenarios.IInterfaceToAddMembersTo",
                    OldMemberId = null,
                    NewTypeId = "public interface ComparisonScenarios.IInterfaceToAddMembersTo",
                    NewMemberId = "System.Void set_NewMember(System.Int32 value)",
                    Kind = ChangeKind.Addition
                }
            });

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            Assert.Null(changes.BreakingChanges.FirstOrDefault(bc => bc.Item.Id == "public interface ComparisonScenarios.IInterfaceToAddMembersTo"));
        }

        [Fact]
        public void Compare_DetectsNewMembersInThePresenceOfRenamedAndRemovedMembers()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "System.Void MemberToBeRenamed()");
            Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "System.Void MemberToBeRemoved()");
            Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "public interface ComparisonScenarios.IInterfaceToAddMembersTo");
        }

        [Fact]
        public void Compare_DetectsNewMembersInThePresenceOfTheSameNumberOfRemovedAndAddedMembers()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var changes = comparer.GetDifferences();

            // Assert
            Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "System.Void FirstMemberToRemove()");
            Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "System.Void SecondMemberToRemove()");
            Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "System.Void ThirdMemberToRemove()");
            Assert.Single(changes.BreakingChanges, bc => bc.Item.Id == "public interface ComparisonScenarios.IInterfaceWithSameNumberOfRemovedAndAddedMembers");
        }

        private ApiListing CreateApiListingDocument(Assembly assembly, IEnumerable<Func<MemberInfo, bool>> additionalFilters = null)
        {
            additionalFilters = additionalFilters ?? Enumerable.Empty<Func<MemberInfo, bool>>();
            var generator = new ReflectionApiListingReader(assembly, TestFilters.Concat(additionalFilters));

            return generator.Read();
        }
    }
}
