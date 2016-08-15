using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ApiCheck.Baseline
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BaselineVisibility
    {
        Public,
        Protected,
        Internal,
        ProtectedInternal,
        Private
    }
}
