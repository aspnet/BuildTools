// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.DotNet.PlatformAbstractions;

namespace ApiCheck
{
    public class BreakingChange
    {
        public BreakingChange(string typeId, string memberId, ChangeKind kind)
        {
            TypeId = typeId;
            MemberId = memberId;
            Kind = kind;
        }

        public string TypeId { get; }
        public string MemberId { get; }
        public ChangeKind Kind { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == GetType() && Equals((BreakingChange)obj);
        }

        private bool Equals(BreakingChange other)
        {
            return string.Equals(TypeId, other.TypeId) && string.Equals(MemberId, other.MemberId) && Kind == other.Kind;
        }

        public override int GetHashCode()
        {
            var hashCodeCombiner = HashCodeCombiner.Start();
            hashCodeCombiner.Add(TypeId);
            hashCodeCombiner.Add(MemberId);
            hashCodeCombiner.Add(Kind);

            return hashCodeCombiner.CombinedHash;
        }
    }
}
