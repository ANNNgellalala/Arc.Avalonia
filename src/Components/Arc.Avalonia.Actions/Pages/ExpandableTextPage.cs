using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Expandable text's completeness preview: the clamped state with its bottom fade, the expanded state, a
/// short text that has nothing to toggle and a two line clamp. Declared state keys match
/// Docs/expandable-text.json so the audit can compare the two lists.
/// </summary>
public sealed class ExpandableTextPage : ArcItemPage
{
    public override string ItemSlug => "expandable-text";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-collapsed", "state-expanded", "state-fitted", "variant-two-lines"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 20, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Captioned(
            new ArcExpandableText
            {
                Lines = 3,
                Width = 560,
                Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
                Text = Review
            },
            "收起态：三行之后底部渐隐，开关读作 Read more。"));

        panel.Children.Add(Captioned(
            new ArcExpandableText
            {
                Lines = 3,
                Width = 560,
                IsExpanded = true,
                Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
                Text = Review
            },
            "展开态：完整高度，渐隐消失，开关读作 Show less。"));

        panel.Children.Add(Captioned(
            new ArcExpandableText
            {
                Lines = 3,
                Width = 560,
                Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
                Text = "Maya merged the checkout branch and requested a review on the pricing page."
            },
            "短文本：没有溢出，因此既不加渐隐也不显示开关。"));

        panel.Children.Add(Captioned(
            new ArcExpandableText
            {
                Lines = 2,
                Width = 560,
                Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
                Text = Review
            },
            "两行：窄卡片里用 Lines=2 收紧裁剪，渐隐仍贴着文本底部。"));

        return panel;
    }

    private const string Review =
        "The release moves the checkout flow onto the new pricing service. Orders placed before the switch " +
        "keep their original totals, and the migration job reconciles anything that lands in flight. Support " +
        "has the runbook, and the rollback path is a single flag. Expect two short windows on Thursday and " +
        "Friday mornings, each under five minutes, with no visible change for customers beyond a faster " +
        "confirmation screen. After the second window the old service is read only for a week, so any " +
        "inconsistency can still be compared against the previous totals.";

    private static Control Captioned(Control content, string caption)
    {
        var panel = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(content);
        panel.Children.Add(new TextBlock
        {
            Text = caption,
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 620,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        return panel;
    }
}
