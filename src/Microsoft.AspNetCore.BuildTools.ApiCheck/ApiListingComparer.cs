// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ApiCheck.Description;
using ApiCheck.IO;

namespace ApiCheck
{
    public class ApiListingComparer
    {
        private readonly ApiListing _newApiListing;
        private readonly ApiListing _oldApiListing;
        private readonly IList<ApiChangeExclusion> _exclusions;

        public ApiListingComparer(
            ApiListing oldApiListing,
            ApiListing newApiListing,
            IList<ApiChangeExclusion> exclusions = null)
        {
            _oldApiListing = oldApiListing;
            _newApiListing = newApiListing;
            _exclusions = exclusions ?? new List<ApiChangeExclusion>();
        }

        public ApiComparisonResult GetDifferences()
        {
            var exclusions = _exclusions.ToList();

            var breakingChanges = new List<BreakingChange>();
            foreach (var type in _oldApiListing.Types)
            {
                var newType = _newApiListing.FindType(type.Name);
                if (newType == null || !string.Equals(type.Id, newType.Id, StringComparison.Ordinal))
                {
                    var isAcceptable = newType != null && IsAcceptableTypeChange(type, newType);
                    if (!isAcceptable)
                    {
                        var typeChange = FilterExclusions(type, member: null, exclusions: exclusions);
                        if (typeChange != null)
                        {
                            breakingChanges.Add(typeChange);
                        }
                    }

                }

                if (newType != null)
                {
                    CompareMembers(type, newType, exclusions, breakingChanges);
                }
            }

            return new ApiComparisonResult(breakingChanges, exclusions);
        }

        private void CompareMembers(TypeDescriptor type, TypeDescriptor newType, List<ApiChangeExclusion> exclusions, List<BreakingChange> breakingChanges)
        {
            var removedOrChanged = 0;
            foreach (var member in type.Members)
            {
                var newMember = newType.FindMember(member.Id);
                var isAcceptable = IsAcceptableMemberChange(_newApiListing, newType, member);
                if (isAcceptable)
                {
                    if (newMember == null)
                    {
                        removedOrChanged++;
                    }

                    continue;
                }

                if (newMember == null)
                {
                    removedOrChanged++;
                    var memberChange = FilterExclusions(type, member, exclusions);
                    if (memberChange != null)
                    {
                        breakingChanges.Add(memberChange);
                    }
                }
            }

            if (type.Kind == TypeKind.Interface && type.Members.Count - removedOrChanged < newType.Members.Count)
            {
                var members = newType.Members.ToList();
                foreach (var member in newType.Members)
                {
                    var change = FilterExclusions(type, null, exclusions);
                    if (change == null)
                    {
                        members.Remove(member);
                    }
                }

                if (type.Members.Count - removedOrChanged < members.Count)
                {
                    breakingChanges.Add(new BreakingChange(type, "New members were added to the following interface"));
                }
            }
        }

        private bool IsAcceptableMemberChange(ApiListing newApiListing, TypeDescriptor newType, MemberDescriptor member)
        {
            var acceptable = false;
            var candidate = newType == null ? null : newType;
            while (candidate != null && !acceptable)
            {
                if (candidate.Members.Any(m => m.Id == member.Id))
                {
                    acceptable = true;
                }
                else if (member.Kind == MemberKind.Method)
                {
                    var newMember = newType.Members.FirstOrDefault(m => SameSignature(member, m));
                    if (newMember != null)
                    {
                        acceptable = (!member.Sealed ? !newMember.Sealed : true) &&
                            (member.Virtual ? newMember.Virtual || newMember.Override : true) &&
                            (member.Static == newMember.Static) &&
                            (!member.Abstract ? !newMember.Abstract : true);
                    }
                }

                candidate = candidate.BaseType == null ? null : FindOrGenerateDescriptorForBaseType(newApiListing, candidate);
            }

            return acceptable;
        }

        private static TypeDescriptor FindOrGenerateDescriptorForBaseType(ApiListing newApiListing, TypeDescriptor candidate)
        {
            return newApiListing.FindType(candidate.BaseType) ??
                ReflectionApiListingReader.GenerateTypeDescriptor(candidate.Source.BaseType.GetTypeInfo(), newApiListing.SourceFilters);
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
                default:
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

        private BreakingChange FilterExclusions(TypeDescriptor type, MemberDescriptor member, List<ApiChangeExclusion> exclusions)
        {
            var exclusion = exclusions
                .FirstOrDefault(e => e.IsExclusionFor(type.Id, member?.Id));

            if (exclusion != null)
            {
                var element = _newApiListing.FindElement(exclusion.NewTypeId, exclusion.NewMemberId);
                if (exclusion.Kind == ChangeKind.Removal && element == null ||
                    exclusion.Kind == ChangeKind.Modification && element != null ||
                    exclusion.Kind == ChangeKind.Addition && element != null)
                {
                    exclusions.Remove(exclusion);
                    return null;
                }
            }

            if (member == null)
            {
                return new BreakingChange(type);
            }
            else
            {
                return new BreakingChange(member, type.Id);
            }
        }
    }
}
