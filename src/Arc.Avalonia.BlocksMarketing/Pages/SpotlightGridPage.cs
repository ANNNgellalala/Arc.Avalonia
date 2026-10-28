using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;
using Arc.Avalonia.BlocksMarketing.Controls;
using Mk = Arc.Avalonia.BlocksMarketing.MarketingKit;

namespace Arc.Avalonia.BlocksMarketing.Pages;

/// <summary>
/// Spotlight grid: a feature grid where the light follows the pointer and a click morphs a card into its
/// detail. The pointer light is a gradient that tracks the pointer under the card content; the morph is a
/// direct swap, since the port has no shared-element transition, and the detail panel is a page-hosted
/// surface rather than a dialog. Cards are focusable, so the detail is reachable without a pointer.
/// </summary>
public sealed class SpotlightGridPage : ArcItemPage
{
    public override string ItemSlug => "spotlight-grid";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A feature overview that has to stay interesting past the first screen.",
        "Product pages that want a small live demo per claim.",
        "Surfaces where a pointer trail is the only decoration you need."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each card is focusable, and Enter or Space opens its detail, so the pointer light is decoration only.",
        "The detail panel starts with a Back control and takes focus when it opens.",
        "Card titles and descriptions carry the meaning; the glow never encodes state."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The light follows the pointer with no easing of its own; the card border firms up as the pointer enters.",
        "Opening a detail swaps the grid in place. Reduced motion removes the light entirely."
    };

    public override string? ImplementationNotes =>
        "The pointer light is a local radial gradient brush redrawn under the card content, not a blur filter; it is skipped under reduced motion. A click swaps the grid for the detail panel, since Avalonia has no shared-element morph. The demo is the same light in every cell, so the specimen is sample UI rather than real feature code.";

    private sealed record Feature(string Title, string Caption, string Icon, string Body, string Detail);

    private static readonly Feature[] Features =
    {
        new("Realtime insights", "Numbers that update as work lands.", "activity",
            "Watch revenue, activation and churn move without a refresh.",
            "Every figure is derived from the same event stream your product already emits. The card's sparkline shows the last 12 weeks of sample data, and the detail view is where you would put the full chart."),
        new("Keyboard first", "Every surface has a shortcut.", "type",
            "Open, search and move without leaving the keys.",
            "The command bar takes one keystroke, the palette searches everything, and every row is reachable with the arrows. The sample shortcut chips show three of the bindings."),
        new("Automations", "Work that runs before you ask.", "zap",
            "Rules with a branch, a delay and a plain-language log.",
            "Rules are plain if-this-then-that; the log writes the decision in words, so the run can be explained to someone else."),
        new("Permissions", "Scoped access by role.", "shield-check",
            "Read-only guests, contributor seats and admin overrides.",
            "Roles are additive and always visible on the record. Guests never take a seat, and every override is written to the audit trail."),
        new("Audit trail", "Every change, in order.", "list",
            "Who changed what, with the before and after value.",
            "The trail keeps twelve months on the Team plan and longer on Business. Each entry links to the record it changed."),
        new("API and webhooks", "Typed endpoints, quiet retries.", "code",
            "One schema for every event you emit.",
            "The API returns typed payloads and rate-limit headers. Webhooks retry with backoff for 24 hours, then land in a dead-letter queue you can replay.")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var host = new ContentControl();
        var list = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12, IsVisible = true };
        var detail = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12, IsVisible = false };
        var body = new Panel { Children = { list, detail } };

        void ShowGrid()
        {
            detail.IsVisible = false;
            list.IsVisible = true;
        }

        void Show(Feature feature)
        {
            detail.Children.Clear();
            detail.Children.Add(Mk.Btn("All features", ArcButtonVariant.Ghost, ArcButtonSize.Sm, ShowGrid, icon: "arrow-left"));
            detail.Children.Add(Mk.Row(10,
                Mk.Disc(feature.Icon, 34, 16, accent: true),
                Mk.Stack(2,
                    Mk.Text(feature.Title, "arc-heading", 460),
                    Mk.Caption(feature.Caption))));
            detail.Children.Add(Mk.Body(feature.Body, 620));
            detail.Children.Add(Mk.Divider());
            detail.Children.Add(Mk.Body(feature.Detail, 620));
            list.IsVisible = false;
            detail.IsVisible = true;
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto")
        };
        for (var index = 0; index < Features.Length; index++)
        {
            var feature = Features[index];
            var card = new ArcSpotlightCard
            {
                Padding = new Thickness(16),
                Margin = new Thickness(index % 3 == 0 ? 0 : 6, index < 3 ? 0 : 6, index % 3 == 2 ? 0 : 6, 0),
                Invoke = () => Show(feature)
            };
            card.Label = feature.Title;

            card.Child = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 8,
                Children =
                {
                    Mk.Disc(feature.Icon, 30, 14, accent: true),
                    Mk.Text(feature.Title, "arc-body arc-medium", 220),
                    Mk.Caption(feature.Caption),
                    Demo(index)
                }
            };

            Grid.SetColumn(card, index % 3);
            if (index >= 3)
            {
                Grid.SetRow(card, 1);
            }

            grid.Children.Add(card);
        }
        list.Children.Add(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                Mk.Stack(3,
                    Mk.Heading("Six features, one surface", 440),
                    Mk.Caption("Move the pointer across a card, then open one.")),
                Mk.Pill("Pro block", ArcBadgeTone.Accent)
            }
        });

        var pillAlign = ((Grid)list.Children[0]).Children[1];
        pillAlign.HorizontalAlignment = HorizontalAlignment.Right;
        pillAlign.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(pillAlign, 1);

        list.Children.Add(grid);

        host.Content = body;

        return Mk.Surface(new Border { Padding = new Thickness(24, 22, 24, 22), Child = host });
    }

    /// <summary>A small live fragment per card, so the grid reads as product UI rather than six paragraphs.</summary>
    private static Control Demo(int index)
    {
        switch (index)
        {
            case 1:
                var keys = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (var key in new[] { "K", "/", "G I" })
                {
                    keys.Children.Add(new Border
                    {
                        Background = Mk.Brush(ArcTokens.SurfaceMuted),
                        CornerRadius = new CornerRadius(7),
                        Padding = new Thickness(7, 2, 7, 2),
                        Margin = new Thickness(0, 0, 5, 0),
                        Child = Mk.Text(key, "arc-mono")
                    });
                }

                return keys;

            case 2:
                return new Arc.Avalonia.Inputs.Controls.ArcSwitch { Label = "On", Checked = true };

            case 3:
                return Mk.Row(6,
                    Mk.Pill("Guest", ArcBadgeTone.Neutral),
                    Mk.Pill("Contributor", ArcBadgeTone.Accent));

            case 4:
                var rows = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
                rows.Children.Add(Mk.Caption("Maya renamed the project · 09:41"));
                rows.Children.Add(Mk.Caption("Jonas moved 3 issues · 09:38"));
                return rows;

            case 5:
                return Mk.Stack(4,
                    Mk.Text("POST /v1/events  200", "arc-mono"),
                    Mk.Caption("Typed payload, 41 ms"));

            default:
                return Mk.Spark(new double[] { 12, 15, 13, 18, 17, 21, 20, 24, 23, 27, 26, 31 }, 36);
        }
    }
}

