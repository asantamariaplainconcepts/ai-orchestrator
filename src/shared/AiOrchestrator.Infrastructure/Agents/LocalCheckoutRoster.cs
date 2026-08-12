using System.Collections.Concurrent;

namespace AiOrchestrator.ServiceDefaults.Agents;

/// <summary>
/// Where a Local Run's own checkout lives, and which of them this host may remove (#331, design D3).
/// One definition, used by both the thing that <b>creates</b> checkouts and the sweep that
/// <b>deletes</b> them — the <see cref="Sbx.SbxSandboxRoster"/> discipline, adopted here for the same
/// reason: a sweep that cannot tell its own artifacts from someone else's is a sweep that eventually
/// deletes someone else's.
/// <para>
/// <b>The root is outside the configured folder.</b> Under it, a checkout would surface in the
/// owner's file watchers, their editor and their <c>git status</c> — the folder they were promised
/// stays untouched would visibly gain a directory. Temp is where this product already puts host-side
/// working directories (<c>aio-carry-*</c> staging, <c>aio-workspace-*</c> archives), so the
/// namespace is the one a reader already recognises.
/// </para>
/// <para>
/// <b>Ownership is in-process and deliberately so.</b> A checkout is claimed while a Run of
/// <i>this</i> process holds it, and the sweep runs at startup, before any Run of this process has
/// claimed anything. Two orchestrators sharing one machine would reap each other's live checkouts —
/// out of scope by DEC-016 (one owner, one machine), and written here rather than discovered.
/// </para>
/// </summary>
static class LocalCheckoutRoster
{
    /// <summary>The name this host claims. Everything else in temp belongs to somebody else.</summary>
    const string ClaimedPrefix = "aio-checkout-";

    /// <summary>Whether this host created the checkout by that name, and may therefore remove it.</summary>
    public static bool Claims(string? name) =>
        name is not null && name.StartsWith(ClaimedPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Where checkouts live. Temp by default; overridable only so a test can own a root of its
    /// own, because a sweep and a live Run sharing the machine's real temp is exactly the
    /// cross-process hazard this class documents — two test projects would reproduce it.
    /// </summary>
    public static string DefaultRoot => Path.GetTempPath();

    /// <summary>A fresh checkout path, in the claimed namespace. Not created — that is git's job.</summary>
    public static string NewCheckout(string? root = null) =>
        Path.Combine(root ?? DefaultRoot, $"{ClaimedPrefix}{Guid.NewGuid():N}");

    /// <summary>Checkouts a Run of this process currently occupies, which no sweep may touch.</summary>
    static readonly ConcurrentDictionary<string, byte> Live = new(StringComparer.Ordinal);

    public static void Occupy(string checkout) => Live[checkout] = 0;

    public static void Release(string checkout) => Live.TryRemove(checkout, out _);

    /// <summary>
    /// Checkouts in the claimed namespace that no live Run of this process owns. Returns empty when
    /// temp cannot be read — "cannot tell" is treated as "nothing to act on", which is what keeps a
    /// startup sweep from throwing on a machine it does not understand.
    /// </summary>
    public static IReadOnlyList<string> Abandoned(string? root = null)
    {
        string[] candidates;
        try
        {
            candidates = Directory.GetDirectories(root ?? DefaultRoot, $"{ClaimedPrefix}*");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return
        [
            .. candidates.Where(path => Claims(Path.GetFileName(path)) && !Live.ContainsKey(path)),
        ];
    }
}
