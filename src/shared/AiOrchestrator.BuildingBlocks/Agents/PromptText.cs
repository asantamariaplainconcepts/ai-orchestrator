namespace AiOrchestrator.BuildingBlocks.Agents;

/// <summary>
/// What a prompt file's text means to this product.
/// <para>
/// Shared rather than private to the Run path since #190: the starter catalogue promises that a file
/// taken from it behaves identically whether this product runs it or a local agent runner does, and
/// that promise is only testable against the <i>same</i> routine the Run path uses. A test that
/// reimplemented the rule would assert that two implementations agree today.
/// </para>
/// </summary>
public static class PromptText
{
    /// <summary>
    /// Drops a leading YAML frontmatter block. That block is how <i>another</i> runner is told what
    /// to do with the file, and this product's wiring is the Automation — so honouring a
    /// <c>model:</c> line would let a file in somebody's repository choose what this product spends,
    /// and a <c>tools:</c> line would let it grant itself powers the Automation withheld.
    /// </summary>
    public static string WithoutFrontmatter(string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var opening = Array.FindIndex(lines, line => line.Trim().Length > 0);

        if (opening < 0 || lines[opening].Trim() != "---")
        {
            return content.Trim();
        }

        for (var index = opening + 1; index < lines.Length; index++)
        {
            if (lines[index].Trim() is "---" or "...")
            {
                return string.Join('\n', lines.Skip(index + 1)).Trim();
            }
        }

        // An opening delimiter that never closes is not frontmatter. Treating it as such would
        // swallow the entire file and then refuse it as empty — a confusing lie about a file whose
        // real problem is a missing '---'.
        return content.Trim();
    }
}
