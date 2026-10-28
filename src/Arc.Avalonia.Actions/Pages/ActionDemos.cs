using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Layout helpers for the action pages. Floating surfaces are hosted inside the page, so a demo that
/// shows a menu or a dialog open reserves the room it needs instead of overlapping its neighbours.
/// </summary>
public static class ActionDemos
{
    public static Control Section(string title, string description, Control content) =>
        DemoUi.Section(title, description, content);

    public static Control Row(params Control[] children) => DemoUi.Row(children);

    public static Control Column(params Control[] children) => DemoUi.Column(children);

    /// <summary>A fixed box that gives an open menu somewhere to land without covering the next section.</summary>
    public static Control Reserved(double height, Control content) => new Panel
    {
        Height = height,
        HorizontalAlignment = HorizontalAlignment.Left,
        Children = { content }
    };

    /// <summary>The same box with a width, so the open surface cannot reach the next demo in the row.</summary>
    public static Control Reserved(double width, double height, Control content) => new Panel
    {
        Width = width,
        Height = height,
        HorizontalAlignment = HorizontalAlignment.Left,
        Children = { content }
    };

    public static TextBlock Body(string text, double maxWidth = 560) => new()
    {
        Text = text,
        Classes = { "arc-body-sm", "arc-secondary" },
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = maxWidth,
        HorizontalAlignment = HorizontalAlignment.Left
    };

    public static TextBlock Caption(string text) => new()
    {
        Text = text,
        Classes = { "arc-caption" },
        TextWrapping = TextWrapping.Wrap
    };

    /// <summary>
    /// A muted stage that acts as the "page" behind a modal demo. The dialog or drawer fills the stage,
    /// so its dimmed backdrop and floating panel read exactly as they do over a real window.
    /// </summary>
    public static Panel Stage(double width, double height, bool mockPage = true)
    {
        var stage = new Panel
        {
            Width = width,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Left,
            ClipToBounds = true
        };

        stage.Children.Add(new Border
        {
            Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(26)
        });

        if (mockPage)
        {
            stage.Children.Add(MockPage());
        }

        return stage;
    }

    private static Control MockPage()
    {
        var panel = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(28, 22, 28, 22)
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Billing settings",
            Classes = { "arc-heading" },
            HorizontalAlignment = HorizontalAlignment.Left
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Your plan renews on 12 March. Invoices are sent to finance@acme.com.",
            Classes = { "arc-body-sm", "arc-secondary" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 380,
            HorizontalAlignment = HorizontalAlignment.Left
        });

        for (var i = 0; i < 2; i++)
        {
            panel.Children.Add(new Border
            {
                Height = 44,
                CornerRadius = new CornerRadius(18),
                Background = DemoUi.Brush(ArcTokens.Surface),
                BorderBrush = DemoUi.Brush(ArcTokens.BorderSubtle),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = i switch
                    {
                        0 => "Payment method · Visa ending 4242",
                        1 => "Billing email · finance@acme.com",
                        _ => "Invoice history"
                    },
                    Classes = { "arc-body-sm", "arc-secondary" },
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(16, 0)
                }
            });
        }

        return panel;
    }
}
