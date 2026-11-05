using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data.Pages;

/// <summary>
/// Layout morph: a 24 card gallery that flies between grid, masonry, list, fan, carousel and helix. Cards are
/// sample metrics; the layout switch is the interaction, with a hover and a keyboard cursor. An independent
/// Avalonia implementation of Arc's public layout-morph block; no Pro source was used.
/// </summary>
public sealed class LayoutMorphPage : ArcItemPage
{
    public override string ItemSlug => "layout-morph";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "layout-grid", "layout-masonry", "layout-list", "layout-fan", "layout-carousel", "layout-helix"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A gallery or dashboard where the same set of cards needs several arrangements.",
        "Showcase pages that demonstrate layout transitions between grid, list and fanned views."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a data grid when cards carry tabular values people compare.",
        "Use a plain grid when the arrangement never changes; a morph adds motion for no gain."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The layout switch is a segmented control; arrows inside the gallery move a keyboard cursor, Home and End jump to the first or last card.",
        "Card positions and the active layout are announced politely, and every card keeps its label when the arrangement changes."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Cards interpolate between the old and new rectangles on a short ease; reduced motion paints the target layout at once.",
        "Hover raises a card's border and selection adds the accent outline, both with short fades."
    };

    public override string? ImplementationNotes =>
        "Cards are interpolated between two rect lists, not measured and animated by a layout engine, so a card never reflows its own content mid flight. The fan and helix are flattened: cards shift and scale but do not rotate in 3D, because this is a 2D drawing surface with no perspective camera.";

    private sealed record LayoutOption(string Key, string Label);

    private static readonly LayoutOption[] Layouts =
    {
        new("grid", "Grid"),
        new("masonry", "Masonry"),
        new("list", "List"),
        new("fan", "Fan"),
        new("carousel", "Carousel"),
        new("helix", "Helix")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var stageHost = new ContentControl();
        var status = DataKit.Status();
        var layout = "grid";
        var morph = new ArcLayoutMorph();
        stageHost.Content = morph;

        var selector = DataKit.Segmented(Layouts.Select(option => (option.Key, option.Label)), layout, key =>
        {
            layout = key;
            morph.Layout = key;
            DataKit.Say(status, $"Arranged the 24 cards as {Layouts.First(option => option.Key == key).Label.ToLowerInvariant()}.");
        });

        var header = DataKit.Header("24 live cards",
            "The same cards in six arrangements · the switch flies them between layouts",
            DataKit.Row(8, selector));

        var card = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                DataKit.Row(8, DataKit.Label("Gallery"), DataKit.Caption("· hover a card, arrow keys move the cursor")),
                morph,
                DataKit.Caption("Grid · masonry · list · fan · carousel · helix. Fan and helix are flattened to offsets and scale, not 3D rotation.")
            }
        });

        var notes = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*"),
            Children =
            {
                DataKit.Stat("24 cards", "6 layouts", "one shared card, six arrangements"),
                DataKit.AtColumn(DataKit.Stat("320 ms", "Morph", "one ease, no overshoot"), 2),
                DataKit.AtColumn(DataKit.Stat("Reduced", "Instant", "the target layout paints at once"), 4)
            }
        };

        // The other five arrangements pinned in small stages, so every declared layout is visible statically.
        var previews = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var option in Layouts)
        {
            if (option.Key == "grid")
            {
                continue;
            }

            previews.Children.Add(new StackPanel
            {
                Spacing = 4,
                Margin = new Thickness(0, 0, 10, 10),
                Children =
                {
                    new ArcLayoutMorph { Layout = option.Key, Width = 182, Height = 132 },
                    DataKit.Caption(option.Label)
                }
            });
        }

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { header, card, previews, notes, status }
        }, 16);
    }
}
