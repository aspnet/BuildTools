using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiCheck.Baseline;

namespace ApiCheck
{
    public class BreakingChange
    {
        public BreakingChange(BaselineItem oldItem, string context = null)
        {
            Context = context;
            Item = oldItem;
        }
        public string Context { get; }

        public BaselineItem Item { get; }

        public override string ToString()
        {
            return $"{Context}: {Item.Id}";
        }
    }
}
