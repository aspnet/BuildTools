using System.Diagnostics;

namespace ApiCheck.Description
{
    [DebuggerDisplay("{Id,nq}")]
    public class ApiElement
    {
        public virtual string Id { get; }
    }
}
