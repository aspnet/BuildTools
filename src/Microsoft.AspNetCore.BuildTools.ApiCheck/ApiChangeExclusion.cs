// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace ApiCheck
{
    /// <summary>
    /// Exclusion list for API changes. It contains the missing element id, (type or type + member)
    /// which acts as a unique identifier for the exclusion. The new type id and the new member id
    /// that replace them in case there's any. And the type of change performed, whether we changed
    /// something on the signature of the type or member of we just deleted the entire element.
    /// 
    /// Exclusions are only considered valid if the old elements on the baseline and the new elements
    /// described in the exclusion match perfectly. No additional exclusion should be left out after
    /// doing a successful api listing comparison.
    /// </summary>
    public class ApiChangeExclusion
    {
        public string OldTypeId { get; set; }
        public string OldMemberId { get; set; }
        public string NewTypeId { get; set; }
        public string NewMemberId { get; set; }        
        public ChangeKind Kind { get; set; }

        public bool IsExclusionFor(string oldTypeId, string oldMemberId) =>
            oldTypeId != null && OldTypeId == oldTypeId && OldMemberId == oldMemberId;
    }
}
