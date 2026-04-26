using System.Text.Json;
using System.Text.Json.Serialization;
using Alba;

namespace MartenStarter.Tests.Integration;

// Alba's default JSON reader doesn't know about our JsonStringEnumConverter, so
// enum-valued response fields ("Buy", "Priced", ...) fail to deserialize. This
// helper deserializes with options matching what the app writes.
internal static class ScenarioResultExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static T FromJson<T>(this IScenarioResult result)
        => JsonSerializer.Deserialize<T>(result.ReadAsText(), Options)
           ?? throw new InvalidOperationException($"Response body was null or failed to deserialize to {typeof(T).Name}.");
}
