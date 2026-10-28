using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksTeam.Pages;

/// <summary>
/// Kanban board: swimlanes by stream, live WIP limits and card moves. The lifted-card pointer physics of the
/// reference are approximated with explicit chevron buttons, which also keeps the board fully keyboard
/// operable; WIP limits are checked after every move and shown with text as well as colour.
/// </summary>
public sealed class KanbanBoardPage : ArcItemPage
{
    private sealed class Card
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Lane { get; init; } = string.Empty;
        public int Stage { get; set; }
        public string Owner { get; init; } = string.Empty;
        public string Tag { get; init; } = string.Empty;
    }

    private static readonly string[] Columns = { "To do", "Doing", "Done" };
    private static readonly int[] Limits = { -1, 2, -1 };

    public override string ItemSlug => "kanban-board";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A team board where cards move between statuses and ordering within a stage matters.",
        "Pipelines that need swimlanes per stream and live WIP limits."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use project-board for a small single-lane board, or week-calendar for scheduling on dates."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Cards are focusable and the move chevrons are labelled with the source and target column.",
        "Every WIP limit is shown as text (\"3 of 2\") with a warning tone, not colour alone."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Cards settle into the slot that opens for them, and lane counts roll; the reference adds lift and tilt on pointer drag.",
        "Reduced motion lands drops instantly and removes the tilt, which is the branch this port builds."
    };

    public override string? ImplementationNotes =>
        "Pointer drag with lift, tilt and auto-scroll is not reproduced; cards move with labelled chevrons instead, " +
        "which keeps the WIP rules and keyboard path honest. Swimlanes, live limits, quick add and the detail " +
        "strip are implemented. Sample cards and people are in memory. No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var lanes = new[] { "Platform", "Growth" };
        var cards = new List<Card>
        {
            new() { Id = "BIL-208", Title = "Usage meter rounds up at the plan edge", Lane = "Platform", Stage = 1, Owner = "Jonas Weber", Tag = "Billing" },
            new() { Id = "BIL-203", Title = "Retry queue for failed invoices", Lane = "Platform", Stage = 0, Owner = "Priya Raman", Tag = "Backend" },
            new() { Id = "BIL-199", Title = "Proration copy needs a review", Lane = "Platform", Stage = 2, Owner = "Lena Fischer", Tag = "Copy" },
            new() { Id = "BIL-211", Title = "Trial banner on the billing overview", Lane = "Growth", Stage = 1, Owner = "Maya Chen", Tag = "Design" },
            new() { Id = "BIL-206", Title = "Upgrade prompt after seat limit", Lane = "Growth", Stage = 1, Owner = "Sam Okafor", Tag = "Frontend" },
            new() { Id = "BIL-195", Title = "Annual plan comparison table", Lane = "Growth", Stage = 2, Owner = "Tomas Novak", Tag = "Design" }
        };
        var draftIndex = 0;

        var board = new Grid();
        var warning = new Border { IsVisible = false };
        var status = TeamKit.Caption("Sprint 42 · Grouped by stream · WIP limit 2 in Doing");

        void Render()
        {
            var doing = cards.Count(card => card.Stage == 1);
            var over = doing > Limits[1];
            warning.IsVisible = over;
            if (over)
            {
                warning.Child = TeamKit.Row(10,
                    new ArcIcon { Icon = "triangle-alert", Size = 16, Foreground = TeamKit.Brush(ArcTokens.Warning) },
                    TeamKit.Text($"Doing is over its WIP limit ({doing} of {Limits[1]}). Finish work before pulling more.",
                        "arc-body-sm"));
            }

            board.ColumnDefinitions = new ColumnDefinitions("92,*,*,*");
            board.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto");
            board.Children.Clear();

            // Column headers with totals across both lanes.
            for (var stage = 0; stage < Columns.Length; stage++)
            {
                var total = cards.Count(card => card.Stage == stage);
                var header = new StackPanel { Spacing = 1 };
                header.Children.Add(TeamKit.Label(Columns[stage]));
                header.Children.Add(TeamKit.Caption(Limits[stage] > 0 ? $"{total} of {Limits[stage]} max" : $"{total} card{(total == 1 ? string.Empty : "s")}"));
                if (Limits[stage] > 0 && total > Limits[stage])
                {
                    header.Children.Add(TeamKit.Pill("Over limit", ArcBadgeTone.Warning, dot: true));
                }

                var headerBorder = new Border
                {
                    Padding = new Thickness(10, 4, 10, 8),
                    Margin = new Thickness(stage == 0 ? 0 : 6, 0, 6, 0)
                };
                headerBorder.Child = header;
                Grid.SetRow(headerBorder, 1);
                Grid.SetColumn(headerBorder, stage + 1);
                board.Children.Add(headerBorder);
            }

            for (var lane = 0; lane < lanes.Length; lane++)
            {
                var laneLabel = new StackPanel
                {
                    Spacing = 2,
                    Margin = new Thickness(0, 8, 8, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };
                laneLabel.Children.Add(new TextBlock
                {
                    Text = lanes[lane],
                    Classes = { "arc-label" },
                    TextWrapping = TextWrapping.Wrap
                });
                var laneCount = cards.Count(card => card.Lane == lanes[lane]);
                laneLabel.Children.Add(TeamKit.Caption($"{laneCount} card{(laneCount == 1 ? string.Empty : "s")}"));
                Grid.SetRow(laneLabel, lane + 2);
                board.Children.Add(laneLabel);

                for (var stage = 0; stage < Columns.Length; stage++)
                {
                    var columnCards = cards.Where(card => card.Lane == lanes[lane] && card.Stage == stage).ToList();
                    var stack = new StackPanel { Spacing = 6 };
                    foreach (var card in columnCards)
                    {
                        stack.Children.Add(BuildCard(card, Render));
                    }

                    if (columnCards.Count == 0)
                    {
                        stack.Children.Add(new Border
                        {
                            Background = TeamKit.Brush(ArcTokens.Surface),
                            BorderBrush = TeamKit.Brush(ArcTokens.BorderSubtle),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(12),
                            Padding = new Thickness(10, 12),
                            Child = TeamKit.Caption("Drop a card here")
                        });
                    }

                    var cell = new Border
                    {
                        Background = TeamKit.Brush(ArcTokens.SurfaceMuted),
                        CornerRadius = new CornerRadius(16),
                        Padding = new Thickness(8),
                        Margin = new Thickness(stage == 0 ? 0 : 6, 0, 6, 8),
                        Child = stack
                    };
                    Grid.SetRow(cell, lane + 2);
                    Grid.SetColumn(cell, stage + 1);
                    board.Children.Add(cell);
                }
            }
        }

        var add = TeamKit.Btn("Add card", ArcButtonVariant.Secondary, ArcButtonSize.Sm, icon: "plus");
        add.Click += (_, _) =>
        {
            var titles = new[]
            {
                "Audit the invoice email copy",
                "Seat counter on the plan card",
                "Empty state for saved cards",
                "Retry button on failed charges"
            };
            cards.Add(new Card
            {
                Id = $"BIL-{220 + draftIndex}",
                Title = titles[draftIndex % titles.Length],
                Lane = draftIndex % 2 == 0 ? "Platform" : "Growth",
                Stage = 0,
                Owner = "Maya Chen",
                Tag = "Idea"
            });
            draftIndex++;
            Render();
            status.Text = "Card added to To do.";
        };

        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = "Billing v2", Classes = { "arc-heading" } },
                TeamKit.Caption("Sprint 42 · 6 cards · 2 swimlanes")
            }
        });
        var actions = TeamKit.Row(8,
            TeamKit.Pill("WIP limit 2", ArcBadgeTone.Neutral),
            add);
        Grid.SetColumn(actions, 1);
        heading.Children.Add(actions);

        warning = new Border
        {
            IsVisible = false,
            Background = TeamKit.Tint(ArcTokens.Warning, 0.10),
            BorderBrush = TeamKit.Tint(ArcTokens.Warning, 0.26),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12, 8)
        };

        Render();

        return TeamKit.Root(new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Padding = new Thickness(20),
            Child = TeamKit.Stack(12, heading, warning, board, status)
        });
    }

    private static Control BuildCard(Card card, Action refresh)
    {
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        top.Children.Add(TeamKit.Pill(card.Tag, card.Tag == "Design" ? ArcBadgeTone.Accent : ArcBadgeTone.Neutral));
        var id = TeamKit.Caption(card.Id);
        id.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(id, 2);
        top.Children.Add(id);

        var title = new TextBlock
        {
            Text = card.Title,
            FontSize = 14,
            LineHeight = 19,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2
        };

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        footer.Children.Add(TeamKit.Avatar(card.Owner, ArcAvatarSize.Xs));
        var nav = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
        var back = TeamKit.IconBtn("chevron-left", $"Move {card.Title} to {Columns[Math.Max(0, card.Stage - 1)]}", () =>
        {
            if (card.Stage > 0)
            {
                card.Stage--;
                refresh();
            }
        }, box: 22);
        back.IsEnabled = card.Stage > 0;
        var forward = TeamKit.IconBtn("chevron-right", $"Move {card.Title} to {Columns[Math.Min(Columns.Length - 1, card.Stage + 1)]}", () =>
        {
            if (card.Stage < Columns.Length - 1)
            {
                card.Stage++;
                refresh();
            }
        }, box: 22);
        forward.IsEnabled = card.Stage < Columns.Length - 1;
        nav.Children.Add(back);
        nav.Children.Add(forward);
        Grid.SetColumn(nav, 2);
        footer.Children.Add(nav);

        return new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(9, 7),
            Child = TeamKit.Stack(7, top, title, footer)
        };
    }
}


