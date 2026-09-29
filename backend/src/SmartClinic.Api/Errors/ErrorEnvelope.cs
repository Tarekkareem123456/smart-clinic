using System.Text.Json.Serialization;

namespace SmartClinic.Api.Errors;

/// <summary>
/// T-006 — the ONE error response shape for the entire API:
/// status / code / message / fields / correlationId.
///
/// All five keys are ALWAYS present (this is asserted by
/// SmartClinic.Api.Tests.ErrorEnvelopeTests as the "envelope shape
/// stability" requirement) — <see cref="Fields"/> is an empty array rather
/// than omitted/null when there are no field-level details, so clients
/// never need to check for the property's existence.
/// </summary>
public sealed class ErrorEnvelope
{
    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("code")]
    public string Code { get; init; } = default!;

    [JsonPropertyName("message")]
    public string Message { get; init; } = default!;

    [JsonPropertyName("fields")]
    public IReadOnlyList<ErrorFieldDto> Fields { get; init; } = Array.Empty<ErrorFieldDto>();

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; init; } = default!;
}

public sealed record ErrorFieldDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string? Message);
