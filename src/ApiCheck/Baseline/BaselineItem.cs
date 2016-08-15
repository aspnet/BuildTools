using System.Diagnostics;

namespace ApiCheck.Baseline
{
    [DebuggerDisplay("{Id,nq}")]
    public class BaselineItem
    {
        protected BaselineItem()
        {
        }

        public BaselineItem(string id)
        {
            Id = id;
        }

        public virtual string Id { get; }
    }
}
