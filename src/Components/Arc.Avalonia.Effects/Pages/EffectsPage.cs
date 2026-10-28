using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Effects.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Effects.Pages;

/// <summary>
/// The page-level effects: announcement bar, cookie consent, hover card, bottom sheet and expandable
/// card. Every floating surface is hosted inside a reserved stage, so the page screenshot shows the open
/// state instead of a portal that the offscreen renderer would never see.
/// </summary>
public sealed class EffectsPage : ArcPage
{
    public override string Title => "Effects";

    public override string Group => "Effects";

    public override string? Description =>
        "Banners, consent, previews and sheets that appear over the page. Surfaces are hosted in a " +
        "reserved stage on the page itself, so each demo shows its open state.";

    public override int Order => 800;

    public override Control Build(ArcDemoContext context)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 24
        };

        var announcement = EffectDemos.Section(
            "Announcement bar",
            "A slim site-wide notice that rotates messages, counts down and collapses when dismissed.",
            AnnouncementDemo());
        Grid.SetColumn(announcement, 0);
        grid.Children.Add(announcement);

        var expandable = EffectDemos.Section(
            "Expandable card",
            "A dense summary that opens in place when someone wants the details.",
            ExpandableDemo());
        Grid.SetColumn(expandable, 1);
        grid.Children.Add(expandable);

        var hover = EffectDemos.Section(
            "Hover card",
            "A read-only preview of a person on hover or focus, for mentions and author names.",
            HoverDemo());
        Grid.SetRow(hover, 1);
        grid.Children.Add(hover);

        var consent = EffectDemos.Section(
            "Cookie consent",
            "Per-category consent in the bar, the panel and the small reopen button; Customize opens the panel.",
            ConsentDemo());
        Grid.SetRow(consent, 1);
        Grid.SetColumn(consent, 1);
        grid.Children.Add(consent);

        var sheet = EffectDemos.Section(
            "Bottom sheet",
            "A modal sheet resting at a peek height, dragged to full height or dismissed, shown open here.",
            SheetDemo());
        Grid.SetRow(sheet, 2);
        Grid.SetColumnSpan(sheet, 2);
        grid.Children.Add(sheet);

        return grid;
    }

    private static Control AnnouncementDemo()
    {
        var bar = new ArcAnnouncementBar
        {
            Id = "effects-demo",
            Controls = true,
            Tone = ArcAnnouncementTone.Neutral,
            Messages = new[]
            {
                new ArcAnnouncement
                {
                    Id = "sale",
                    Message = "Fall sale: 30% off",
                    Countdown = new ArcAnnouncementCountdown
                    {
                        To = DateTimeOffset.UtcNow.AddDays(3).AddHours(4),
                        Label = "Ends in"
                    },
                    Action = new ArcAnnouncementAction { Label = "See plans" }
                },
                new ArcAnnouncement
                {
                    Id = "launch",
                    Message = "Workflows are now in beta",
                    Action = new ArcAnnouncementAction { Label = "Read more" }
                }
            }
        };

        var inverted = new ArcAnnouncementBar
        {
            Tone = ArcAnnouncementTone.Inverted,
            Dismissible = false,
            Interval = 9000,
            Messages = new[]
            {
                new ArcAnnouncement { Id = "inverted", Message = "Maintenance on Sunday, 02:00 to 04:00 UTC" }
            }
        };

        return new StackPanel { Spacing = 12, Children = { bar, inverted } };
    }

    private static Control ExpandableDemo()
    {
        var details = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                EffectDemos.DetailRow("Seats", "12 of 20 used"),
                EffectDemos.DetailRow("Renews", "4 March 2027")
            }
        };

        return new ArcExpandableCard
        {
            Title = "Pro plan",
            Description = "Billed yearly, renews 4 March",
            Content = details,
            IsExpanded = true,
            CollapsedWidth = 440,
            ExpandedWidth = 470
        };
    }

    private static Control HoverDemo()
    {
        var card = new ArcHoverCard
        {
            Side = ArcHoverCardSide.Right,
            Align = ArcHoverCardAlign.Center,
            IsOpen = true,
            OpenDelay = 120,
            Content = new ArcHoverProfile
            {
                Name = "Maya Chen",
                Role = "Product designer, Payments",
                Bio = "Checkout and pricing.",
                Stats = new[]
                {
                    new ArcHoverStat { Label = "Projects", Value = "12" },
                    new ArcHoverStat { Label = "Reviews", Value = "48" }
                }
            },
            Trigger = new ArcButton
            {
                Content = "@maya",
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm
            }
        };

        var stage = EffectDemos.Stage(518, 212);
        var host = new Panel { Margin = new Thickness(24, 0, 24, 0) };
        card.HorizontalAlignment = HorizontalAlignment.Left;
        card.VerticalAlignment = VerticalAlignment.Center;
        host.Children.Add(card);
        stage.Children.Add(host);

        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                stage,
                DemoUi.Caption("The first card is pinned open for the page; the trigger opens it on hover or focus.")
            }
        };
    }

    private static Control ConsentDemo()
    {
        var consent = new ArcCookieConsent
        {
            StorageKey = "effects-demo-consent",
            Position = ArcCookieConsentPosition.Bottom,
            Policy = new ArcCookiePolicy
            {
                Label = "Cookie policy",
                OnClick = () => { }
            }
        };

        var stage = EffectDemos.Stage(518, 230, clip: false);
        stage.Children.Add(consent);

        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                stage,
                DemoUi.Caption("Accept, reject or save and the bar folds into a 48 px button; the panel opens over the stage.")
            }
        };
    }

    private static Control SheetDemo()
    {
        var sheet = new ArcBottomSheet
        {
            Title = "Lisbon, 3 nights",
            Description = "Oct 12 to Oct 15",
            IsOpen = true,
            InitialDetent = 0,
            Detents = new[] { 0.68, 0.94 },
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    EffectDemos.DetailRow("Flight", "TP 1042 · 09:10"),
                    EffectDemos.DetailRow("Hotel", "Alfama Loft · 3 nights"),
                    new TextBlock
                    {
                        Text = "Drag the header or use the arrow keys on the grabber to change height. " +
                               "Dragging past the smallest height dismisses the sheet.",
                        Classes = { "arc-caption" },
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 6, 0, 0)
                    }
                }
            }
        };

        var stage = EffectDemos.Stage(1068, 288, mockPage: true);

        var open = new ArcButton
        {
            Content = "Open sheet",
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 20, 24, 0)
        };
        open.Click += (_, _) => sheet.IsOpen = true;
        stage.Children.Add(open);
        stage.Children.Add(sheet);

        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                stage,
                DemoUi.Caption("The sheet rests at 68% and can be dragged to 94%; the scrim tracks the drag.")
            }
        };
    }
}
