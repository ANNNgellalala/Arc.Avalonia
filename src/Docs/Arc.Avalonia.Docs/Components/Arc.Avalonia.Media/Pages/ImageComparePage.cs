using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Media.Controls;

namespace Arc.Avalonia.Media.Pages;

/// <summary>
/// Image compare's completeness preview: a horizontal split at 35%, a vertical split at 62%, both edge
/// positions and the form without caption chips. Declared state keys match Docs/image-compare.json so the
/// audit can compare the two. No source paths are supplied, so both sides draw the deterministic placeholder
/// mesh instead of loading a file.
/// </summary>
public sealed class ImageComparePage : ArcItemPage
{
    public override string ItemSlug => "image-compare";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-horizontal", "variant-vertical", "state-left-edge", "state-right-edge", "state-no-labels"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("水平对比", "左右分割，Position=35，两侧带角标", Horizontal(35)),
            Card("垂直对比", "上下分割，DefaultPosition=62", Vertical())));

        panel.Children.Add(Row(
            Card("最左", "Position=0，只露出 Before 一侧", Horizontal(0)),
            Card("最右", "Position=100，只露出 After 一侧", Horizontal(100))));

        var noLabels = Horizontal(50);
        noLabels.ShowLabels = false;
        noLabels.BeforeLabel = null;
        noLabels.AfterLabel = null;

        panel.Children.Add(Row(Card("无角标", "ShowLabels=false，只留分隔线", noLabels)));

        return panel;
    }

    private static Control Row(params Control[] cards)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var card in cards)
        {
            card.Margin = new Thickness(0, 0, 16, 0);
            row.Children.Add(card);
        }

        return row;
    }

    private static Control Card(string title, string note, ArcImageCompare compare)
    {
        compare.Width = 340;
        return DemoUi.Card(title, note, compare);
    }

    private static ArcImageCompare Horizontal(double position) => new()
    {
        Position = position,
        BeforeLabel = "Original",
        AfterLabel = "Retouched",
        Label = "Portrait edit",
        AspectRatio = 1.5
    };

    private static ArcImageCompare Vertical() => new()
    {
        DefaultPosition = 62,
        Orientation = ArcImageCompareOrientation.Vertical,
        BeforeLabel = "Before",
        AfterLabel = "After",
        Label = "Poster edit",
        AspectRatio = 1.2
    };
}
