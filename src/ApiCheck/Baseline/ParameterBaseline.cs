using System.Collections.Generic;

namespace ApiCheck.Baseline
{
    public class ParameterBaseline : BaselineItem
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public BaselineParameterDirection Direction { get; set; }
        public string DefaultValue { get; set; }
        public bool IsParams { get; set; }

        public override string Id
        {
            get
            {
                return string.Join(" ", GetComponents());
            }
        }

        private IEnumerable<string> GetComponents()
        {
            switch (Direction)
            {
                case BaselineParameterDirection.In:
                    break;
                case BaselineParameterDirection.Out:
                    yield return "out";
                    break;
                case BaselineParameterDirection.Ref:
                    yield return "ref";
                    break;
                default:
                    break;
            }

            if (IsParams)
            {
                yield return "params";
            }

            yield return Type;
            yield return Name;

            if (DefaultValue != null)
            {
                yield return "=";
                yield return DefaultValue;
            }
        }
    }
}
