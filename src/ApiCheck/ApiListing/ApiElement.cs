using System.Diagnostics;

namespace ApiCheck.Baseline
{
    [DebuggerDisplay("{Id,nq}")]
    public class ApiElement
    {
        protected ApiElement()
        {
        }

        public ApiElement(string id)
        {
            Id = id;
        }

        public virtual string Id { get; }
    }
}
