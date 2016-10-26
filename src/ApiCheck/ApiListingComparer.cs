// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
            foreach (var type in _oldApiListing.Types)
            {
                var newType = _newApiListing.FindType(type.Id);
                if (newType == null)
                {
                    breakingChanges.Add(new BreakingChange(type));

                    foreach (var member in type.Members)
                    {
                        breakingChanges.Add(new BreakingChange(member, type.Id));
                    }
                    continue;
                }

                foreach (var member in type.Members)
                {
                    var newMember = newType.FindMember(member.Id);
                    if (newMember == null)
                    {
                        breakingChanges.Add(new BreakingChange(member, type.Id));
                    }
                }
            }

            return breakingChanges;
        }
    }
}
