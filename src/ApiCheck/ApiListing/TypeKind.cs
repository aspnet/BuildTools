using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ApiCheck.Baseline
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TypeKind
    {
        Struct,
        Interface,
        Class,
        Enumeration
    }
}
