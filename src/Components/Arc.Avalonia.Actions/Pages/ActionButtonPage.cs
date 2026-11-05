using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Action button's completeness preview: the three variants, all three sizes, and the pending, success and
/// disabled faces shown side by side so a screenshot proves the whole state machine. Declared state keys
/// match Docs/action-button.json.
/// </summary>
public sealed class ActionButtonPage : ArcItemPage
{
    public override string ItemSlug => "action-button";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-primary", "variant-secondary", "variant-danger",
        "size-sm", "size-md", "size-lg",
        "state-pending", "state-success", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("变体",
            Face("Publish", ArcButtonVariant.Primary, ArcButtonSize.Md),
            Face("Save draft", ArcButtonVariant.Secondary, ArcButtonSize.Md),
            Face("Delete draft", ArcButtonVariant.Danger, ArcButtonSize.Md)));

        panel.Children.Add(Row("尺寸",
            Face("Small", ArcButtonVariant.Secondary, ArcButtonSize.Sm),
            Face("Medium", ArcButtonVariant.Secondary, ArcButtonSize.Md),
            Face("Large", ArcButtonVariant.Secondary, ArcButtonSize.Lg)));

        panel.Children.Add(Row("状态",
            new ArcActionButton
            {
                Label = "Publish",
                PendingLabel = "Publishing",
                Variant = ArcButtonVariant.Primary,
                State = ArcActionButtonState.Pending
            },
            new ArcActionButton
            {
                Label = "Publish",
                SuccessLabel = "Published",
                Variant = ArcButtonVariant.Primary,
                State = ArcActionButtonState.Success,
                ResetAfterMs = 0
            },
            new ArcActionButton
            {
                Label = "Publish",
                Variant = ArcButtonVariant.Primary,
                IsEnabled = false
            }));

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

    private static ArcActionButton Face(string label, ArcButtonVariant variant, ArcButtonSize size) => new()
    {
        Label = label,
        Variant = variant,
        Size = size
    };
}
