using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Effects.Controls;

namespace Arc.Avalonia.Effects.Pages;

/// <summary>
/// In-view title's completeness preview: one sample per reveal style (blur, word, line, tracking, wipe).
/// Declared state keys match Docs/in-view-title.json so the audit can compare the two. Each sample is shown in
/// its settled final state after the one-shot entrance.
/// </summary>
public sealed class InViewTitlePage : ArcItemPage
{
    public override string ItemSlug => "in-view-title";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-blur", "variant-word", "variant-line", "variant-tracking", "variant-wipe"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Card("模糊入场", "从模糊中变清晰，默认样式", Heading(ArcInViewVariant.Blur, "Everything your team needs")));
        panel.Children.Add(Card("按词入场", "每个词依次落下", Heading(ArcInViewVariant.Word, "Everything your team needs to ship")));
        panel.Children.Add(Card("按行入场", "用 Lines 指定换行，逐行出现", Line()));
        panel.Children.Add(Card("字距入场", "字距从松到紧收拢", Heading(ArcInViewVariant.Tracking, "Quiet confidence")));
        panel.Children.Add(Card("擦出入场", "从左向右揭开", Heading(ArcInViewVariant.Wipe, "One motion per section")));

        return panel;
    }

    private static Control Card(string title, string note, ArcInViewTitle heading)
    {
        heading.Width = 760;
        return DemoUi.Card(title, note, heading);
    }

    private static ArcInViewTitle Heading(ArcInViewVariant variant, string text) => new()
    {
        Text = text,
        Variant = variant,
        Level = ArcTextLevel.H3
    };

    private static ArcInViewTitle Line() => new()
    {
        Text = "Everything your team needs to ship",
        Variant = ArcInViewVariant.Line,
        Lines = new[] { "Everything your team", "needs to ship" },
        Level = ArcTextLevel.H3
    };
}
