using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ApiCheck.Description
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MemberKind
    {
        Constructor,
        Method,
        Field
    }
}
