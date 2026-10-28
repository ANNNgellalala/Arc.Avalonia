using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery.Pages;

/// <summary>The design token page: colour roles and the type scale.</summary>
public sealed class FoundationsPage : ArcPage
{
    public override string Title => "Foundations";

    public override string Group => "Overview";

    public override string? Description =>
        "The token layer every Arc component resolves at runtime: semantic colour roles and the type scale. Radii, spacing, elevation and motion continue on the Tokens page.";

    public override int Order => 0;

    public override Control Build(ArcDemoContext context)
    {
        var stack = new StackPanel();

        stack.Children.Add(DemoUi.Section(
            "Colour roles",
            "Components use semantic roles only. Most of a screen stays neutral; accent is spent on the active item, one data series, progress, and interaction details.",
            new WrapPanel
            {
                Children =
                {
                    Swatch("background", ArcTokens.Background),
                    Swatch("surface", ArcTokens.Surface),
                    Swatch("surface-muted", ArcTokens.SurfaceMuted),
                    Swatch("foreground", ArcTokens.Foreground),
                    Swatch("text-secondary", ArcTokens.TextSecondary),
                    Swatch("text-muted", ArcTokens.TextMuted),
                    Swatch("border", ArcTokens.Border),
                    Swatch("border-strong", ArcTokens.BorderStrong),
                    Swatch("accent", ArcTokens.Accent),
                    Swatch("accent-subtle", ArcTokens.AccentSubtle),
                    Swatch("success", ArcTokens.Success),
                    Swatch("warning", ArcTokens.Warning),
                    Swatch("danger", ArcTokens.Danger)
                }
            }));

        stack.Children.Add(DemoUi.Section(
            "Type scale",
            "Inter for body and, as a documented substitution, for display too (Geist is not available offline). Weights are 400 and 500 only; hierarchy comes from size and colour.",
            new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    TypeRow("arc-display", "Display 72"),
                    TypeRow("arc-title", "Title 52"),
                    TypeRow("arc-title-sm", "Title 36"),
                    TypeRow("arc-subtitle", "Subtitle 28"),
                    TypeRow("arc-heading", "Heading 22"),
                    TypeRow("arc-body-lg", "Body large 18"),
                    TypeRow("arc-body", "Body 16"),
                    TypeRow("arc-body-sm", "Body small 14"),
                    TypeRow("arc-caption", "Caption 12")
                }
            }));

        return stack;
    }

    private static Control Swatch(string name, string key) => new Border
    {
        Width = 124,
        Margin = new Thickness(0, 0, 10, 14),
        Child = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new Border
                {
                    Height = 56,
                    CornerRadius = new CornerRadius(16),
                    Background = DemoUi.Brush(key),
                    BorderBrush = DemoUi.Brush(ArcTokens.Border),
                    BorderThickness = new Thickness(1)
                },
                new TextBlock { Text = name, Classes = { "arc-caption" } }
            }
        }
    };

    private static Control TypeRow(string style, string label) => new StackPanel
    {
        Spacing = 2,
        Children =
        {
            new TextBlock { Text = "Arc for Avalonia", Classes = { style } },
            new TextBlock { Text = $"{label} · .{style}", Classes = { "arc-caption" } }
        }
    };
}
