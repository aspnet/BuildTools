using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiCheck.Baseline;

namespace ApiCheck
{
    public class BreakingChangeContext
    {
        public BaselineDocument OldBaseline { get; set; }
        public BaselineDocument NewBaseline { get; set; }
        public TypeBaseline OldType { get; set; }
        public TypeBaseline NewType { get; set; }
        public MemberBaseline OldMember { get; set; }
        public MemberBaseline NewMember { get; set; }
        public BreakingChangeTypes BreakType { get; set; }
    }
}
