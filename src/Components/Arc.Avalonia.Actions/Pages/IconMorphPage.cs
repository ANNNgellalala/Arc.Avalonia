using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Icon morph's completeness preview: every icon pair shown in both states side by side, plus the chevron
/// that rests down or right and turns when State flips. Declared state keys match Docs/icon-morph.json.
/// </summary>
public sealed class IconMorphPage : ArcItemPage
{
    public override string ItemSlug => "icon-morph";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-first", "state-second",
        "pair-menu", "pair-play", "pair-plus", "pair-copy", "pair-theme", "pair-chevron",
        "chevron-pointing-right"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("图标对",
            Pair("Menu", ArcIconMorphName.Menu, "menu", "close"),
            Pair("Play", ArcIconMorphName.Play, "play", "pause"),
            Pair("Plus", ArcIconMorphName.Plus, "plus", "check"),
            Pair("Copy", ArcIconMorphName.Copy, "copy", "check"),
            Pair("Theme", ArcIconMorphName.Theme, "sun", "moon")));

        panel.Children.Add(Row("Chevron",
            Chevron("朝下", ArcChevronPointing.Down, 180, "向下", "转 180° 向上"),
            Chevron("朝右", ArcChevronPointing.Right, 90, "向右", "转 90° 向下")));

        return panel;
    }

    private static Control Pair(string caption, ArcIconMorphName name, string first, string second)
    {
        var states = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                Tile(name, false),
                Tile(name, true)
            }
        };

        return Column($"{caption}：{first} → {second}", states);
    }

    private static Control Chevron(string caption, ArcChevronPointing pointing, double turn, string first, string second)
    {
        var states = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                Tile(ArcIconMorphName.Chevron, false, pointing, turn),
                Tile(ArcIconMorphName.Chevron, true, pointing, turn)
            }
        };

        return Column($"{caption}：{first} → {second}", states);
    }

    private static Control Column(string caption, Control states)
    {
        var column = new StackPanel { Spacing = 6 };
        column.Children.Add(states);
        column.Children.Add(new TextBlock { Text = caption, Classes = { "arc-caption" } });
        return column;
    }

    private static Control Tile(
        ArcIconMorphName name,
        bool state,
        ArcChevronPointing pointing = ArcChevronPointing.Down,
        double turn = 180)
    {
        var morph = new ArcIconMorph
        {
            Icon = name,
            State = state,
            Pointing = pointing,
            Turn = turn,
            Size = 20,
            Foreground = DemoUi.Brush(ArcTokens.Foreground),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        return new Border
        {
            Width = 52,
            Height = 52,
            CornerRadius = new CornerRadius(16),
            Background = DemoUi.Brush(ArcTokens.Surface),
            BorderBrush = DemoUi.Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1),
            Child = morph
        };
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 18, 0);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
