using System.Text.Json.Serialization;

namespace SMEFLOWSystem.WebAPI.Exceptions;

public sealed class ApiProblemDetails :
    Microsoft.AspNetCore.Mvc.ProblemDetails
{
    [JsonPropertyName("traceId")]
    public required string TraceId { get; init; }

    [JsonPropertyName("errorCode")]
    public required string ErrorCode { get; init; }

    [JsonPropertyName("error")]
    public required string Error { get; init; }

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; init; }
}
