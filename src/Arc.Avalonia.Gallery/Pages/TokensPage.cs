using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery.Pages;

/// <summary>Radii, spacing, elevation and motion presets, the second half of the token tour.</summary>
public sealed class TokensPage : ArcPage
{
    public override string Title => "Tokens";

    public override string Group => "Overview";

    public override string? Description =>
        "Concentric radii, the 4px spacing grid, elevation, the spring presets, and the eight accents the palette ships with.";

    public override int Order => 3;

    public override Control Build(ArcDemoContext context)
    {
        var stack = new StackPanel();

        stack.Children.Add(DemoUi.Section(
            "Radii, concentric corners",
            "Control 18, panel 26, surface 34, pill. A nested corner is the outer radius minus the padding between them, so curves stay parallel.",
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                Children =
                {
                    RadiusSample("control 18", 18),
                    RadiusSample("panel 26", 26),
                    RadiusSample("surface 34", 34),
                    RadiusSample("pill", 999)
                }
            }));

        stack.Children.Add(DemoUi.Section(
            "Spacing",
            "A 4px grid: 4, 8, 12, 16, 24, 32 for product UI; 48, 64, 96 for sections. Controls are 36, 44, or 50 tall.",
            new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    SpacingRow("space-1", 4),
                    SpacingRow("space-2", 8),
                    SpacingRow("space-3", 12),
                    SpacingRow("space-4", 16),
                    SpacingRow("space-6", 24),
                    SpacingRow("space-8", 32),
                    SpacingRow("space-12", 48)
                }
            }));

        stack.Children.Add(DemoUi.Section(
            "Elevation",
            "Shadows only on layers that float: menus, popovers, dialogs, sheets. Cards rest on a hairline border with no shadow.",
            DemoUi.Canvas(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 24,
                Children =
                {
                    ElevationSample("resting", ArcTokens.ShadowResting),
                    ElevationSample("raised", ArcTokens.ShadowRaised),
                    ElevationSample("floating", ArcTokens.ShadowFloating)
                }
            })));

        stack.Children.Add(DemoUi.Section(
            "Accents",
            "Eight accents ship with the token set: neutral, violet, blue, green, amber, orange, coral, rose. Selection controls are tuned per accent so white thumbs always read against their track.",
            AccentRow()));

        return stack;
    }

    private static Control RadiusSample(string label, double radius) => new StackPanel
    {
        Spacing = 6,
        Children =
        {
            new Border
            {
                Width = 96,
                Height = 64,
                CornerRadius = new CornerRadius(radius),
                Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
                BorderBrush = DemoUi.Brush(ArcTokens.BorderStrong),
                BorderThickness = new Thickness(1)
            },
            new TextBlock { Text = label, Classes = { "arc-caption" } }
        }
    };

    private static Control SpacingRow(string label, double value) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 10,
        Children =
        {
            new Border
            {
                Width = value,
                Height = 12,
                CornerRadius = new CornerRadius(3),
                Background = DemoUi.Brush(ArcTokens.Accent)
            },
            new TextBlock { Text = $"{label} · {value:0}px", Classes = { "arc-caption" } }
        }
    };

    private static Control ElevationSample(string label, string shadowKey) => new StackPanel
    {
        Spacing = 8,
        Children =
        {
            new Border
            {
                Width = 132,
                Height = 76,
                CornerRadius = new CornerRadius(22),
                Background = DemoUi.Brush(ArcTokens.Surface),
                BoxShadow = ArcResources.Shadows(shadowKey)
            },
            new TextBlock { Text = label, Classes = { "arc-caption" } }
        }
    };

    private static Control AccentRow()
    {
        var panel = new WrapPanel();
        foreach (var accent in Enum.GetValues<ArcAccent>())
        {
            panel.Children.Add(AccentSample(accent));
        }
        return panel;
    }

    private static Control AccentSample(ArcAccent accent)
    {
        var palette = ArcPalette.Build(ArcThemeManager.Scheme, accent);
        var color = palette[ArcTokens.Accent] is ISolidColorBrush brush ? brush.Color : Colors.Black;
        return new StackPanel
        {
            Margin = new Thickness(0, 0, 14, 12),
            Spacing = 6,
            Children =
            {
                new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(14), Background = new SolidColorBrush(color) },
                new TextBlock { Text = accent.ToString(), Classes = { "arc-caption" } }
            }
        };
    }
}



