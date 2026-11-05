using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Effects.Controls;

namespace Arc.Avalonia.Effects.Pages;

/// <summary>
/// Text reveal's completeness preview: an H1 hero, an H2 title, a paragraph, the character unit and a
/// multi-line headline. Declared state keys match Docs/text-reveal.json so the audit can compare the two.
/// The preview shows the settled final state of every sample, which is what the reader sees after the
/// one-shot entrance.
/// </summary>
public sealed class TextRevealPage : ArcItemPage
{
    public override string ItemSlug => "text-reveal";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-h1", "variant-h2", "variant-paragraph", "variant-character", "variant-multiline"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Card("H1 主标题", "两行大标题，按词升起", Hero()));
        panel.Children.Add(Card("H2 区块标题", "默认级别，单行标题", SectionHeading()));
        panel.Children.Add(Card("段落", "正文字号，按词依次出现", Paragraph()));
        panel.Children.Add(Card("按字符", "Unit=Character，逐字升起", Character()));
        panel.Children.Add(Card("多行文案", "用 \\n 指定换行，行间节奏一致", MultiLine()));

        return panel;
    }

    private static Control Card(string title, string note, ArcTextReveal reveal)
    {
        reveal.Width = 760;
        return DemoUi.Card(title, note, reveal);
    }

    private static ArcTextReveal Hero() => new()
    {
        Text = "Ship interfaces\nthat feel precise",
        Level = ArcTextLevel.H1,
        Unit = ArcTextUnit.Word,
        Delay = 0.1,
        Stagger = 40
    };

    private static ArcTextReveal SectionHeading() => new()
    {
        Text = "Quietly confident",
        Level = ArcTextLevel.H2
    };

    private static ArcTextReveal Paragraph() => new()
    {
        Text = "Restrained motion for copy that matters",
        Level = ArcTextLevel.Paragraph,
        Stagger = 28
    };

    private static ArcTextReveal Character() => new()
    {
        Text = "Quietly confident",
        Level = ArcTextLevel.H3,
        Unit = ArcTextUnit.Character,
        Stagger = 18
    };

    private static ArcTextReveal MultiLine() => new()
    {
        Text = "One motion per section\nOne idea per screen",
        Level = ArcTextLevel.H2,
        Stagger = 34
    };
}
