using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>
/// Countdown's completeness preview: the large digits, the compact inline form and the completed state where
/// CompleteLabel and the live dot replace the numbers. Declared keys match Docs/countdown.json.
/// </summary>
public sealed class CountdownPage : ArcItemPage
{
    public override string ItemSlug => "countdown";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-large", "variant-compact", "state-complete"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var now = DateTimeOffset.Now;
        var panel = new StackPanel { Spacing = 24, HorizontalAlignment = HorizontalAlignment.Left, Width = 560 };

        panel.Children.Add(Labelled("Large",
            new ArcCountdown
            {
                Target = now.AddDays(2).AddHours(4).AddMinutes(12).AddSeconds(30),
                Label = "Keynote starts in",
                CompleteLabel = "Live now"
            }));

        panel.Children.Add(Labelled("Compact",
            new ArcCountdown
            {
                Target = now.AddHours(1).AddMinutes(8).AddSeconds(20),
                Variant = ArcCountdownVariant.Compact,
                Units = new[] { ArcCountdownUnit.Hours, ArcCountdownUnit.Minutes, ArcCountdownUnit.Seconds },
                Label = "Sale ends in",
                CompleteLabel = "Sale ended"
            }));

        panel.Children.Add(Labelled("Complete",
            new ArcCountdown
            {
                Target = now.AddMinutes(-4),
                Label = "Stream status",
                CompleteLabel = "Live now"
            }));

        return panel;
    }

    private static Control Labelled(string label, Control content)
    {
        var column = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Left };
        column.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        column.Children.Add(content);
        return column;
    }
}
