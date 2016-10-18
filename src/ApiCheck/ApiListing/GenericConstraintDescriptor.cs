using System.Collections.Generic;

namespace ApiCheck.Description
{
    public class GenericConstraintDescriptor : ApiElement
    {
        public override string Id => ParameterName + " : " + GetConstraints();

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

        public bool New { get; set; }
        public bool Class { get; set; }
        public bool Struct { get; set; }
        public string ParameterName { get; set; }
        public IList<string> BaseTypeOrInterfaces { get; } = new List<string>();
    }
}
