using System.Collections.Generic;

namespace ApiCheck.Baseline
{
    public class GenericConstraintBaseline : BaselineItem
    {
        public override string Id => string.Join(" ", ParameterName, ":", GetConstraints());

        private string GetConstraints()
        {
            var constraints = new List<string>();
            foreach (var type in BaseTypeOrInterfaces)
            {
                constraints.Add(type);
            }
            if (Class)
            {
                constraints.Add("class");
            }

            if (Struct)
            {
                constraints.Add("struct");
            }

            if (!Struct && New)
            {
                constraints.Add("new()");
            }

            return string.Join(", ", constraints);
        }

        public bool New { get; internal set; }
        public bool Class { get; internal set; }
        public bool Struct { get; internal set; }
        public string ParameterName { get; set; }
        public IList<string> BaseTypeOrInterfaces { get; } = new List<string>();
    }
}
