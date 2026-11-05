using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Effects.Controls;

namespace Arc.Avalonia.Effects.Pages;

/// <summary>
/// Text morph's completeness preview: the three labels of one publish flow, plus the heading and paragraph
/// levels. Declared state keys match Docs/text-morph.json so the audit can compare the two. Each sample shows
/// the settled state of a morph, so the text is readable without interaction.
/// </summary>
public sealed class TextMorphPage : ArcItemPage
{
    public override string ItemSlug => "text-morph";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-idle", "variant-busy", "variant-done", "variant-h1", "variant-paragraph"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("待发布", "Text=Publish，默认 Span 之外的短标签", Morph("Publish", ArcTextLevel.Span)),
            Card("发布中", "Text=Publishing，宽度随标签变长", Morph("Publishing", ArcTextLevel.Span))));

        panel.Children.Add(Row(
            Card("已发布", "Text=Published，状态收束", Morph("Published", ArcTextLevel.Span)),
            Card("大号标题", "Level=H1，用于页面级状态文案", Morph("Publish", ArcTextLevel.H1))));

        panel.Children.Add(Row(
            Card("段落级别", "Level=Paragraph，用于正文中的状态词", Morph("Published", ArcTextLevel.Paragraph))));

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

    private static Control Card(string title, string note, ArcTextMorph morph)
    {
        morph.Width = 340;
        return DemoUi.Card(title, note, morph);
    }

    private static ArcTextMorph Morph(string text, ArcTextLevel level) => new()
    {
        Text = text,
        Level = level
    };
}
