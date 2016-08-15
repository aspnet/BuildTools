using System;
using System.Collections.Generic;

namespace ApiCheck.Baseline
{
    public class BaselineDocument
    {
        public string AssemblyIdentity { get; set; }
        public IList<TypeBaseline> Types { get; } = new List<TypeBaseline>();

        public TypeBaseline FindType(string id)
        {
            foreach (var type in Types)
            {
                if (string.Equals(id, type.Id, StringComparison.Ordinal))
                {
                    return type;
                }
            }

            return null;
        }
    }
}
