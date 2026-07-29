using AiOrchestrator.Modules.Projects.Domain;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.UnitTests;

/// <summary>
/// BR-003's overlap rule, made precise by design D1 and pinned here.
/// <para>
/// This is the whole of the change's domain logic, and it is the kind of rule that gets quietly
/// re-interpreted by the next person who touches it. Every row of the D1 table is asserted, in
/// both directions, because "some Story could match both" is a symmetric claim.
/// </para>
/// </summary>
public class Automation_Should_Constraint
{
    static Automation Trigger(string label, string? state = null, bool enabled = true)
    {
        var automation = Automation.Create(
            Guid.CreateVersion7(),
            label,
            state,
            AutomationAction.ImplementToPullRequest,
            AgentRuntime.ClaudeCodeHeadless,
            requiresApproval: false,
            TimeSpan.FromMinutes(30)
        );

        if (!enabled)
        {
            // Disabling is #15's capability; the rule already has to account for the state.
            typeof(Automation)
                .GetProperty(nameof(Automation.Enabled))!
                .SetValue(automation, false);
        }

        return automation;
    }

    [Fact]
    public void Triggers_Should_OverlapOnTheSameLabelAndState()
    {
        Trigger("ai:implement", "open").Overlaps(Trigger("ai:implement", "open")).ShouldBeTrue();
    }

    [Fact]
    public void Triggers_Should_NotOverlapOnDifferentStates()
    {
        // No Story carries two states at once, so neither rule can ever match the other's Stories.
        Trigger("ai:implement", "open")
            .Overlaps(Trigger("ai:implement", "closed"))
            .ShouldBeFalse();
    }

    [Fact]
    public void Triggers_Should_NotOverlapOnDifferentLabels()
    {
        Trigger("ai:implement", "open").Overlaps(Trigger("ai:refine", "open")).ShouldBeFalse();
    }

    [Fact]
    public void Triggers_Should_OverlapWhenOneIsUnconstrainedByState()
    {
        // The case that matters. A Story labelled ai:implement and open matches both, which is
        // precisely the ambiguity BR-003 exists to prevent — and the case a unique index on
        // (label, state) would silently permit.
        var any = Trigger("ai:implement");
        var specific = Trigger("ai:implement", "open");

        any.Overlaps(specific).ShouldBeTrue("an unconstrained trigger subsumes a specific one");
        specific
            .Overlaps(any)
            .ShouldBeTrue("and the relation is symmetric — order must not change the verdict");
    }

    [Fact]
    public void Triggers_Should_OverlapWhenBothAreUnconstrained()
    {
        Trigger("ai:implement").Overlaps(Trigger("ai:implement")).ShouldBeTrue();
    }

    [Fact]
    public void Triggers_Should_IgnoreDisabledAutomations()
    {
        // BR-003 says "existing *enabled*". A disabled rule matches no events, so it cannot
        // compete for one — and blocking on it would leave an Admin unable to replace a rule they
        // had deliberately switched off.
        Trigger("ai:implement", "open")
            .Overlaps(Trigger("ai:implement", "open", enabled: false))
            .ShouldBeFalse();
        Trigger("ai:implement", "open", enabled: false)
            .Overlaps(Trigger("ai:implement", "open"))
            .ShouldBeFalse();
    }

    [Fact]
    public void Triggers_Should_CompareLabelsTheWayTheVendorDoes()
    {
        // This asserted the opposite until #147, on the stated grounds that "vendor labels are
        // case-sensitive strings we mirror verbatim; folding case here would invent a rule the
        // vendor does not have". That was a claim about GitHub that nobody had checked, and it is
        // false — exercised rather than argued:
        //
        //   gh api /repos/{owner}/{repo}/labels/bug  -> bug
        //   gh api /repos/{owner}/{repo}/labels/BUG  -> bug
        //   gh api /repos/{owner}/{repo}/labels/Bug  -> bug
        //
        // One label, three spellings. So the rule the vendor has is exactly the one this now
        // enforces, and the old comment invented the *absence* of it (DEC-056).
        Trigger("AI:Implement", "open")
            .Overlaps(Trigger("ai:implement", "open"))
            .ShouldBeTrue();
    }

    [Fact]
    public void AnExactDuplicate_Should_BeTheSameTriggerEvenWhenDisabled()
    {
        // Subsumption ignores disabled Automations because it is about matching (#147, design D3).
        // Identity does not: two rows carrying one trigger are one trigger either way.
        Trigger("ai:implement", "open", enabled: false)
            .IsSameTriggerAs(Trigger("AI:IMPLEMENT", "OPEN"))
            .ShouldBeTrue();
    }

    [Fact]
    public void ABroadTrigger_Should_NotBeTheSameAsANarrowOne()
    {
        // Identity is not subsumption: these two overlap, and they are not the same trigger.
        Trigger("ai:implement").IsSameTriggerAs(Trigger("ai:implement", "open")).ShouldBeFalse();
    }

    [Fact]
    public void Automation_Should_BeEnabledWhenCreated()
    {
        Trigger("ai:implement").Enabled.ShouldBeTrue();
    }
}
