using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Media.Controls;

namespace Arc.Avalonia.Media.Pages;

/// <summary>
/// Carousel's completeness preview: four slides from the second one, a compact slide size, and the first and
/// last slides so the edge alignment is visible. Declared state keys match Docs/carousel.json so the audit can
/// compare the two. Slides are built from fixed copy and contain no remote media.
/// </summary>
public sealed class CarouselPage : ArcItemPage
{
    public override string ItemSlug => "carousel";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-four-slides", "variant-compact", "state-first-slide", "state-last-slide"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("四张幻灯片", "默认从第二张开始，SlideSize=300", Build(index: 1, slideSize: 300)),
            Card("紧凑尺寸", "SlideSize=200，同屏露出更多相邻卡片", Build(index: 1, slideSize: 200))));

        panel.Children.Add(Row(
            Card("第一张", "DefaultIndex=0，左边缘对齐", Build(index: 0, slideSize: 300)),
            Card("最后一张", "DefaultIndex=3，右边缘对齐", Build(index: 3, slideSize: 300))));

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

    private static Control Card(string title, string note, ArcCarousel carousel)
    {
        carousel.Width = 380;
        return DemoUi.Card(title, note, carousel);
    }

    private static ArcCarousel Build(int index, double slideSize) => new()
    {
        Label = "Harbour suites",
        DefaultIndex = index,
        SlideSize = slideSize,
        Interval = 0,
        Items = new List<ArcCarouselItem>
        {
            new("Sea view suite", "Floor to ceiling glass and a private terrace."),
            new("Garden suite", "Quiet rooms around a planted courtyard."),
            new("Loft suite", "A double height studio under the roof."),
            new("Harbour suite", "Corner windows over the water.")
        }
    };
}
