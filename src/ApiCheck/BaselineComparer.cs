using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiCheck.Baseline;
using Microsoft.Extensions.CommandLineUtils;

namespace ApiCheck
{
    public class BaselineComparer
    {
        private readonly BaselineDocument _newBaseline;
        private readonly BaselineDocument _oldBaseline;
        private readonly BreakingChangeTypes _breakingChangeType;
        private readonly IEnumerable<Func<BreakingChangeContext, bool>> _breakingChangeHandlers;

        public BaselineComparer(
            BaselineDocument oldBaseline,
            BaselineDocument newBaseline,
            BreakingChangeTypes breakingChangeLevel,
            IEnumerable<Func<BreakingChangeContext, bool>> breakingChangeHandlers)
        {
            _oldBaseline = oldBaseline;
            _newBaseline = newBaseline;
            _breakingChangeType = breakingChangeLevel;
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
                    continue;
                }

                foreach (var member in type.Members)
                {
                    var newMember = newType.FindMember(member.Id);
                    if (newMember == null)
                    {
                        var ctx = new BreakingChangeContext
                        {
                            NewBaseline = _newBaseline,
                            OldBaseline = _oldBaseline,
                            OldType = type,
                            NewType = newType,
                            OldMember = member
                        };

                        var isException = HandlePotentialBreakingChange(ctx);
                        if (!isException || !ValidBreakingChange(ctx))
                        {
                            breakingChanges.Add(new BreakingChange(ctx.OldMember, ctx.NewMember, ctx.BreakType));
                        }
                    }
                }
            }

            return breakingChanges;
        }

        private bool ValidBreakingChange(BreakingChangeContext ctx)
        {
            // The change was contextualized as a non breaking change.
            if (ctx.BreakType == BreakingChangeTypes.None)
            {
                return true;
            }

            // We are looking for any type of breaking change, so no
            // breaking change is valid.
            if (_breakingChangeType == BreakingChangeTypes.All)
            {
                return false;
            }

            // Undefined means no exception contextualized the change
            // so we identify it as a breaking change.
            if (ctx.BreakType == BreakingChangeTypes.Undefined)
            {
                return false;
            }

            // The change was marked as source and binary breaking
            // change or is of the same type of breaking change we are
            // looking for.
            if (ctx.BreakType == BreakingChangeTypes.All ||
                ctx.BreakType == _breakingChangeType)
            {
                return false;
            }

            return true;
        }

        private TypeBaseline GetNewTypeOrAddBreakingChange(TypeBaseline type, IList<BreakingChange> breakingChanges)
        {
            var newType = _newBaseline.FindType(type.Id);
            if (newType == null)
            {
                var ctx = new BreakingChangeContext
                {
                    NewBaseline = _newBaseline,
                    OldBaseline = _oldBaseline,
                    OldType = type
                };

                var isException = HandlePotentialBreakingChange(ctx);
                if (isException || ValidBreakingChange(ctx))
                {
                    newType = ctx.NewType;
                }
                else
                {
                    breakingChanges.Add(new BreakingChange(type, ctx.NewType, ctx.BreakType));
                }
            }

            return newType;
        }

        private bool HandlePotentialBreakingChange(BreakingChangeContext ctx)
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
