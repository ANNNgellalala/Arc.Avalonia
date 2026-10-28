using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIWidgets.Controls;

namespace Arc.Avalonia.Special.AIWidgets.Pages;

/// <summary>
/// Catalog page for the agent timeline. The demo is one incident investigation with five agents, a failed query,
/// its retry and seven handoffs; play, restart and the speed control replay it, and pressing a span pins it.
/// </summary>
public sealed class AgentTimelinePage : ArcItemPage
{
    public override string ItemSlug => "agent-timeline";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var timeline = new ArcAgentTimeline
        {
            Title = "Incident investigation",
            Duration = 14.4,
            Agents = new List<ArcTimelineAgent>
            {
                new() { Id = "lead", Name = "Orchestrator", Model = "Claude Opus 5.5", PricePerMillion = 8 },
                new() { Id = "search", Name = "Researcher", Model = "Claude Haiku 5.5", PricePerMillion = 0.2 },
                new() { Id = "sql", Name = "Analyst", Model = "Claude Haiku 5.5", PricePerMillion = 0.2 },
                new() { Id = "docs", Name = "Writer", Model = "Claude Sonnet 5.5", PricePerMillion = 3 },
                new() { Id = "review", Name = "Reviewer", Model = "Claude Opus 5.5", PricePerMillion = 8 }
            },
            Spans = new List<ArcTimelineSpan>
            {
                new() { Id = "s1", AgentId = "lead", Kind = "thinking", Start = 0, End = 1.4, Label = "Planned the investigation", Tokens = 1200 },
                new() { Id = "s2", AgentId = "lead", Kind = "waiting", Start = 1.4, End = 5.8, Label = "Waiting on the researcher" },
                new() { Id = "s3", AgentId = "search", Kind = "tool", Tool = "Web search", Start = 1.6, End = 3.8, Label = "Searched the incident log", Tokens = 2200 },
                new() { Id = "s4", AgentId = "search", Kind = "thinking", Start = 3.8, End = 5.4, Label = "Ranked the matches", Tokens = 900 },
                new() { Id = "s5", AgentId = "lead", Kind = "thinking", Start = 5.8, End = 6.4, Label = "Split the work", Tokens = 500 },
                new() { Id = "s6", AgentId = "lead", Kind = "waiting", Start = 6.6, End = 12.6, Label = "Waiting on the analyst and the writer" },
                new() { Id = "s7", AgentId = "sql", Kind = "tool", Tool = "PostgreSQL", Start = 6.8, End = 9.4, Label = "Pulled error rates by hour", Tokens = 2600 },
                new() { Id = "s8", AgentId = "sql", Kind = "tool", Tool = "PostgreSQL", Start = 9.5, End = 10.3, Label = "Joined sessions on the raw table", Tokens = 800, Failed = true },
                new() { Id = "s9", AgentId = "sql", Kind = "tool", Tool = "PostgreSQL", Start = 10.5, End = 12.4, Label = "Re-ran with a narrower window", Tokens = 1900, Retry = true },
                new() { Id = "s10", AgentId = "docs", Kind = "thinking", Start = 6.8, End = 9.0, Label = "Drafted the finding", Tokens = 1400 },
                new() { Id = "s11", AgentId = "docs", Kind = "tool", Tool = "Notion", Start = 9.0, End = 10.6, Label = "Saved the draft", Tokens = 1100 },
                new() { Id = "s12", AgentId = "review", Kind = "thinking", Start = 12.6, End = 13.6, Label = "Checked the numbers", Tokens = 1000 },
                new() { Id = "s13", AgentId = "lead", Kind = "thinking", Start = 13.6, End = 14.4, Label = "Summarized the run", Tokens = 600 }
            },
            Handoffs = new List<ArcTimelineHandoff>
            {
                new() { From = "lead", To = "search", At = 1.4, Label = "Find the error spike" },
                new() { From = "search", To = "lead", At = 5.6, Label = "Two candidate spikes" },
                new() { From = "lead", To = "sql", At = 6.6, Label = "Error rates per hour" },
                new() { From = "lead", To = "docs", At = 6.6, Label = "Draft the finding" },
                new() { From = "sql", To = "lead", At = 12.5, Label = "Rate doubled at 14:00" },
                new() { From = "docs", To = "lead", At = 10.7, Label = "Draft ready" },
                new() { From = "lead", To = "review", At = 12.6, Label = "Check the numbers" },
                new() { From = "review", To = "lead", At = 13.7, Label = "Numbers check out" }
            }
        };

        return Sv.Card(timeline, 16);
    }

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Run history or a trace viewer shows how an orchestrator split work across subagents and how long each part took.",
        "A finished run is replayed in a demo or changelog with real numbers for time, tokens and cost.",
        "A multi-agent run is followed live as events stream in from the backend."
    };


    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Tab reaches the plot, the play, restart and speed controls; arrow keys move between spans and Shift with the arrows scrubs time.",
        "Enter or Space pins the focused span and Escape unpins it; a pinned span dims everything off its path.",
        "Every span name reads the agent, the kind, the tool, failure, what it did, its time range and its tokens."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Spans grow with the playhead, an active span keeps a square leading edge and a short shine, and its corner rounds off when it ends.",
        "Handoff elbows draw in as they happen and their port dot lands after the connector.",
        "When the run ends the playhead fades and the status settles into the summary; reduced motion opens on the finished run."
    };

    public override string? ImplementationNotes =>
        "Recreated from the public agent-timeline documentation. Spans are drawn and hit tested rather than being individual buttons, so the plot is " +
        "one tab stop with arrow key navigation instead of a roving tab index, and the span detail appears on one line under the plot instead of in a " +
        "floating card. Model and tool marks are neutral labels, never brand glyphs. Reduced motion starts on the finished run.";
}
