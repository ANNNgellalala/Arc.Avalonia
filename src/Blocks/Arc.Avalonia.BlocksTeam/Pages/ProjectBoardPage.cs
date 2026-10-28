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
/// Project board: a four stage workflow with per-column counts, a sprint readiness bar and explicit move
/// buttons, so a task can be moved without a drag gesture. All work and My work filter the same in-memory
/// tasks; Add task appends a sample card to Planned.
/// </summary>
public sealed class ProjectBoardPage : ArcItemPage
{
    private sealed class BoardTask
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public int Stage { get; set; }
    }

    private static readonly string[] Stages = { "Planned", "In progress", "Review", "Done" };

    public override string ItemSlug => "project-board";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A compact kanban with a handful of tasks across fixed stages.",
        "Boards where moving tasks should be accessible through explicit buttons, not drag only."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use reorderable-list when order inside one list matters more than stages, or add drag for large boards."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Every card has buttons labelled \"Move <task> to <stage>\"; each column is a labelled section with its count.",
        "Moves are announced in a live status region, and the filter buttons expose a pressed state."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Cards travel between columns on a gentle spring; the progress bar grows with scaleX rather than width.",
        "Reduced motion drops the travel and the bar animates in place."
    };

    public override string? ImplementationNotes =>
        "Tasks, owners and the sprint window are sample data in memory; there is no drag gesture and no API. " +
        "Cards move with explicit arrow buttons, which is the documented accessible path; Add task cycles through " +
        "a small list of draft titles. No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var tasks = new List<BoardTask>
        {
            new() { Id = "NW-142", Title = "Empty states for the new board columns", Tag = "Design", Owner = "Maya Chen", Stage = 0 },
            new() { Id = "NW-138", Title = "Persist column order when a filter is active", Tag = "Frontend", Owner = "Sam Okafor", Stage = 1 },
            new() { Id = "NW-131", Title = "Permissions review before the beta", Tag = "Platform", Owner = "Priya Raman", Stage = 1 },
            new() { Id = "NW-129", Title = "Release notes and support macros", Tag = "Ops", Owner = "Lena Fischer", Stage = 2 },
            new() { Id = "NW-126", Title = "Week calendar swipe gestures", Tag = "Frontend", Owner = "Jonas Weber", Stage = 2 },
            new() { Id = "NW-121", Title = "Directory search ranking", Tag = "Platform", Owner = "Priya Raman", Stage = 1 },
            new() { Id = "NW-118", Title = "Onboarding illustration pass", Tag = "Design", Owner = "Maya Chen", Stage = 3 },
            new() { Id = "NW-112", Title = "Invite parser edge cases", Tag = "Frontend", Owner = "Sam Okafor", Stage = 3 }
        };

        var drafts = new[]
        {
            "Tighten the board empty state",
            "Add a WIP limit warning",
            "Review keyboard order in the drawer",
            "Write the migration note"
        };
        var draftIndex = 0;
        var mineOnly = false;
        const string me = "Maya Chen";

        var progress = TeamKit.Meter(0, 7);
        var progressLabel = TeamKit.Caption(string.Empty);
        var board = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
        var status = TeamKit.Caption("Sprint 24 · ends in 6 days");

        void Render()
        {
            var visible = tasks.Where(task => !mineOnly || task.Owner == me).ToList();
            var done = visible.Count(task => task.Stage == 3);
            var total = Math.Max(1, visible.Count);
            progress.Fraction = (double)done / total;
            progressLabel.Text = $"{done} of {visible.Count} done · {Math.Round((double)done / total * 100)}%";

            board.Children.Clear();
            for (var stage = 0; stage < Stages.Length; stage++)
            {
                var columnTasks = visible.Where(task => task.Stage == stage).ToList();
                var cards = new StackPanel { Spacing = 6 };
                foreach (var task in columnTasks)
                {
                    cards.Children.Add(BuildCard(task, tasks, Render));
                }

                if (columnTasks.Count == 0)
                {
                    cards.Children.Add(new Border
                    {
                        Padding = new Thickness(10, 14),
                        Background = TeamKit.Brush(ArcTokens.Surface),
                        BorderBrush = TeamKit.Brush(ArcTokens.BorderSubtle),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(12),
                        Child = TeamKit.Caption("Nothing here yet")
                    });
                }

                var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                header.Children.Add(new TextBlock { Text = Stages[stage], Classes = { "arc-label" } });
                var badge = TeamKit.Pill(columnTasks.Count.ToString(),
                    stage == 3 && columnTasks.Count > 0 ? ArcBadgeTone.Success : ArcBadgeTone.Neutral);
                Grid.SetColumn(badge, 1);
                header.Children.Add(badge);

                var column = new Border
                {
                    Background = TeamKit.Brush(ArcTokens.SurfaceMuted),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(10),
                    Margin = new Thickness(stage == 0 ? 0 : 6, 0, stage == Stages.Length - 1 ? 0 : 6, 0),
                    Child = new StackPanel
                    {
                        Spacing = 10,
                        Children =
                        {
                            header,
                            cards
                        }
                    }
                };
                Grid.SetColumn(column, stage);
                board.Children.Add(column);
            }

            status.Text = mineOnly
                ? $"Showing Maya's work · {visible.Count} of {tasks.Count} tasks"
                : $"Sprint 24 · ends in 6 days · {tasks.Count} tasks";
        }

        var filter = TeamKit.Segmented(new[] { ("all", "All work"), ("mine", "My work") }, "all", value =>
        {
            mineOnly = value == "mine";
            Render();
        }, "Filter");

        var add = TeamKit.Btn("Add task", ArcButtonVariant.Secondary, ArcButtonSize.Sm, icon: "plus");
        add.Click += (_, _) =>
        {
            tasks.Add(new BoardTask
            {
                Id = $"NW-{150 + draftIndex}",
                Title = drafts[draftIndex % drafts.Length],
                Tag = "Idea",
                Owner = me,
                Stage = 0
            });
            draftIndex++;
            Render();
            status.Text = "Draft task added to Planned.";
        };

        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = "Sprint 24", Classes = { "arc-heading" } },
                TeamKit.Caption("Northwind Studio · Nov 3 – Nov 14")
            }
        });
        var actions = TeamKit.Row(8, filter, add);
        Grid.SetColumn(actions, 1);
        heading.Children.Add(actions);

        var progressRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        progressRow.Children.Add(TeamKit.Caption("Progress"));
        var meter = new Border { Margin = new Thickness(12, 0), Child = progress, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(meter, 1);
        progressRow.Children.Add(meter);
        Grid.SetColumn(progressLabel, 2);
        progressRow.Children.Add(progressLabel);

        Render();

        return TeamKit.Root(new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Padding = new Thickness(20),
            Child = TeamKit.Stack(14,
                heading,
                progressRow,
                TeamKit.Divider(),
                board,
                status)
        });
    }

    private static Control BuildCard(BoardTask task, List<BoardTask> tasks, Action refresh)
    {
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        top.Children.Add(TeamKit.Pill(task.Tag, task.Tag == "Design" ? ArcBadgeTone.Accent : ArcBadgeTone.Neutral));
        var avatar = TeamKit.Avatar(task.Owner, ArcAvatarSize.Xs);
        Grid.SetColumn(avatar, 1);
        top.Children.Add(avatar);

        var title = new TextBlock
        {
            Text = task.Title,
            FontSize = 14,
            LineHeight = 20,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2
        };

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        footer.Children.Add(TeamKit.Caption(task.Id));
        var arrows = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        var left = TeamKit.IconBtn("chevron-left", $"Move {task.Title} to {Stages[Math.Max(0, task.Stage - 1)]}", () =>
        {
            if (task.Stage > 0)
            {
                task.Stage--;
                refresh();
            }
        }, box: 24);
        left.IsEnabled = task.Stage > 0;
        var right = TeamKit.IconBtn("chevron-right", $"Move {task.Title} to {Stages[Math.Min(Stages.Length - 1, task.Stage + 1)]}", () =>
        {
            if (task.Stage < Stages.Length - 1)
            {
                task.Stage++;
                refresh();
            }
        }, box: 24);
        right.IsEnabled = task.Stage < Stages.Length - 1;
        arrows.Children.Add(left);
        arrows.Children.Add(right);
        Grid.SetColumn(arrows, 1);
        footer.Children.Add(arrows);

        return new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(10, 8),
            Child = TeamKit.Stack(8, top, title, footer)
        };
    }
}

