using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ApiCheck
{
    public class BreakingChangeHandlers
    {
        public static bool FindTypeUsingFullName(BreakingChangeContext ctx)
        {
            if (ctx.NewType != null)
            {
                return false;
            }

            var oldType = ctx.OldType;
            var baseline = ctx.NewBaseline;

            var oldName = oldType.Name;
            var foundMatch = baseline.Types
                .FirstOrDefault(t => t.Name.Equals(oldName, StringComparison.Ordinal));

            if (foundMatch != null)
            {
                ctx.NewType = foundMatch;
            }

            return false;
        }

        public static bool FindMemberUsingName(BreakingChangeContext ctx)
        {
            if (ctx.OldMember == null)
            {
                return false;
            }

            var oldMember = ctx.OldMember;

            var oldName = oldMember.Name;
            var foundMatch = ctx.NewType.Members
                .FirstOrDefault(t => t.Name.Equals(oldName, StringComparison.Ordinal));

            if (foundMatch != null)
            {
                ctx.NewMember = foundMatch;
            }

            return false;
        }
    }
}
