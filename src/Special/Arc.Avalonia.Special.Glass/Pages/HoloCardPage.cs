using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Glass.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Glass.Pages;

/// <summary>The Holo card catalog page: a member card with foil, glitter and a flip to its back.</summary>
public sealed class HoloCardPage : ArcItemPage
{
    public override string ItemSlug => "holo-card";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A membership or subscription card on an account or welcome page.",
        "A shareable profile, badge, or achievement that people will screenshot."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Tab focuses the card when it has a back; Enter or Space flips it.",
        "The card is named by its label and reports whether the front or the back is showing.",
        "The hidden face is not in the tree, so assistive technology reads only the side that is visible.",
        "Foil, glitter and glare are decorative and never announced; the two pixel focus outline marks keyboard focus."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Tilt follows the pointer on a lightly underdamped spring while the light trails on a softer one, so the sheen lags the lean.",
        "The foil slides and shifts hue with the angle; glitter drifts against the tilt and sparkles near the glare.",
        "A flip turns the card half a turn on a spring with a little bounce and the faces swap at halfway; reduced motion cross fades the faces instead."
    };

    public override string? ImplementationNotes =>
        "Tilt is an Avalonia Rotate3DTransform with a fixed perspective depth rather than a CSS matrix3d, and the faces swap at the halfway point of the rotation. The foil is a conic gradient plus a deterministic glitter field and a glare, because Avalonia has no blend modes. Device tilt is not available on desktop, so leaning the card with the pointer stands in for the phone gyroscope. No Pro source was used.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var front = new ArcHoloCard
        {
            MemberName = "Jasmine Brooks",
            Tier = "Founding member",
            MemberSince = "Member since 2024",
            Number = "2481 0917",
            MaxTilt = 14,
            Intensity = 1
        };
        front.HorizontalAlignment = HorizontalAlignment.Center;
        front.VerticalAlignment = VerticalAlignment.Center;

        var back = new ArcHoloCard
        {
            MemberName = "Jasmine Brooks",
            Tier = "Founding member",
            MemberSince = "Member since 2024",
            Number = "2481 0917",
            IsFlipped = true,
            MaxTilt = 14,
            Intensity = 0.9
        };
        back.HorizontalAlignment = HorizontalAlignment.Center;
        back.VerticalAlignment = VerticalAlignment.Center;

        var labels = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Width = 600,
            ColumnSpacing = 48,
            Children = { GlassDemo.Caption("Front"), GlassDemo.Caption("Back") }
        };
        Grid.SetColumn(labels.Children[1], 1);

        var cards = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Width = 600,
            ColumnSpacing = 48,
            Children = { front, back }
        };
        Grid.SetColumn(back, 1);

        var body = new StackPanel
        {
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { labels, cards }
        };

        var stage = GlassDemo.Stage(body, ArcGlassScene.Dusk, 430, 24, seed: 7);
        return new StackPanel
        {
            Children =
            {
                stage,
                GlassDemo.Hint("Lean the pointer over a card to move the foil and glare; click, Enter or Space turns it over, and the second card starts on its back.")
            }
        };
    }
}

