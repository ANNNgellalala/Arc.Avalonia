using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>
/// Skeleton's completeness preview: text lines, an avatar plus lines, a rectangle, a circle and the loaded
/// form where real content replaces the placeholder. Declared keys match Docs/skeleton.json.
/// </summary>
public sealed class SkeletonPage : ArcItemPage
{
    public override string ItemSlug => "skeleton";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-text", "variant-avatar", "shape-rect", "shape-circle", "state-loaded"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("文本行与头像",
            DemoUi.FixedColumn(280,
                new ArcSkeleton { Lines = 3, Label = "Loading article" }),
            DemoUi.FixedColumn(280,
                new ArcSkeleton { Avatar = true, Lines = 3, Label = "Loading profile" }),
            DemoUi.FixedColumn(280,
                new ArcSkeleton
                {
                    Loading = false,
                    Content = new TextBlock
                    {
                        Text = "Content has loaded into the reserved space.",
                        Classes = { "arc-body-sm", "arc-secondary" },
                        TextWrapping = TextWrapping.Wrap
                    }
                })));

        panel.Children.Add(Row("矩形与圆形",
            DemoUi.FixedColumn(280,
                new ArcSkeleton { Shape = ArcSkeletonShape.Rect, Height = 88 }),
            DemoUi.FixedColumn(280,
                new ArcSkeleton { Shape = ArcSkeletonShape.Circle, Height = 56 })));

        return panel;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 16, 12);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
