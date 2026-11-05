using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIWidgets.Controls;

namespace Arc.Avalonia.Special.AIWidgets.Pages;

/// <summary>
/// Catalog page for the agent status pill. The demo opens the panel so the steps, the progress line and the
/// run controls are visible, and the row underneath walks the run through its states.
/// </summary>
public sealed class AgentStatusPage : ArcItemPage
{
    public override string ItemSlug => "agent-status";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-expanded",
        "state-working", "state-needs-input", "state-paused", "state-done", "state-failed"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = new ArcAgentStatus
        {
            IsExpanded = true,
            Status = ArcAgentRunStatus.Working,
            AgentName = "Claude Sonnet 5.5",
            ActivityLabel = "Running tests",
            ActivityIcon = "sparkles",
            ActivityCount = "8 of 12",
            ElapsedSeconds = 72,
            Steps = new List<ArcAgentStep>
            {
                new() { Id = "read", Label = "Read 12 files", State = ArcAgentStepState.Done, DurationSeconds = 1.9 },
                new() { Id = "edit", Label = "Edited src/billing/invoice.ts", State = ArcAgentStepState.Done, DurationSeconds = 2.5 },
                new() { Id = "tests", Label = "Running tests", State = ArcAgentStepState.Active },
                new() { Id = "push", Label = "Push the branch", State = ArcAgentStepState.Pending }
            }
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var hint = Sv.Caption("Try a state");
        hint.Margin = new Thickness(0, 0, 6, 0);
        row.Children.Add(hint);
        foreach (var (label, value) in new (string, ArcAgentRunStatus)[]
                 {
                     ("Working", ArcAgentRunStatus.Working),
                     ("Needs input", ArcAgentRunStatus.NeedsInput),
                     ("Paused", ArcAgentRunStatus.Paused),
                     ("Done", ArcAgentRunStatus.Done),
                     ("Failed", ArcAgentRunStatus.Failed)
                 })
        {
            var button = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Content = label };
            button.Click += (_, _) => status.Status = value;
            row.Children.Add(button);
        }

        return new StackPanel { Spacing = 16, Children = { status, row } };
    }

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A header, title bar or composer needs to say at a glance that an agent is running, what it is doing and for how long.",
        "An agent pauses for approval, so the person approves or denies without opening a full run view.",
        "Background runs that people check on occasionally: the pill stays small, the panel carries the steps."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Tab reaches the pill, then the inline Deny and Approve or Retry; with the panel open it continues to Pause, Stop and Open run.",
        "Enter or Space opens and closes the panel; Escape closes it from inside and returns focus to the pill.",
        "The accessible name reads the agent, the activity, the count and the elapsed time, and every state pairs its ring and glyph with a word."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "A new activity rolls in while the old one leaves, and the pill's width follows on the morph spring.",
        "The live ring sweeps while working, freezes when paused, becomes an accent dot for approval and closes for done or failed.",
        "Counts and the timer change in place in tabular numerals, so the pill never reflows while it counts."
    };

    public override string? ImplementationNotes =>
        "Recreated from the public agent-status documentation. The panel expands in place in the layout rather than floating over the page on a shadow, " +
        "the working label has no shimmer band (text-shimmer belongs to the Effects project) and the agent mark is a generic four point star rather than a brand glyph. " +
        "The elapsed timer counts while the status is Working and holds its value in every other state.";
}
