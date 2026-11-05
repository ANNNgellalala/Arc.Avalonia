using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Accordion's completeness preview: a fully closed list, a list with the first row open at the medium size
/// and the same list open at the large size. The open row is set through DefaultOpen, so it is part of the
/// static layout. Declared state keys match Docs/accordion.json so the audit can compare the two lists.
/// </summary>
public sealed class AccordionPage : ArcItemPage
{
    public override string ItemSlug => "accordion";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-md", "size-lg", "state-open", "state-closed"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 20, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Captioned(Items(-1, ArcAccordionSize.Md), "全部收起：只留标题行与发丝分隔线，页面高度不变。"));

        panel.Children.Add(Captioned(Items(0, ArcAccordionSize.Md), "打开第一行：高度与不透明度一起生长，箭头旋转 180°。"));

        panel.Children.Add(Captioned(Items(0, ArcAccordionSize.Lg), "Large：标题行 68px，标题字号与字重更大。"));

        return panel;
    }

    private static ArcAccordion Items(int defaultOpen, ArcAccordionSize size) => new()
    {
        Label = "Billing questions",
        Width = 520,
        HorizontalAlignment = HorizontalAlignment.Left,
        DefaultOpen = defaultOpen,
        Size = size,
        Items = new List<ArcAccordionItem>
        {
            new("How is usage counted?",
                "Every request counts once, including retries. Cached responses are free, and the dashboard totals them separately so a spike is easy to explain."),
            new("Can I change plan mid-cycle?",
                "Yes. The difference is prorated to the day, and the next invoice shows both lines."),
            new("Where do invoices go?",
                "Billing contacts receive the invoice and the receipt, and owners can download both.")
        }
    };

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
