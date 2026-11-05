using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Media.Controls;

namespace Arc.Avalonia.Media.Pages;

/// <summary>
/// Card stack's completeness preview: the default city deck, a compact stage, custom decision copy and a
/// two-card deck. Declared state keys match Docs/card-stack.json so the audit can compare the two.
/// </summary>
public sealed class CardStackPage : ArcItemPage
{
    public override string ItemSlug => "card-stack";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-default", "variant-compact", "variant-custom-labels", "variant-two-cards"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("默认牌堆", "四张城市卡片，Skip / Shortlist", Deck(cardWidth: 340)),
            Card("紧凑牌堆", "CardWidth=300，适合窄栏", Deck(cardWidth: 300))));

        panel.Children.Add(Row(
            Card("自定义文案", "Pass / Save 与 decided / kept 过去式", CustomLabels()),
            Card("两张卡", "只评审两个候选时牌堆更轻", TwoCards())));

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

    private static Control Card(string title, string note, ArcCardStack stack)
    {
        stack.Width = 380;
        return DemoUi.Card(title, note, stack);
    }

    private static ArcCardStack Deck(double cardWidth) => new()
    {
        Label = "City shortlist",
        CardWidth = cardWidth,
        LeftLabel = "Skip",
        RightLabel = "Shortlist",
        LeftOutcome = "skipped",
        RightOutcome = "shortlisted",
        Cards = new List<ArcCardStackItem>
        {
            new("lis", "Lisbon", "Lisbon", "Tiled facades, steep streets, and the light coming off the river."),
            new("osl", "Oslo", "Oslo", "Fjord swimming in summer, quiet design in winter."),
            new("kyo", "Kyoto", "Kyoto", "Temple gardens and a very particular sense of season."),
            new("cph", "Copenhagen", "Copenhagen", "Harbour baths, bicycles, and long summer evenings.")
        }
    };

    private static ArcCardStack CustomLabels() => new()
    {
        Label = "Draft review",
        CardWidth = 340,
        LeftLabel = "Pass",
        RightLabel = "Save",
        LeftOutcome = "decided",
        RightOutcome = "kept",
        Cards = new List<ArcCardStackItem>
        {
            new("intro", "Intro", "Opening draft", "A shorter opening that gets to the point."),
            new("pricing", "Pricing", "Pricing page", "Two plans, one comparison table."),
            new("faq", "FAQ", "FAQ block", "Six questions pulled from support.")
        }
    };

    private static ArcCardStack TwoCards() => new()
    {
        Label = "Pick a layout",
        CardWidth = 340,
        LeftLabel = "Skip",
        RightLabel = "Keep",
        Cards = new List<ArcCardStackItem>
        {
            new("a", "Layout A", "Layout A", "Two columns with a sticky sidebar."),
            new("b", "Layout B", "Layout B", "One wide column with inline cards.")
        }
    };
}
