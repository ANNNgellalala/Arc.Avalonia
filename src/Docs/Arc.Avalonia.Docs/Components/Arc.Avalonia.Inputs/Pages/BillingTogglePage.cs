using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Billing toggle's completeness preview: the monthly and yearly selections, the savings badge on the
/// yearly option and both sizes. Declared state keys match Docs/billing-toggle.json.
/// </summary>
public sealed class BillingTogglePage : ArcItemPage
{
    private static readonly List<ArcBillingToggleOption> Periods = new()
    {
        new("monthly", "Monthly"),
        new("yearly", "Yearly", "Save 20%", "You save $48")
    };

    public override string ItemSlug => "billing-toggle";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-monthly", "state-yearly", "variant-badge", "size-md", "size-lg"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("周期",
            Column("年付选中，徽标改写",
                new ArcBillingToggle { Options = Periods, Value = "yearly", Width = 300 },
                Note("$16 per month, billed yearly · 划掉 $20")),
            Column("月付选中，年付项保留 Save 20%",
                new ArcBillingToggle { Options = Periods, Value = "monthly", Width = 300 },
                Note("$20 per month"))));

        panel.Children.Add(Row("尺寸",
            Column("Medium：每项 36 px",
                new ArcBillingToggle { Options = Periods, Value = "monthly", Size = ArcBillingToggleSize.Md }),
            Column("Large：每项 44 px",
                new ArcBillingToggle { Options = Periods, Value = "yearly", Size = ArcBillingToggleSize.Lg })));

        return panel;
    }

    private static Control Column(string caption, params Control[] children)
    {
        var stack = new StackPanel { Spacing = 8, Width = 300 };
        stack.Children.Add(new TextBlock { Text = caption, Classes = { "arc-caption" }, TextWrapping = TextWrapping.Wrap });
        foreach (var child in children)
        {
            stack.Children.Add(child);
        }

        return stack;
    }

    private static TextBlock Note(string text) => new()
    {
        Text = text,
        Classes = { "arc-body-sm", "arc-secondary" },
        TextWrapping = TextWrapping.Wrap
    };

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 20, 12);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
