using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>
/// Card's completeness preview: the default surface, the muted variant and the compact padding, all with the
/// same content so the difference is only the surface. Declared keys match Docs/card.json.
/// </summary>
public sealed class CardPage : ArcItemPage
{
    public override string ItemSlug => "card";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-default", "variant-muted", "variant-compact"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(new TextBlock { Text = "表面与内边距", Classes = { "arc-caption" } });

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            VerticalAlignment = VerticalAlignment.Top
        };
        row.Children.Add(Sample("Default", Content(), muted: false, compact: false));
        row.Children.Add(Sample("Muted", Content(), muted: true, compact: false));
        row.Children.Add(Sample("Compact", Content(), muted: false, compact: true));
        panel.Children.Add(row);

        return panel;
    }

    private static Control Content() => new StackPanel
    {
        Spacing = 6,
        Children =
        {
            new TextBlock { Text = "Invite your team", Classes = { "arc-label" } },
            new TextBlock
            {
                Text = "Bring the people who will review and ship with you.",
                Classes = { "arc-body-sm", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap
            }
        }
    };

    private static ArcCard Sample(string label, Control content, bool muted, bool compact) => new()
    {
        Width = 248,
        Muted = muted,
        Padding = compact ? new Thickness(16) : new Thickness(24),
        Content = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = label, Classes = { "arc-caption" } },
                content
            }
        }
    };
}
