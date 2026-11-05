using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Rating's completeness preview: the three unit sizes, the empty / rated / half-star values, the read-only
/// form and custom value labels. Declared state keys match Docs/rating.json.
/// </summary>
public sealed class RatingPage : ArcItemPage
{
    private static readonly string[] ChineseLabels = { "很差", "较差", "一般", "不错", "很好" };

    public override string ItemSlug => "rating";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-empty", "state-set", "state-half",
        "variant-readonly", "variant-value-labels",
        "size-sm", "size-md", "size-lg"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("尺寸",
            Column(new ArcRating { Label = "Small", Value = 3, Size = ArcFieldSize.Sm }),
            Column(new ArcRating { Label = "Medium", Value = 3, Size = ArcFieldSize.Md }),
            Column(new ArcRating { Label = "Large", Value = 3, Size = ArcFieldSize.Lg })));

        panel.Children.Add(Row("状态",
            Column(new ArcRating
            {
                Label = "未评分",
                Value = 0,
                Description = "右侧显示 EmptyLabel。"
            }),
            Column(new ArcRating
            {
                Label = "整星",
                Value = 4,
                Description = "Great。"
            }),
            Column(new ArcRating
            {
                Label = "半星",
                AllowHalf = true,
                Value = 4.5,
                Description = "AllowHalf 打开后可评半星。"
            })));

        panel.Children.Add(Row("只读与自定义文案",
            Column(new ArcRating
            {
                Label = "只读平均分",
                ReadOnly = true,
                AllowHalf = true,
                ShowValue = true,
                Value = 4.5
            }),
            Column(new ArcRating
            {
                Label = "自定义词",
                Value = 4,
                ValueLabels = ChineseLabels
            }),
            Column(new ArcRating
            {
                Label = "自定义空态",
                Value = 0,
                EmptyLabel = "尚未评分"
            })));

        return panel;
    }

    private static Control Column(ArcRating rating)
    {
        rating.Width = 300;
        return rating;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 24, 12);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
