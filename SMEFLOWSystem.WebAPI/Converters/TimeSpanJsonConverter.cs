using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SMEFLOWSystem.WebAPI.Converters;

public sealed class TimeSpanJsonConverter : JsonConverter<TimeSpan>
{
    private static readonly string[] Formats =
    {
        "c",
        "hh\\:mm\\:ss",
        "h\\:mm\\:ss",
        "hh\\:mm",
        "h\\:mm"
    };

    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected string for TimeSpan");

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException("TimeSpan value is empty");

        if (TimeSpan.TryParseExact(value, Formats, CultureInfo.InvariantCulture, out var result))
            return result;

        throw new JsonException($"Invalid TimeSpan format: {value}");
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("c", CultureInfo.InvariantCulture));
    }
}
