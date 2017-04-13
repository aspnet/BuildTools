// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ApiCheck.Description;
using ApiCheckApiListing.V2;
using Scenarios;
using Xunit;

namespace ApiCheck.Test
{
    public class ApiListingComparerTests
    {
        public Assembly V1Assembly => typeof(ApiCheckApiListingV1).GetTypeInfo().Assembly;
        public Assembly V2Assembly => typeof(ApiCheckApiListingV2).GetTypeInfo().Assembly;

        public IEnumerable<Func<MemberInfo, bool>> TestFilters
            => new Func<MemberInfo, bool> []
               {
                   ti => (ti as TypeInfo)?.Namespace?.StartsWith("ComparisonScenarios") == false
               };

        [Fact]
        public void Compare_Detects_ChangesInTypeVisibility_as_removal()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            var expected = new BreakingChange(
                "public class ComparisonScenarios.PublicToInternalClass",
                memberId: null,
                kind: ChangeKind.Removal);
            Assert.Contains(expected, breakingChanges);
        }

        [Fact]
        public void Compare_Detects_TypeRenames_as_removal()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            var expected = new BreakingChange(
                "public interface ComparisonScenarios.TypeToRename",
                memberId: null,
                kind: ChangeKind.Removal);
            Assert.Contains(expected, breakingChanges);
        }

        [Fact]
        public void Compare_Detects_TypeGenericAritybreakingChanges_as_removal()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            var expected = new BreakingChange(
                "public struct ComparisonScenarios.StructToMakeGeneric",
                memberId: null,
                kind: ChangeKind.Removal);
            Assert.Contains(expected, breakingChanges);
        }

        [Fact]
        public void Compare_Detects_NamespacebreakingChanges_as_removal()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            var expected = new BreakingChange(
                "public class ComparisonScenarios.ClassToChangeNamespaces",
                memberId: null,
                kind: ChangeKind.Removal);
            Assert.Contains(expected, breakingChanges);
        }

        [Fact]
        public void Compare_Detects_ClassBeingNested_as_removal()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            var expected = new BreakingChange(
                "public class ComparisonScenarios.ClassToNest",
                memberId: null,
                kind: ChangeKind.Removal);
            Assert.Contains(expected, breakingChanges);
        }

        [Fact]
        public void Compare_Detects_ClassBeingUnnested_as_removal()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            var expected = new BreakingChange(
                "public class ComparisonScenarios.ClassToUnnestContainer+ClassToUnnest",
                memberId: null,
                kind: ChangeKind.Removal);
            Assert.Contains(expected, breakingChanges);
        }

        [Fact]
        public void Compare_Detects_GenericTypeConstraintsBeingAdded_as_removal()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            var expected = new BreakingChange(
                "public class ComparisonScenarios.GenericTypeWithConstraintsToBeAdded<T0>",
                memberId: null,
                kind: ChangeKind.Removal);
            Assert.Contains(expected, breakingChanges);
        }

        [Fact]
        public void Compare_Detects_MethodParametersBeingAdded_as_removal()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            var expected = new BreakingChange(
                "public class ComparisonScenarios.ClassWithMethods",
                "public System.Void MethodToAddParameters()",
                kind: ChangeKind.Removal);
            Assert.Contains(expected, breakingChanges);
        }

        [Fact]
        public void Compare_DoesNotFailForTypeAddingAnInterface()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            Assert.Null(breakingChanges.FirstOrDefault(
                bc => bc.TypeId == "public ComparisonScenarios.TypeWithExtraInterface"));
        }

        [Fact]
        public void Compare_DetectsNewMembersBeingAddedToAnInterface_as_addition()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            var interfaceBreakingChanges = breakingChanges.Where(
                    b => b.TypeId ==
                         "public interface ComparisonScenarios.IInterfaceToAddMembersTo")
                .ToList();
            Assert.Single(interfaceBreakingChanges,
                b => b.MemberId == "System.Int32 get_NewMember()" && b.Kind == ChangeKind.Addition);
            Assert.Single(interfaceBreakingChanges,
                b => b.MemberId == "System.Void set_NewMember(System.Int32 value)" && b.Kind == ChangeKind.Addition);
        }

        [Fact]
        public void Compare_AllowsExclusionsOnNewInterfaceMembers()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            var knownBreakingChanges = new List<BreakingChange>
                                       {
                                           new BreakingChange(
                                               "public interface ComparisonScenarios.IInterfaceToAddMembersTo",
                                               "System.Int32 get_NewMember()",
                                               ChangeKind.Addition),
                                           new BreakingChange(
                                               "public interface ComparisonScenarios.IInterfaceToAddMembersTo",
                                               "System.Void set_NewMember(System.Int32 value)",
                                               ChangeKind.Addition)
                                       };

            // Act
            var breakingChanges = comparer.GetDifferences().Except(knownBreakingChanges);

            // Assert
            Assert.Null(breakingChanges.FirstOrDefault(
                bc => bc.TypeId == "public interface ComparisonScenarios.IInterfaceToAddMembersTo"));
        }

        [Fact]
        public void Compare_DetectsNewMembersInThePresenceOfRenamedAndRemovedMembers()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            var interfaceBreakingChanges = breakingChanges.Where(
                    b => b.TypeId ==
                         "public interface ComparisonScenarios.IInterfaceWithMembersThatWillGetRenamedRemovedAndAdded")
                .ToList();
            Assert.Single(interfaceBreakingChanges,
                b => b.MemberId == "System.Void MemberToBeRenamed()" && b.Kind == ChangeKind.Removal);
            Assert.Single(interfaceBreakingChanges,
                b => b.MemberId == "System.Void MemberToBeRemoved()" && b.Kind == ChangeKind.Removal);
            Assert.Single(interfaceBreakingChanges,
                b => b.MemberId == "System.Void RenamedMember()" && b.Kind == ChangeKind.Addition);
            Assert.Single(interfaceBreakingChanges,
                b => b.MemberId == "System.Void AddedMember()" && b.Kind == ChangeKind.Addition);
        }

        [Fact]
        public void Compare_DetectsNewMembersInThePresenceOfTheSameNumberOfRemovedAndAddedMembers()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            var interfaceBreakingChanges = breakingChanges.Where(
                    b => b.TypeId ==
                         "public interface ComparisonScenarios.IInterfaceWithSameNumberOfRemovedAndAddedMembers")
                .ToList();
            Assert.Single(interfaceBreakingChanges,
                b => b.MemberId == "System.Void FirstMemberToRemove()" && b.Kind == ChangeKind.Removal);
            Assert.Single(interfaceBreakingChanges,
                b => b.MemberId == "System.Void SecondMemberToRemove()" && b.Kind == ChangeKind.Removal);
            Assert.Single(interfaceBreakingChanges,
                b => b.MemberId == "System.Void ThirdMemberToRemove()" && b.Kind == ChangeKind.Removal);
            Assert.Single(interfaceBreakingChanges,
                b => b.MemberId == "System.Void FirstAddedMember()" && b.Kind == ChangeKind.Addition);
            Assert.Single(interfaceBreakingChanges,
                b => b.MemberId == "System.Void SecondAddedMember()" && b.Kind == ChangeKind.Addition);
            Assert.Single(interfaceBreakingChanges,
                b => b.MemberId == "System.Void ThirdAddedMember()" && b.Kind == ChangeKind.Addition);
        }

        private ApiListing CreateApiListingDocument(Assembly assembly,
            IEnumerable<Func<MemberInfo, bool>> additionalFilters = null)
        {
            additionalFilters = additionalFilters ?? Enumerable.Empty<Func<MemberInfo, bool>>();
            var generator = new ApiListingGenerator(assembly, TestFilters.Concat(additionalFilters));

            return generator.GenerateApiListing();
        }
    }
}
