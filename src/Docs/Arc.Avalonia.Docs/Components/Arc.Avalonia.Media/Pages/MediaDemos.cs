using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Media.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Media.Pages;

/// <summary>
/// Demo content for the media and gesture pages. Each builder shows the component at a useful size and in
/// the state the documentation calls out, so the screenshots double as a specification.
/// </summary>
internal static class MediaDemos
{
    public static Control HoldToConfirm()
    {
        var status = DemoUi.Caption("Release early and the fill drains with a “hold longer” hint.");

        var delete = new ArcHoldToConfirm
        {
            Label = "Hold to delete project",
            ConfirmedLabel = "Project deleted",
            Duration = 1200
        };
        delete.Confirmed += (_, _) => status.Text = "Confirmed: the project was deleted.";

        var publish = new ArcHoldToConfirm
        {
            Label = "Hold to publish",
            ConfirmedLabel = "Published",
            Duration = 900,
            Tone = ArcHoldToConfirmTone.Neutral,
            Icon = "upload"
        };

        var done = new ArcHoldToConfirm
        {
            Label = "Hold to delete project",
            ConfirmedLabel = "Project deleted",
            IsConfirmed = true
        };

        return DemoUi.Column(DemoUi.Row(delete, publish, done), status);
    }

    public static Control SwipeActions()
    {
        var status = DemoUi.Caption(
            "Swipe with the mouse, or focus a row and press Left. Tab walks into the revealed actions and Escape puts the row away.");

        var open = new ArcSwipeActions
        {
            Label = "Design review",
            Width = 660,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = MailRow("Maya Chen", "Design review", "9:41"),
            Leading = new List<ArcSwipeAction>
            {
                new("Unread", "mail", ArcSwipeActionTone.Accent) { KeepRow = true }
            },
            Trailing = new List<ArcSwipeAction>
            {
                new("Archive", "archive"),
                new("Delete", "trash-2", ArcSwipeActionTone.Danger)
            }
        };
        var opened = false;
        open.AttachedToVisualTree += (_, _) =>
        {
            if (opened)
            {
                return;
            }

            opened = true;
            Dispatcher.UIThread.Post(() => open.Open(1), DispatcherPriority.Loaded);
        };
        open.ActionInvoked += (_, action) => status.Text = $"{action.Label} chosen for the design review.";

        var closed = new ArcSwipeActions
        {
            Label = "Invoice 2041",
            Width = 660,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = MailRow("Accounts", "Invoice 2041", "Yesterday"),
            Trailing = new List<ArcSwipeAction>
            {
                new("Flag", "flag"),
                new("Archive", "archive")
            }
        };
        closed.ActionInvoked += (_, action) => status.Text = $"{action.Label} chosen for invoice 2041.";

        return DemoUi.Column(open, closed, status);
    }

    public static Control Reactions()
    {
        var status = DemoUi.Caption("One tap toggles your reaction; the count ticks to its new value.");

        var options = new List<ArcReactionOption>
        {
            new("like", "Like", "thumbs-up"),
            new("love", "Love", "heart"),
            new("wow", "Wow", "sparkles"),
            new("party", "Celebrate", "star")
        };

        var bar = new ArcReactions
        {
            Label = "Reactions on the design review",
            Options = options,
            DefaultValue = new List<ArcReaction>
            {
                new("like", 12),
                new("love", 4, true)
            }
        };
        bar.ValueChanged += (_, args) =>
            status.Text = $"{args.Change.Id} {(args.Change.Reacted ? "added" : "removed")}.";

        var release = new ArcReactions
        {
            Label = "Reactions on the release note",
            Options = options,
            DefaultValue = new List<ArcReaction>
            {
                new("love", 128, true),
                new("party", 9)
            }
        };

        var reviewRow = new StackPanel { Spacing = 6 };
        reviewRow.Children.Add(new TextBlock { Text = "Design review", Classes = { "arc-caption" } });
        reviewRow.Children.Add(bar);

        var releaseRow = new StackPanel { Spacing = 6 };
        releaseRow.Children.Add(new TextBlock { Text = "Release note", Classes = { "arc-caption" } });
        releaseRow.Children.Add(release);

        return DemoUi.Column(reviewRow, releaseRow, status);
    }

    public static Control ImageCompare()
    {
        var wide = new ArcImageCompare
        {
            Position = 35,
            Label = "Portrait edit",
            BeforeLabel = "Original",
            AfterLabel = "Retouched",
            AspectRatio = 2.0,
            MaxWidth = 400
        };

        var tall = new ArcImageCompare
        {
            DefaultPosition = 62,
            Orientation = ArcImageCompareOrientation.Vertical,
            Label = "Poster edit",
            BeforeLabel = "Before",
            AfterLabel = "After",
            AspectRatio = 1.05,
            Width = 210
        };

        return DemoUi.Column(DemoUi.Row(wide, tall),
            DemoUi.Caption("No file path is supplied here, so both frames draw the placeholder mesh. Arrow keys move one percent, Shift ten."));
    }

    public static Control Carousel()
    {
        return new ArcCarousel
        {
            Label = "Harbour suites",
            DefaultIndex = 1,
            SlideSize = 300,
            Interval = 5000,
            Items = new List<ArcCarouselItem>
            {
                new("Sea view suite", "Floor to ceiling glass and a private terrace."),
                new("Garden suite", "Quiet rooms around a planted courtyard."),
                new("Loft suite", "A double height studio under the roof."),
                new("Harbour suite", "Corner windows over the water.")
            }
        };
    }

    public static Control CardStack()
    {
        var status = DemoUi.Caption("Drag the top card, or use the buttons. Backspace undoes the last decision.");

        var stack = new ArcCardStack
        {
            Label = "City shortlist",
            CardWidth = 400,
            LeftLabel = "Skip",
            RightLabel = "Shortlist",
            LeftOutcome = "skipped",
            RightOutcome = "shortlisted",
            Cards = new List<ArcCardStackItem>
            {
                new("lis", "Lisbon", "Lisbon", "Tiled facades, steep streets, and the light coming off the river."),
                new("osl", "Oslo", "Oslo", "Fjord swimming in summer, quiet design in winter."),
                new("kyo", "Kyoto", "Kyoto", "Temple gardens and a very particular sense of season."),
                new("cph", "Copenhagen", "Copenhagen", "Harbour baths, bicycles, and long summer evenings.")
            }
        };
        stack.CardDismissed += (_, args) =>
            status.Text = args.Decision == ArcCardDecision.Left
                ? $"{args.Item.Label} skipped."
                : $"{args.Item.Label} shortlisted.";
        stack.CardUndone += (_, args) => status.Text = $"{args.Item.Label} restored.";

        return DemoUi.Column(stack, status);
    }

    public static Control ScrollArea()
    {
        var vertical = Framed(new ArcScrollArea
        {
            Label = "Recent activity",
            Width = 380,
            Height = 196,
            Fade = 24,
            Scrollbars = ArcScrollbarsMode.Auto,
            Content = ActivityList(14)
        });

        var horizontal = Framed(new ArcScrollArea
        {
            Label = "Templates",
            Orientation = ArcScrollOrientation.Horizontal,
            Width = 380,
            Height = 196,
            Fade = 20,
            Scrollbars = ArcScrollbarsMode.Always,
            Content = TemplateStrip(8)
        });

        var fitted = Framed(new ArcScrollArea
        {
            Label = "Status",
            Width = 220,
            Height = 196,
            Fade = 24,
            Content = ActivityList(2)
        });

        return DemoUi.Column(DemoUi.Row(vertical, horizontal, fitted),
            DemoUi.Caption("Bars fade in while scrolling or hovering. The middle strip keeps them visible; the last panel has nothing to fade."));
    }

    public static Control ResizablePanels()
    {
        var horizontal = new ArcResizablePanels
        {
            Label = "Mail",
            Width = 620,
            Height = 250,
            Panes = new List<ArcResizablePane>
            {
                new("folders", "Folders", 24, 120, FolderList()) { Collapsible = true },
                new("messages", "Messages", 46, 200, MessageList()),
                new("preview", "Preview", 30, 200, PreviewPane())
            }
        };

        var vertical = new ArcResizablePanels
        {
            Label = "Inspector",
            Width = 300,
            Height = 250,
            Orientation = ArcPanelOrientation.Vertical,
            Panes = new List<ArcResizablePane>
            {
                new("layers", "Layers", 58, 80, LayerList()) { Collapsible = true },
                new("properties", "Properties", 42, 80, PropertyList())
            }
        };

        return DemoUi.Column(DemoUi.Row(horizontal, vertical),
            DemoUi.Caption("Drag a divider, or focus it and use the arrow keys. Enter collapses a collapsible pane; a double click resets the layout."));
    }

    private static Control Framed(Control child) => new Border
    {
        Background = DemoUi.Brush(ArcTokens.Surface),
        BorderBrush = DemoUi.Brush(ArcTokens.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(26),
        ClipToBounds = true,
        Child = child
    };

    private static Control MailRow(string sender, string subject, string time)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };

        var avatar = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(9999),
            Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = sender.Substring(0, 1),
                FontSize = ArcResources.Double(ArcTokens.TextXs),
                FontWeight = FontWeight.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        grid.Children.Add(avatar);

        var left = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        left.Children.Add(new TextBlock { Text = sender, Classes = { "arc-label" } });
        left.Children.Add(new TextBlock { Text = subject, Classes = { "arc-caption" } });
        Grid.SetColumn(left, 1);
        grid.Children.Add(left);

        var stamp = new TextBlock
        {
            Text = time,
            Classes = { "arc-caption" },
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        Grid.SetColumn(stamp, 2);
        grid.Children.Add(stamp);
        return grid;
    }

    private static Control ActivityList(int count)
    {
        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(16) };
        for (var index = 1; index <= count; index++)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            row.Children.Add(new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(99),
                Background = DemoUi.Brush(ArcTokens.Accent),
                VerticalAlignment = VerticalAlignment.Center
            });

            var text = new TextBlock
            {
                Text = $"Event {index} · updated the workspace",
                Classes = { "arc-body-sm" },
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 2,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            panel.Children.Add(row);
        }

        return panel;
    }

    private static Control TemplateStrip(int count)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(16)
        };
        for (var index = 1; index <= count; index++)
        {
            var card = new Border
            {
                Width = 116,
                Height = 128,
                CornerRadius = new CornerRadius(18),
                BorderThickness = new Thickness(1),
                BorderBrush = DemoUi.Brush(ArcTokens.Border),
                Background = DemoUi.Brush(ArcTokens.GradientSoft),
                Padding = new Thickness(12),
                Child = new TextBlock
                {
                    Text = $"Template {index}",
                    Classes = { "arc-caption" }
                }
            };
            panel.Children.Add(card);
        }

        return panel;
    }

    private static Control FolderList()
    {
        var panel = new StackPanel { Spacing = 2, Margin = new Thickness(16) };
        var folders = new[] { "Inbox", "Starred", "Drafts", "Sent", "Archive", "Trash" };
        for (var index = 0; index < folders.Length; index++)
        {
            var row = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 6),
                Child = new TextBlock
                {
                    Text = folders[index],
                    Classes = { index == 0 ? "arc-label" : "arc-body-sm" }
                }
            };
            if (index == 0)
            {
                row.Background = DemoUi.Brush(ArcTokens.AccentSubtle);
            }

            panel.Children.Add(row);
        }

        return new Border
        {
            Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
            Child = panel
        };
    }

    private static Control MessageList()
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(16) };
        var messages = new[]
        {
            ("Maya Chen", "Re: design review"),
            ("Accounts", "Invoice 2041 is ready"),
            ("Leo Park", "Token naming, one last pass"),
            ("Sara Ali", "Carousel copy")
        };
        foreach (var (sender, subject) in messages)
        {
            var row = new StackPanel { Spacing = 1 };
            row.Children.Add(new TextBlock { Text = sender, Classes = { "arc-label" } });
            row.Children.Add(new TextBlock
            {
                Text = subject,
                Classes = { "arc-caption" },
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            panel.Children.Add(new Border { Padding = new Thickness(0, 4), Child = row });
        }

        return panel;
    }

    private static Control PreviewPane()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "Carousel copy", Classes = { "arc-heading" } });
        panel.Children.Add(new TextBlock
        {
            Text = "Neighbouring slides should hint at more without asking for attention. Keep one sentence per slide and let the row carry the rhythm.",
            Classes = { "arc-body-sm", "arc-secondary" },
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock { Text = "Sara Ali · 2:14 pm", Classes = { "arc-caption" } });
        return panel;
    }

    private static Control LayerList()
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(16) };
        foreach (var name in new[] { "Hero", "Carousel", "Footer" })
        {
            panel.Children.Add(new TextBlock { Text = name, Classes = { "arc-body-sm" } });
        }

        return panel;
    }

    private static Control PropertyList()
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "Width · 1180", Classes = { "arc-caption" } });
        panel.Children.Add(new TextBlock { Text = "Radius · 26", Classes = { "arc-caption" } });
        panel.Children.Add(new TextBlock { Text = "Gap · 24", Classes = { "arc-caption" } });
        return panel;
    }
}


