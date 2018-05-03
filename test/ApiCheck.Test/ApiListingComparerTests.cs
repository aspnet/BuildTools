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

        public IEnumerable<Func<MemberInfo, bool>> TestFilters => new Func<MemberInfo, bool>[]
        {
            ti => (ti as TypeInfo)?.Namespace?.StartsWith("ComparisonScenarios") == false
        };

#if NETCOREAPP2_1 // Reflection does not provide a hook to enumerate forwarded types in .NET Framework.
        [Fact]
        public void Compare_AllowsTypeToBeForwarded()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);
            var typeToCheck = "public class ComparisonScenarios.TypeToBeForwarded";

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            Assert.DoesNotContain(breakingChanges, bc => bc.TypeId == typeToCheck);
        }

        [Fact]
        public void Compare_DetectsChangesInForwardedType()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);
            var typeToCheck = "public class ComparisonScenarios.TypeToBeForwardedAndChanged";
            var getterRemoval = new BreakingChange(
                typeToCheck,
                "public System.String get_PropertyToBeRemoved()",
                ChangeKind.Removal);
            var setterRemoval = new BreakingChange(
                typeToCheck,
                "public System.Void set_PropertyToBeRemoved(System.String value)",
                ChangeKind.Removal);

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            Assert.Equal(2, breakingChanges.Count(bc => bc.TypeId == typeToCheck));
            Assert.Contains(getterRemoval, breakingChanges);
            Assert.Contains(setterRemoval, breakingChanges);
        }
#endif

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

        [Theory]
        [InlineData("public class ComparisonScenarios.ClassToRemoveFieldsFrom")]
        [InlineData("public struct ComparisonScenarios.StructToRemoveFieldsFrom")]
        public void Compare_DetectsAllFieldRemovals(string typeToCheck)
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);

            // Oops. The NewInternalProperty addition is a breaking change; makes it impossible to subclass type in
            // another assembly.
            var expected = new List<BreakingChange>
            {
                // Changing a const's value doesn't cause a binary incompatibility but often causes problems.
                new BreakingChange(
                    typeToCheck,
                    "public const System.Int32 ConstToChangeValue = 1",
                    ChangeKind.Removal),
                // Removing a const doesn't cause a binary incompatibilty but often causes problems.
                new BreakingChange(
                    typeToCheck,
                    "public const System.Int32 ConstToMakeField = 2",
                    ChangeKind.Removal),
                // Oops. Making a field writable is not technically a breaking change.
                new BreakingChange(
                    typeToCheck,
                    "public readonly System.Int32 FieldToMakeWritable",
                    ChangeKind.Removal),
                new BreakingChange(
                    typeToCheck,
                    "public static readonly System.Int32 StaticFieldToMakeConst",
                    ChangeKind.Removal),
                // Oops. Making a field writable is not technically a breaking change.
                new BreakingChange(
                    typeToCheck,
                    "public static readonly System.Int32 StaticFieldToMakeWritable",
                    ChangeKind.Removal),
                new BreakingChange(
                    typeToCheck,
                    "public static System.Int32 StaticFieldToMakeReadonly",
                    ChangeKind.Removal),
                new BreakingChange(
                    typeToCheck,
                    "public System.Int32 FieldToMakeReadonly",
                    ChangeKind.Removal),
                new BreakingChange(
                    typeToCheck,
                    "public System.Int32 FieldToRemove",
                    ChangeKind.Removal),
            };

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            var breakingChanginesInType = breakingChanges
                .Where(change => string.Equals(change.TypeId, typeToCheck, StringComparison.Ordinal))
                .OrderBy(change => change.MemberId);
            Assert.Equal(expected, breakingChanginesInType);
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
        public void Compare_DetectsAbstractMethodAdditions()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);
            var typeToCheck = "public abstract class ComparisonScenarios.AbstractClassToAddMethodsTo";

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            var breakingChangesInType = breakingChanges
                .Where(change => string.Equals(change.TypeId, typeToCheck, StringComparison.Ordinal));
            var breakingChange = Assert.Single(breakingChangesInType);
            Assert.Equal(ChangeKind.Addition, breakingChange.Kind);
            Assert.Equal("public abstract System.Void NewAbstractMethod()", breakingChange.MemberId);
        }

        [Fact]
        public void Compare_DetectsAbstractPropertyAdditions()
        {
            // Arrange
            var v1ApiListing = CreateApiListingDocument(V1Assembly);
            var v2ApiListing = CreateApiListingDocument(V2Assembly);
            var comparer = new ApiListingComparer(v1ApiListing, v2ApiListing);
            var typeToCheck = "public abstract class ComparisonScenarios.AbstractClassToAddPropertiesTo";
            var expected = new List<BreakingChange>
            {
                new BreakingChange(
                    typeToCheck,
                    "public abstract System.Int32 get_NewAbstractProperty()",
                    ChangeKind.Addition),
                new BreakingChange(
                    typeToCheck,
                    "public abstract System.Void set_PropertyToAddSetterTo(System.Int32 value)",
                    ChangeKind.Addition),
            };

            // Act
            var breakingChanges = comparer.GetDifferences();

            // Assert
            var breakingChangesInType = breakingChanges
                .Where(change => string.Equals(change.TypeId, typeToCheck, StringComparison.Ordinal))
                .OrderBy(change => change.MemberId);
            Assert.Equal(expected, breakingChangesInType);
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
            var interfaceBreakingChanges = breakingChanges
                .Where(b => b.TypeId == "public interface ComparisonScenarios.IInterfaceToAddMembersTo")
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
            Assert.DoesNotContain(
                breakingChanges,
                bc => bc.TypeId == "public interface ComparisonScenarios.IInterfaceToAddMembersTo");
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
            var interfaceBreakingChanges = breakingChanges
                .Where(b => b.TypeId == "public interface ComparisonScenarios.IInterfaceWithMembersThatWillGetRenamedRemovedAndAdded")
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
            var interfaceBreakingChanges = breakingChanges
                .Where(b => b.TypeId == "public interface ComparisonScenarios.IInterfaceWithSameNumberOfRemovedAndAddedMembers")
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

        private ApiListing CreateApiListingDocument(Assembly assembly)
        {
            var generator = new ApiListingGenerator(assembly, TestFilters);

            return generator.GenerateApiListing();
        }
    }
}
