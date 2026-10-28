using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Effects.Pages;

/// <summary>
/// Layout helpers for the effect pages. Floating surfaces are hosted inside the page, so a demo that
/// shows a hover card, a sheet or a consent bar reserves a stage instead of overlapping its neighbours.
/// </summary>
internal static class EffectDemos
{
    /// <summary>A muted stage with a hairline border, standing in for the page behind an overlay.</summary>
    public static Panel Stage(double width, double height, bool mockPage = false, bool clip = true)
    {
        var stage = new Panel
        {
            Width = width,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Left,
            ClipToBounds = clip
        };

        stage.Children.Add(new Border
        {
            Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(26),
            BorderBrush = DemoUi.Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1)
        });

        if (mockPage)
        {
            stage.Children.Add(MockPage());
        }

        return stage;
    }

    private static Control MockPage()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(24, 20, 24, 20) };
        panel.Children.Add(new TextBlock
        {
            Text = "Trip details",
            Classes = { "arc-heading" },
            HorizontalAlignment = HorizontalAlignment.Left
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Lisbon, 3 nights. Flights and hotel are booked.",
            Classes = { "arc-body-sm", "arc-secondary" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Left
        });

        panel.Children.Add(new Border
        {
            Height = 44,
            CornerRadius = new CornerRadius(18),
            Background = DemoUi.Brush(ArcTokens.Surface),
            BorderBrush = DemoUi.Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 360,
            Child = new TextBlock
            {
                Text = "Flight TP 1042 · 12 October",
                Classes = { "arc-body-sm", "arc-secondary" },
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0)
            }
        });

        return panel;
    }

    /// <summary>A rounded row used inside sheet and card demos.</summary>
    public static Control DetailRow(string label, string value)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(new TextBlock { Text = label, Classes = { "arc-body-sm", "arc-secondary" } });
        var right = new TextBlock { Text = value, Classes = { "arc-label" } };
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        return new Border
        {
            CornerRadius = new CornerRadius(18),
            Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
            Padding = new Thickness(14, 12),
            Child = grid
        };
    }

    /// <summary>A caption above a demo, at the 12 px caption size.</summary>
    public static Control Labelled(string label, Control content)
    {
        var panel = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        panel.Children.Add(content);
        return panel;
    }

    public static Control Section(string title, string description, Control content) =>
        DemoUi.Section(title, description, content);
}
