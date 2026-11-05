using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>
/// Badge's completeness preview: all five tones and the dot form, so every declared key is visible at once.
/// Declared keys match Docs/badge.json.
/// </summary>
public sealed class BadgePage : ArcItemPage
{
    public override string ItemSlug => "badge";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "tone-neutral", "tone-accent", "tone-success", "tone-warning", "tone-danger", "variant-dot"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("色调",
            Badge("Draft", ArcBadgeTone.Neutral),
            Badge("Pro", ArcBadgeTone.Accent),
            Badge("Published", ArcBadgeTone.Success),
            Badge("Due soon", ArcBadgeTone.Warning),
            Badge("Failed", ArcBadgeTone.Danger)));

        panel.Children.Add(Row("圆点",
            Badge("Online", ArcBadgeTone.Success, dot: true),
            Badge("Syncing", ArcBadgeTone.Accent, dot: true),
            Badge("Overdue", ArcBadgeTone.Danger, dot: true),
            Badge("+2", ArcBadgeTone.Neutral, dot: true)));

        return panel;
    }

    private static ArcBadge Badge(string content, ArcBadgeTone tone, bool dot = false) => new()
    {
        Content = content,
        Tone = tone,
        Dot = dot
    };

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 10, 10);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
