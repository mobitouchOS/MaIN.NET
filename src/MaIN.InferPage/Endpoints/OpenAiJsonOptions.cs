using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaIN.InferPage.Endpoints;

public static class OpenAiJsonOptions
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
