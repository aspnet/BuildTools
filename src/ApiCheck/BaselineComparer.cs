using System;
using System.Collections.Generic;
using ApiCheck.Baseline;

namespace ApiCheck
{
    public class BaselineComparer
    {
        private readonly ApiListing _newBaseline;
        private readonly ApiListing _oldBaseline;

        public BaselineComparer(
            ApiListing oldBaseline,
            ApiListing newBaseline)
        {
            _oldBaseline = oldBaseline;
            _newBaseline = newBaseline;
        }

        public IList<BreakingChange> GetDifferences()
        {
            var breakingChanges = new List<BreakingChange>();
            foreach (var type in _oldBaseline.Types)
            {
                var newType = _newBaseline.FindType(type.Id);
                if (newType == null)
                {
                    breakingChanges.Add(new BreakingChange(type));

                    foreach (var member in type.Members)
                    {
                        breakingChanges.Add(new BreakingChange(member));
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
