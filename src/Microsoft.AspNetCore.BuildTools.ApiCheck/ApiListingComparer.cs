// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ApiCheck.Description;

namespace ApiCheck
{
    public class ApiListingComparer
    {
        private readonly ApiListing _newApiListing;
        private readonly ApiListing _oldApiListing;

        public ApiListingComparer(
            ApiListing oldApiListing,
            ApiListing newApiListing)
        {
            _oldApiListing = oldApiListing;
            _newApiListing = newApiListing;
        }

        public IList<BreakingChange> GetDifferences()
        {
            var breakingChanges = new List<BreakingChange>();
            var newTypes = _newApiListing.Types;

            foreach (var type in _oldApiListing.Types)
            {
                var newType = _newApiListing.FindType(type.Name);
                if (newType == null)
                {
                    breakingChanges.Add(new BreakingChange(type.Id, memberId: null, kind: ChangeKind.Removal));
                }
                else
                {
                    newTypes.Remove(newType);

                    if (!string.Equals(type.Id, newType.Id, StringComparison.Ordinal)
                        && !IsAcceptableTypeChange(type, newType))
                    {
                        breakingChanges.Add(new BreakingChange(type.Id, memberId: null, kind: ChangeKind.Removal));
                        continue;
                    }

                    CompareMembers(type, newType, breakingChanges);
                }
            }

            return breakingChanges;
        }

        private void CompareMembers(TypeDescriptor type, TypeDescriptor newType, List<BreakingChange> breakingChanges)
        {
            var newMembers = newType.Members.ToList();

            foreach (var member in type.Members)
            {
                if (IsAcceptableMemberChange(newType, member, out var newMember))
                {
                    newMembers.Remove(newMember);
                }
                else
                {
                    breakingChanges.Add(new BreakingChange(type.Id, member.Id, ChangeKind.Removal));
                }
            }

            if (type.Kind == TypeKind.Interface && newMembers.Count > 0)
            {
                breakingChanges.AddRange(newMembers.Select(member => new BreakingChange(newType.Id, member.Id, ChangeKind.Addition)));
            }
        }

        private bool IsAcceptableMemberChange(TypeDescriptor newType, MemberDescriptor member, out MemberDescriptor newMember)
        {
            var acceptable = false;
            newMember = null;
            var candidate = newType;
            while (candidate != null && !acceptable)
            {
                var matchingMembers = candidate.Members.Where(m => m.Id == member.Id).ToList();

                if (matchingMembers.Count == 1)
                {
                    newMember = matchingMembers.Single();
                    acceptable = true;
                }
                else if (member.Kind == MemberKind.Method)
                {
                    var matchingMember = newType.Members.FirstOrDefault(m => SameSignature(member, m));
                    if (matchingMember != null)
                    {
                        acceptable = (member.Sealed || !matchingMember.Sealed)
                                     && (!member.Virtual || matchingMember.Virtual || matchingMember.Override)
                                     && member.Static == matchingMember.Static
                                     && (member.Abstract || !matchingMember.Abstract);

                        if (acceptable)
                        {
                            newMember = matchingMember;
                        }
                    }
                }

                candidate = candidate.BaseType == null ? null : FindOrGenerateDescriptorForBaseType(candidate);
            }

            return acceptable;
        }

        private TypeDescriptor FindOrGenerateDescriptorForBaseType(TypeDescriptor candidate)
        {
            return _newApiListing.FindType(candidate.BaseType) ??
                ApiListingGenerator.GenerateTypeDescriptor(candidate.Source.BaseType.GetTypeInfo(), _newApiListing.SourceFilters);
        }

        private bool SameSignature(MemberDescriptor original, MemberDescriptor candidate)
        {
            return original.ReturnType == candidate.ReturnType &&
                original.Name == candidate.Name &&
                SameGenericParameters(original.GenericParameter, candidate.GenericParameter) &&
                SameParameters(original.Parameters, candidate.Parameters);
        }

        private bool SameParameters(
            IList<ParameterDescriptor> original,
            IList<ParameterDescriptor> candidate)
        {
            if (original.Count != candidate.Count)
            {
                return false;
            }

            for (var i = 0; i < original.Count; i++)
            {
                var originalParameter = original[i];
                var candidatePrameter = candidate[i];
                if (originalParameter.Type != candidatePrameter.Type ||
                    originalParameter.Name != candidatePrameter.Name ||
                    originalParameter.Direction != candidatePrameter.Direction ||
                    originalParameter.DefaultValue != candidatePrameter.DefaultValue ||
                    originalParameter.IsParams != candidatePrameter.IsParams)
                {
                    return false;
                }
            }

            return true;
        }

        private bool SameGenericParameters(IList<GenericParameterDescriptor> original, IList<GenericParameterDescriptor> candidate)
        {
            if (original.Count != candidate.Count)
            {
                return false;
            }

            for (var i = 0; i < original.Count; i++)
            {
                var originalParameter = original[i];
                var candidatePrameter = candidate[i];
                if (originalParameter.ParameterPosition != candidatePrameter.ParameterPosition ||
                    !originalParameter.BaseTypeOrInterfaces.OrderBy(id => id).SequenceEqual(candidatePrameter.BaseTypeOrInterfaces.OrderBy(id => id)) ||
                    originalParameter.New != candidatePrameter.New ||
                    originalParameter.Class != candidatePrameter.Class ||
                    originalParameter.Struct != candidatePrameter.Struct)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsAcceptableTypeChange(TypeDescriptor oldType, TypeDescriptor newType)
        {
            var typeChanged = oldType.Kind != newType.Kind;
            if (typeChanged)
            {
                return false;
            }

            if (!HasCompatibleVisibility(oldType, newType))
            {
                return false;
            }

            if (oldType.GenericParameters.Count > 0 &&
                !HasCompatibleSetOfGenericParameters(oldType.GenericParameters, newType.GenericParameters))
            {
                return false;
            }

            switch (oldType.Kind)
            {
                case TypeKind.Struct:
                    return ImplementsAllInterfaces(oldType, newType);
                case TypeKind.Class:
                    return ImplementsAllInterfaces(oldType, newType) &&
                        (!newType.Sealed || oldType.Sealed == newType.Sealed) &&
                        (!newType.Abstract || oldType.Abstract == newType.Abstract) &&
                        newType.Static == oldType.Static &&
                        (oldType.BaseType == null || newType.BaseType == oldType.BaseType);
                case TypeKind.Interface:
                    return HasCompatibleSetOfInterfaces(oldType, newType);
                case TypeKind.Enumeration:
                    return oldType.BaseType == newType.BaseType;
                case TypeKind.Unknown:
                    break;
            }

            return false;
        }

        private bool HasCompatibleSetOfGenericParameters(
            IList<GenericParameterDescriptor> oldGenericParameters,
            IList<GenericParameterDescriptor> newGenericParameters)
        {
            if (oldGenericParameters.Count != newGenericParameters.Count)
            {
                return false;
            }

            var oldSet = oldGenericParameters.OrderBy(ogp => ogp.ParameterPosition).ToArray();
            var newSet = newGenericParameters.OrderBy(ogp => ogp.ParameterPosition).ToArray();
            for (var i = 0; i < oldSet.Length; i++)
            {
                var oldParameter = oldSet[i];
                var newParameter = newSet[i];
                var areCompatible = AreCompatible(oldParameter, newParameter);
                if (!areCompatible)
                {
                    return false;
                }
            }

            return true;
        }

        private bool AreCompatible(GenericParameterDescriptor oldParameter, GenericParameterDescriptor newParameter)
        {
            return ((newParameter.New && oldParameter.New) || !newParameter.New) &&
                ((newParameter.Class && oldParameter.Class) || !newParameter.Class) &&
                ((newParameter.Struct && oldParameter.Struct) || !newParameter.Struct) &&
                newParameter.BaseTypeOrInterfaces.Count == oldParameter.BaseTypeOrInterfaces.Count &&
                newParameter.BaseTypeOrInterfaces.All(btoi => oldParameter.BaseTypeOrInterfaces.Contains(btoi));
        }

        private bool HasCompatibleSetOfInterfaces(TypeDescriptor oldType, TypeDescriptor newType)
        {
            // An interface can't require new implemented interfaces unless they are marker interfaces (they don't have any member)
            var newInterfaces = newType.Source.ImplementedInterfaces
                .Where(i => !oldType.ImplementedInterfaces.Contains(TypeDescriptor.GetTypeNameFor(i.GetTypeInfo())));

            return newInterfaces.All(ni => ni.GetTypeInfo().GetMembers().Length == 0);
        }

        private bool ImplementsAllInterfaces(TypeDescriptor oldType, TypeDescriptor newType)
        {
            var oldInterfaces = oldType.ImplementedInterfaces;
            var newInterfaces = newType.Source.ImplementedInterfaces.Select(i => TypeDescriptor.GetTypeNameFor(i.GetTypeInfo()));

            return oldInterfaces.All(oi => newInterfaces.Contains(oi));
        }

        private bool HasCompatibleVisibility(TypeDescriptor oldType, TypeDescriptor newType)
        {
            switch (oldType.Visibility)
            {
                case ApiElementVisibility.Public:
                    return newType.Visibility == ApiElementVisibility.Public;
                case ApiElementVisibility.Protected:
                    // Is going from protected to public a breaking change ?
                    return true;
                default:
                    throw new InvalidOperationException("Unrecognized visibility");
            }
        }
    }
}
