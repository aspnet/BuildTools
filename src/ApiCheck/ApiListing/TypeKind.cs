using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ApiCheck.Description
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
