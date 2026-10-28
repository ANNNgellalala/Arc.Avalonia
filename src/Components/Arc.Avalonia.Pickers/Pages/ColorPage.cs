using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// Pickers, part three: the color surface. The closed triggers sit beside the open editor so the swatch,
/// the typed value and the contrast readout can all be checked in one shot.
/// </summary>
public sealed class ColorPage : ArcPage
{
    public override string Title => "Color";

    public override string Group => "Pickers";

    public override int Order => 502;

    public override string Description =>
        "A swatch that opens the full color surface: saturation and brightness, hue and opacity, a typed " +
        "value, saved swatches and the WCAG contrast readout against white and black.";

    public override Control Build(ArcDemoContext context)
    {
        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Color picker",
                    "Change the color by dragging the square or the ramps, by typing a hex, rgb() or hsl() value, or by picking a saved swatch. The contrast chips grade the current color as you pick.",
                    BuildPicker())
            }
        };
    }

    private static Control BuildPicker()
    {
        var accent = ArcResources.Color(ArcTokens.Accent);
        var brand = ArcResources.Color(ArcTokens.Series1);
        var picker = new ArcColorPicker
        {
            Width = 280,
            HorizontalAlignment = HorizontalAlignment.Left,
            Label = "Brand color",
            Value = brand,
            DefaultFormat = ArcColorFormat.Hex
        };

        var secondary = new ArcColorPicker
        {
            Width = 280,
            HorizontalAlignment = HorizontalAlignment.Left,
            Label = "Surface tint",
            Value = ArcColor.Mix(ArcResources.Color(ArcTokens.SurfaceMuted), brand, 0.25),
            DefaultFormat = ArcColorFormat.Rgb
        };

        var swatches = new[]
        {
            new ArcColorSwatch("brand", brand),
            new ArcColorSwatch("accent", accent),
            new ArcColorSwatch("ink", ArcResources.Color(ArcTokens.Foreground)),
            new ArcColorSwatch("series-2", ArcResources.Color(ArcTokens.Series2)),
            new ArcColorSwatch("series-3", ArcResources.Color(ArcTokens.Series3)),
            new ArcColorSwatch("series-4", ArcResources.Color(ArcTokens.Series4))
        };

        var editor = new ArcColorEditor
        {
            Value = brand,
            Swatches = swatches,
            Format = ArcColorFormat.Hex
        };

        var fields = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                picker,
                secondary,
                DemoUi.Caption("The trigger shows the swatch and the value; the open surface is on the right.")
            }
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 32 };
        row.Children.Add(fields);
        row.Children.Add(PickerDemo.Surface(editor));
        return row;
    }
}
