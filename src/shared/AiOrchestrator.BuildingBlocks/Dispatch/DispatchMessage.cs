using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiOrchestrator.BuildingBlocks.Dispatch;

/// <summary>
/// The wire format of a dispatch message: a Run id, and a version so the worker can refuse a
/// shape it does not understand rather than misread it.
/// <para>
/// Serialisation lives beside the contract, not in the queue implementation, because the producer
/// and the consumer are different processes that must agree. A change here is a change to both.
/// </para>
/// </summary>
public sealed record DispatchMessage(
    [property: JsonPropertyName("v")] int Version,
    [property: JsonPropertyName("runId")] Guid RunId
)
{
    /// <summary>Bumped only when the shape changes incompatibly; the worker rejects anything else.</summary>
    public const int CurrentVersion = 1;

    static readonly JsonSerializerOptions Format = new(JsonSerializerDefaults.Web);

    public static DispatchMessage For(Guid runId) => new(CurrentVersion, runId);

    public string Serialise() => JsonSerializer.Serialize(this, Format);

    /// <summary>
    /// Returns null for anything this worker cannot safely act on — malformed JSON, an unknown
    /// version, an empty id. The caller discards it rather than guessing: acting on a
    /// misunderstood message is worse than dropping one, and BR-004 means nothing will retry it
    /// into correctness.
    /// </summary>
    public static DispatchMessage? TryParse(string payload)
    {
        try
        {
            var message = JsonSerializer.Deserialize<DispatchMessage>(payload, Format);

            return message is { Version: CurrentVersion } && message.RunId != Guid.Empty
                ? message
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
