using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Effects.Controls;

namespace Arc.Avalonia.Effects.Pages;

/// <summary>
/// Text shimmer's completeness preview: an active status line, the settled text after work finishes, and the
/// same effect at the heading and paragraph levels. Declared state keys match Docs/text-shimmer.json so the
/// audit can compare the two. Active samples show the sweeping gradient at rest in the screenshot; settled
/// samples show the plain foreground colour.
/// </summary>
public sealed class TextShimmerPage : ArcItemPage
{
    public override string ItemSlug => "text-shimmer";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-active", "variant-settled", "variant-h3", "variant-paragraph"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Card("进行中", "Active=true，光带循环扫过文字", Shimmer("Generating summary", active: true, ArcTextLevel.Paragraph)));
        panel.Children.Add(Card("已完成", "Active=false，文字恢复纯色", Shimmer("Summary ready", active: false, ArcTextLevel.Paragraph)));
        panel.Children.Add(Card("标题级别", "Level=H3，放在区块标题位置", Shimmer("Indexing 1,248 files", active: true, ArcTextLevel.H3)));
        panel.Children.Add(Card("段落级别", "Level=Paragraph，放在正文状态行", Shimmer("Building search index", active: true, ArcTextLevel.Paragraph)));

        return panel;
    }

    private static Control Card(string title, string note, ArcTextShimmer shimmer)
    {
        shimmer.Width = 760;
        return DemoUi.Card(title, note, shimmer);
    }

    private static ArcTextShimmer Shimmer(string text, bool active, ArcTextLevel level) => new()
    {
        Text = text,
        Active = active,
        Duration = 1.8,
        Level = level
    };
}
