namespace AiOrchestrator.BuildingBlocks.Domain;

/// <summary>
/// The hold — the one way work waits for a person (BR-007, DEC-067).
/// <para>
/// A Story carrying <see cref="Label"/> starts nothing: not a matched event (UC-011), not
/// <i>Run now</i> (UC-012, BR-013). An Automation that stops for a person applies it among the
/// marks it already writes, so stopping needs no field of its own; a person clears it as an
/// ordinary label change (UC-008) and the resulting event matches like any other.
/// </para>
/// <para>
/// It lives in BuildingBlocks rather than in any module's Contracts because three places need the
/// same word and none of them owns it: Runs refuses on it, Projects ships it in the starter
/// catalogue's wiring, and the frontend renders it. A constant in one module's surface would make
/// the other two depend on that module for a piece of product vocabulary.
/// </para>
/// <para>
/// Fixed, never per-project (design D4). A configurable name costs a Project field, a migration and
/// a resolution at every surface that renders a hold, to answer a question nobody has asked.
/// </para>
/// </summary>
public static class StoryHold
{
    /// <summary>
    /// The reserved label. Lower-case here is the canonical spelling the product writes;
    /// <see cref="IsHeld"/> is what decides whether a Story carries it, and that folds case.
    /// </summary>
    public const string Label = "hitl";

    /// <summary>
    /// Whether this label <i>is</i> the hold, compared the way the vendor compares labels.
    /// <para>
    /// Case-insensitive on purpose (DEC-056): BR-003's identity folds case and so does matching,
    /// and an Admin who typed <c>HITL</c> in the vendor's own casing would otherwise watch the flow
    /// run straight past a hold they believed they had applied.
    /// </para>
    /// </summary>
    public static bool Is(string? label) =>
        string.Equals(label?.Trim(), Label, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether these labels hold the Story. An empty or absent set never holds.</summary>
    public static bool IsHeld(IEnumerable<string>? labels) => labels?.Any(Is) ?? false;
}
