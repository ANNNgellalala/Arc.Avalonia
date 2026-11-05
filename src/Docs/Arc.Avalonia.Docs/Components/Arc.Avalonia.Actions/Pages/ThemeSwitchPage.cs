using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Theme switch's completeness preview: the labelled and icon-only forms pinned to both appearances, so the
/// four faces are visible without pressing anything. Declared state keys match Docs/theme-switch.json.
/// </summary>
public sealed class ThemeSwitchPage : ArcItemPage
{
    public override string ItemSlug => "theme-switch";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-light-labelled", "variant-dark-labelled",
        "variant-light-icon-only", "variant-dark-icon-only"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("带模式词",
            Entry(new ArcThemeSwitcher { Scheme = ArcColorScheme.Light }, "Light"),
            Entry(new ArcThemeSwitcher { Scheme = ArcColorScheme.Dark }, "Dark")));

        panel.Children.Add(Row("纯图标",
            Entry(new ArcThemeSwitcher { Scheme = ArcColorScheme.Light, IconOnly = true }, "Light"),
            Entry(new ArcThemeSwitcher { Scheme = ArcColorScheme.Dark, IconOnly = true }, "Dark")));

        return panel;
    }

    private static Control Entry(ArcThemeSwitcher switcher, string caption)
    {
        var column = new StackPanel { Spacing = 6 };
        column.Children.Add(switcher);
        column.Children.Add(new TextBlock { Text = caption, Classes = { "arc-caption" } });
        return column;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 14, 0);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
