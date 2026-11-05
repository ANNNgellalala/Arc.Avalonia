using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Radio cards' completeness preview: the grid and list layouts, the price meta slot, the icon slot, a
/// disabled card with its reason and the sliding selection ring. Declared keys match Docs/radio-cards.json.
/// </summary>
public sealed class RadioCardsPage : ArcItemPage
{
    private static readonly List<ArcRadioCardOption> Plans = new()
    {
        new("starter", "Starter", "One project and community support", "$0", "zap"),
        new("growth", "Growth", "Unlimited projects, 10 seats", "$24", "sparkles"),
        new("scale", "Scale", "SSO and an audit log", "$96", "users",
            disabled: true, disabledReason: "Available on the enterprise plan")
    };

    private static readonly List<ArcRadioCardOption> Shipping = new()
    {
        new("standard", "Standard", "4 to 6 business days", "Free"),
        new("express", "Express", "2 business days", "$12"),
        new("overnight", "Overnight", "Tomorrow morning", "$29",
            disabled: true, disabledReason: "Not available for this address")
    };

    public override string ItemSlug => "radio-cards";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-grid", "variant-list",
        "variant-meta", "variant-icon", "variant-disabled", "state-selected"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("Grid：图标、价格与禁用卡",
            new ArcRadioCards
            {
                Width = 720,
                Layout = ArcRadioCardsLayout.Grid,
                MinColumnWidth = 200,
                HorizontalAlignment = HorizontalAlignment.Left,
                Value = "growth",
                Options = Plans
            }));

        panel.Children.Add(Row("List：每张卡铺成整行",
            new ArcRadioCards
            {
                Width = 560,
                Layout = ArcRadioCardsLayout.List,
                HorizontalAlignment = HorizontalAlignment.Left,
                Value = "express",
                Options = Shipping
            }));

        return panel;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 12, 12);
            row.Children.Add(child);
        }

        return row;
    }
}
