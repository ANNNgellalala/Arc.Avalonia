using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>
/// Timeline's completeness preview: neutral, success and danger events, an icon node, an avatar node and one
/// event whose detail is expanded by default. Declared keys match Docs/timeline.json.
/// </summary>
public sealed class TimelinePage : ArcItemPage
{
    private const string Sara = "avares://Arc.Avalonia.Docs/Assets/portrait-sara.png";

    public override string ItemSlug => "timeline";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "tone-neutral", "tone-success", "tone-danger",
        "variant-icon", "variant-avatar", "state-detail"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var now = DateTimeOffset.Now;
        var events = new List<ArcTimelineEvent>
        {
            new()
            {
                Id = "e1",
                At = now.AddMinutes(-12),
                Actor = "Maya Chen",
                Title = "merged Checkout redesign into main",
                Meta = "PR #482",
                Tone = ArcTimelineTone.Success
            },
            new()
            {
                Id = "e2",
                At = now.AddMinutes(-41),
                Title = "Deploy failed",
                Meta = "Build step exited with code 1",
                Tone = ArcTimelineTone.Danger,
                Icon = "triangle-alert",
                Detail = "error: Type 'OrderSummary' is missing the following properties:\n  total, currency"
            },
            new()
            {
                Id = "e3",
                At = now.AddHours(-3),
                Actor = "Leo Park",
                Title = "opened an issue about slow exports",
                Meta = "EXPORT-118"
            },
            new()
            {
                Id = "e4",
                At = now.AddDays(-1).AddHours(-2),
                Actor = "Sara Ruiz",
                Avatar = Sara,
                Title = "commented on Billing settings",
                Meta = "4 replies"
            }
        };

        var panel = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new TextBlock { Text = "今日与昨日 · 节点类型 · 展开详情", Classes = { "arc-caption" } });
        panel.Children.Add(new ArcTimeline
        {
            Width = 700,
            Events = events,
            Now = now,
            MaxHeight = 430,
            Label = "Project activity",
            DefaultExpanded = new[] { "e2" }
        });

        return panel;
    }
}
