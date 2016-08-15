using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiCheck.Baseline;

namespace ApiCheck
{
    public class BreakingChange
    {
        public BreakingChange(BaselineItem oldItem, BaselineItem newItem, BreakingChangeTypes level)
        {
            OldItem = oldItem;
            NewItem = newItem ?? new BaselineItem("Element not found");
            Level = level;
        }

        public BaselineItem OldItem { get; set; }
        public BaselineItem NewItem { get; set; }
        public BreakingChangeTypes Level { get; set; }
    }
}
