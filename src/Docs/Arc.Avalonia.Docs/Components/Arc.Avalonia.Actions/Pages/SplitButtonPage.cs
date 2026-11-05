using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Split button's completeness preview: both variants with and without a leading icon, the resting closed
/// face and the alternatives menu held open inside a reserved box. Declared state keys match
/// Docs/split-button.json.
/// </summary>
public sealed class SplitButtonPage : ArcItemPage
{
    public override string ItemSlug => "split-button";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-primary", "variant-secondary", "variant-icon", "state-closed", "state-open"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        var publish = new ArcSplitButton
        {
            Label = "Publish",
            Icon = "upload",
            Variant = ArcSplitButtonVariant.Primary,
            Actions = new[]
            {
                new ArcMenuItem { Label = "Publish and notify", Icon = "bell" },
                new ArcMenuItem { Label = "Publish as draft", Icon = "file" },
                new ArcMenuItem { Label = "Discard changes", Icon = "trash-2", IsDestructive = true, SeparatorBefore = true }
            }
        };

        var export = new ArcSplitButton
        {
            Label = "Export",
            Icon = "download",
            Variant = ArcSplitButtonVariant.Secondary,
            Actions = new[]
            {
                new ArcMenuItem { Label = "Export as CSV", Shortcut = "Ctrl+E" },
                new ArcMenuItem { Label = "Export as JSON" },
                new ArcMenuItem { Label = "Export as PDF" }
            }
        };

        var open = new ArcSplitButton
        {
            Label = "Share",
            Icon = "link",
            Variant = ArcSplitButtonVariant.Primary,
            IsOpen = true,
            Actions = new[]
            {
                new ArcMenuItem { Label = "Copy link", Icon = "link", Shortcut = "Ctrl+L" },
                new ArcMenuItem { Label = "Invite by email", Icon = "mail" },
                new ArcMenuItem { Label = "Manage access", Icon = "users", SeparatorBefore = true }
            }
        };

        panel.Children.Add(Row("变体与前置图标", publish, export));
        panel.Children.Add(Row("展开态", Reserved(240, 230, open)));

        return panel;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 10, 0);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }

    private static Control Reserved(double width, double height, Control content) => new Panel
    {
        Width = width,
        Height = height,
        HorizontalAlignment = HorizontalAlignment.Left,
        Children = { content }
    };
}
