using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialAIWidgets.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialAIWidgets.Pages;

/// <summary>
/// Catalog page for the morph loader: the four loading shapes side by side, the check and cross they fold into, and
/// one interactive loader driven by its status.
/// </summary>
public sealed class MorphLoaderPage : ArcItemPage
{
    public override string ItemSlug => "morph-loader";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        tiles.Children.Add(Tile(new ArcMorphLoader { Variant = ArcMorphLoaderVariant.Dots }, "Dots"));
        tiles.Children.Add(Tile(new ArcMorphLoader { Variant = ArcMorphLoaderVariant.Bars }, "Bars"));
        tiles.Children.Add(Tile(new ArcMorphLoader { Variant = ArcMorphLoaderVariant.Ring }, "Ring"));
        tiles.Children.Add(Tile(new ArcMorphLoader { Variant = ArcMorphLoaderVariant.Square }, "Square"));
        tiles.Children.Add(Tile(
            new ArcMorphLoader { Variant = ArcMorphLoaderVariant.Ring, Status = ArcMorphLoaderStatus.Success, SuccessLabel = "Published" },
            "Success"));
        tiles.Children.Add(Tile(
            new ArcMorphLoader { Variant = ArcMorphLoaderVariant.Ring, Status = ArcMorphLoaderStatus.Error, ErrorLabel = "Publish failed" },
            "Error"));

        var live = new ArcMorphLoader { Variant = ArcMorphLoaderVariant.Ring, Status = ArcMorphLoaderStatus.Loading, Label = "Publishing" };
        var label = new TextBlock { Text = "Publishing your changes", Classes = { "arc-label" }, VerticalAlignment = VerticalAlignment.Center };
        var status = new TextBlock { Text = "In progress", Classes = { "arc-caption" }, VerticalAlignment = VerticalAlignment.Center };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(live);
        row.Children.Add(label);
        row.Children.Add(status);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var success = new ArcButton { Variant = ArcButtonVariant.Secondary, Size = ArcButtonSize.Sm, Content = "Succeed" };
        success.Click += (_, _) =>
        {
            live.Status = ArcMorphLoaderStatus.Success;
            status.Text = "Published";
        };
        var error = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Content = "Fail" };
        error.Click += (_, _) =>
        {
            live.Status = ArcMorphLoaderStatus.Error;
            status.Text = "Publish failed";
        };
        var reset = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Content = "Reset" };
        reset.Click += (_, _) =>
        {
            live.Status = ArcMorphLoaderStatus.Loading;
            status.Text = "In progress";
        };
        actions.Children.Add(success);
        actions.Children.Add(error);
        actions.Children.Add(reset);

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        footer.Children.Add(row);
        Grid.SetColumn(actions, 1);
        footer.Children.Add(actions);

        var stack = new StackPanel { Spacing = 14 };
        stack.Children.Add(tiles);
        var divider = new Border { Height = 1 };
        divider.Paint(Border.BackgroundProperty, ArcTokens.Border);
        stack.Children.Add(divider);
        stack.Children.Add(footer);
        return Sv.Card(stack, 16);
    }

    private static Control Tile(ArcMorphLoader loader, string caption)
    {
        var content = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        loader.HorizontalAlignment = HorizontalAlignment.Center;
        loader.Width = 26;
        loader.Height = 26;
        content.Children.Add(loader);
        var text = Sv.Caption(caption);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        content.Children.Add(text);

        var tile = new Border
        {
            Width = 92,
            Height = 84,
            CornerRadius = new CornerRadius(16),
            Child = content
        };
        tile.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
        return tile;
    }

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Inline progress inside a button, row or field where the result should appear in the same spot.",
        "Short operations that end in a clear success or failure, such as save, publish or upload.",
        "Any spinner where a tiny, crisp, brand-consistent mark matters."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "When the percentage is known, use progress instead.",
        "When content is loading into a layout, use a skeleton."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The loader is a status region: its label changes from Loading to Done or Failed and is announced politely.",
        "Completion is never colour alone, because the shape itself becomes a check or a cross.",
        "Inside a button that already announces progress, mark the loader decorative so the state is not read twice."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Dots bounce in a wave, bars grow in sequence, the ring turns while its arcs breathe, and the square tumbles a quarter turn at a time.",
        "On success or error the loop fades, the strokes draw the mark in order and the mark pops once.",
        "Reduced motion shows a still shape per state and swaps instantly."
    };

    public override string? ImplementationNotes =>
        "Recreated from the public morph-loader documentation. The reference springs each stroke's centre, length, bend and width independently; this " +
        "port keeps one mark and blends between shapes with a crossfade and a short scale, so variants morph in place without four independent springs. " +
        "Sizes from 16 to 32 px suit inline use; the demo uses 26 px.";
}
