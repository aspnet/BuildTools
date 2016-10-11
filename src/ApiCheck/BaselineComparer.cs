using System;
using System.Collections.Generic;
using ApiCheck.Baseline;

namespace ApiCheck
{
    public class BaselineComparer
    {
        private readonly BaselineDocument _newBaseline;
        private readonly BaselineDocument _oldBaseline;
        private readonly IEnumerable<Func<BreakingChangeCandidateContext, bool>> _breakingChangeHandlers;

        public BaselineComparer(
            BaselineDocument oldBaseline,
            BaselineDocument newBaseline,
            IEnumerable<Func<BreakingChangeCandidateContext, bool>> breakingChangeHandlers)
        {
            _oldBaseline = oldBaseline;
            _newBaseline = newBaseline;
            _breakingChangeHandlers = breakingChangeHandlers;
        }

        public IList<BreakingChange> GetDifferences()
        {
            var breakingChanges = new List<BreakingChange>();
            foreach (var type in _oldBaseline.Types)
            {
                var newType = GetNewTypeOrAddBreakingChange(type, breakingChanges);
                if (newType == null)
                {
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
                        var ctx = new BreakingChangeCandidateContext
                        {
                            NewBaseline = _newBaseline,
                            OldBaseline = _oldBaseline,
                            OldType = type,
                            NewType = newType,
                            OldMember = member
                        };

                        var isException = HandlePotentialBreakingChange(ctx);
                        if (!isException)
                        {
                            breakingChanges.Add(new BreakingChange(ctx.NewMember, type.Id));
                        }
                    }
                }
            }

            return breakingChanges;
        }

        private TypeBaseline GetNewTypeOrAddBreakingChange(TypeBaseline type, IList<BreakingChange> breakingChanges)
        {
            var newType = _newBaseline.FindType(type.Id);
            if (newType == null)
            {
                var ctx = new BreakingChangeCandidateContext
                {
                    NewBaseline = _newBaseline,
                    OldBaseline = _oldBaseline,
                    OldType = type
                };

                var isException = HandlePotentialBreakingChange(ctx);
                if (isException)
                {
                    newType = ctx.NewType;
                }
                else
                {
                    breakingChanges.Add(new BreakingChange(ctx.NewType, type.Id));
                }
            }

            return newType;
        }

        private bool HandlePotentialBreakingChange(BreakingChangeCandidateContext ctx)
        {
            var isException = false;
            foreach (var handler in _breakingChangeHandlers)
            {
                var noBreakingChange = handler(ctx);
                isException = isException || noBreakingChange;
            }

            return isException;
        }
    }
}
