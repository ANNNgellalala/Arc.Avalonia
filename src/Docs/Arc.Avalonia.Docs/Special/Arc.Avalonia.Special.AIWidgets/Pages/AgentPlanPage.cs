using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIWidgets.Controls;

namespace Arc.Avalonia.Special.AIWidgets.Pages;

/// <summary>
/// Catalog page for the agent plan. Two instances show the same plan as an editable draft and as a live
/// checklist, so the approve step and the running state can be compared side by side.
/// </summary>
public sealed class AgentPlanPage : ArcItemPage
{
    public override string ItemSlug => "agent-plan";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-draft", "state-running", "state-paused", "state-done",
        "variant-step-failed", "variant-step-skipped"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var draft = new ArcAgentPlan
        {
            Status = ArcAgentPlanStatus.Draft,
            Goal = "Add rate limiting to the public API",
            ModelName = "Claude Opus 5.5",
            Steps = new List<ArcAgentPlanStep>
            {
                Plan("a", "Add a sliding window limiter to the API middleware"),
                Plan("b", "Keep request counters in Redis with a 60 second TTL"),
                Plan("c", "Return a 429 with a Retry-After header"),
                Plan("d", "Cover the limiter with six request tests"),
                Plan("e", "Document the limit in the API reference")
            }
        };

        var running = new ArcAgentPlan
        {
            Status = ArcAgentPlanStatus.Running,
            Goal = "Add rate limiting to the public API",
            Steps = new List<ArcAgentPlanStep>
            {
                Done("a", "Add a sliding window limiter to the API middleware", 1.4),
                Done("b", "Keep request counters in Redis with a 60 second TTL", 2.1),
                new()
                {
                    Id = "c",
                    Title = "Return a 429 with a Retry-After header",
                    State = ArcAgentPlanStepState.Active,
                    SubSteps = new[] { "Read src/middleware/limits.ts", "Ran 6 tests" }
                },
                new() { Id = "d", Title = "Cover the limiter with six request tests", State = ArcAgentPlanStepState.Pending },
                new() { Id = "e", Title = "Document the limit in the API reference", State = ArcAgentPlanStepState.Pending }
            }
        };

        var paused = new ArcAgentPlan
        {
            Status = ArcAgentPlanStatus.Paused,
            Goal = "Add rate limiting to the public API",
            Steps = new List<ArcAgentPlanStep>
            {
                Done("a", "Add a sliding window limiter to the API middleware", 1.4),
                Done("b", "Keep request counters in Redis with a 60 second TTL", 2.1),
                new()
                {
                    Id = "c",
                    Title = "Return a 429 with a Retry-After header",
                    State = ArcAgentPlanStepState.Active,
                    SubSteps = new[] { "Read src/middleware/limits.ts", "Ran 6 tests" }
                },
                new() { Id = "d", Title = "Cover the limiter with six request tests", State = ArcAgentPlanStepState.Pending }
            }
        };

        var done = new ArcAgentPlan
        {
            Status = ArcAgentPlanStatus.Done,
            Goal = "Add rate limiting to the public API",
            Summary = "Rate limiting is live; the Redis fallback was skipped.",
            Steps = new List<ArcAgentPlanStep>
            {
                Done("a", "Add a sliding window limiter to the API middleware", 1.4),
                Done("b", "Keep request counters in Redis with a 60 second TTL", 2.1),
                Done("c", "Return a 429 with a Retry-After header", 1.3),
                new()
                {
                    Id = "d",
                    Title = "Fall back to in-memory counters when Redis is down",
                    State = ArcAgentPlanStepState.Failed,
                    Error = "Redis connection refused after 3 retries",
                    DurationSeconds = 1.1
                },
                new() { Id = "e", Title = "Document the limit in the API reference", State = ArcAgentPlanStepState.Skipped }
            }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 16,
            RowSpacing = 16
        };
        grid.Children.Add(draft);

        var topRight = running;
        Grid.SetColumn(topRight, 1);
        grid.Children.Add(topRight);

        var bottomLeft = paused;
        Grid.SetRow(bottomLeft, 1);
        grid.Children.Add(bottomLeft);

        var bottomRight = done;
        Grid.SetColumn(bottomRight, 1);
        Grid.SetRow(bottomRight, 1);
        grid.Children.Add(bottomRight);

        return grid;
    }

    private static ArcAgentPlanStep Plan(string id, string title) =>
        new() { Id = id, Title = title, State = ArcAgentPlanStepState.Pending };

    private static ArcAgentPlanStep Done(string id, string title, double seconds) =>
        new() { Id = id, Title = title, State = ArcAgentPlanStepState.Done, DurationSeconds = seconds };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "An agent shows and gets approval for its plan before it edits code, runs commands or touches production.",
        "A person needs to adjust the plan, then watch the same plan execute as a live checklist.",
        "A side panel tracks a multi-step task with a current step, failures that need a decision and a final summary."
    };


    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "In a draft, Tab moves through each step's grip, title and remove button, then Add step, Keep planning and Approve plan.",
        "Arrow keys on a grip, or Alt with the arrow keys on a title, move that step; Enter saves a rename and Escape undoes it.",
        "Every step announces its state before its title and pairs that state with its own shape, so colour is never the only signal."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Reorders, inserts and removals move on the smooth spring, so rows slide and grow without overshoot.",
        "Completing a step draws its check while the disc fades up, and the title settles from primary to secondary text.",
        "The current step shimmers beside a small spinner and stops when the run is paused; reduced motion drops travel and keeps every state."
    };

    public override string? ImplementationNotes =>
        "Recreated from the public agent-plan documentation. Keep planning appends a canned suggestion instead of streaming one from a model, " +
        "and the run advances on a fixed clock rather than your agent's events. Renaming uses a plain Avalonia text field inside the row because the " +
        "Arc input controls live in another project; removal is always visible rather than revealed on hover.";
}
