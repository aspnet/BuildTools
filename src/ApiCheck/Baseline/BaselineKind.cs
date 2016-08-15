using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ApiCheck.Baseline
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BaselineKind
    {
        Struct,
        Interface,
        Class
    }
}
