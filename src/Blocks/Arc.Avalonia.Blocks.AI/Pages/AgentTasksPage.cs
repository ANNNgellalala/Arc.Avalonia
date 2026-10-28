using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using K = Arc.Avalonia.Blocks.AI.AiKit;

namespace Arc.Avalonia.Blocks.AI.Pages;

/// <summary>
/// Agent tasks: background coding work on a repository and branch. Starting a task adds a running card, the
/// filters split the queue, and a finished task can open a pull request.
/// </summary>
public sealed class AgentTasksPage : ArcItemPage
{
    public override string ItemSlug => "agent-tasks";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A surface where people hand coding tasks to background agents and come back to review the result.",
        "Dashboards for automated maintenance runs where each run ends in a branch with a diff and checks.",
        "Any queue of long running agent jobs where the current step and a clear finished state matter."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use agent run to follow one live run in detail, or agent routines for scheduled recurring jobs."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each card title is a button with an expanded state, and closed details leave the tab order.",
        "State always pairs an icon with a word: Running, Ready for review, Checks failed or Merged.",
        "The elapsed time has a spoken label, so a compact value is never the only form."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "A started task grows in at the top with one accent tint flash; the live step carries a calm shimmer.",
        "Reduced motion drops the flash and the sweep, keeps every state and moves focus to the new card."
    };

    public override string? ImplementationNotes =>
        "Tasks, diffs and checks are sample data; Start inserts a queued sample task and Create pull request "
        + "invents a number. Repository, branch and model are chips rather than menus.";

    private sealed record Task(string Title, string Repo, string Branch, string Model, string State,
        string Step, int Progress, string Meta, bool CanOpen);

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var tasks = new List<Task>
        {
            new("Fix the flaky checkout test in CI", "arc-ui/web", "fix/flaky-checkout-test", "Opus 5.5",
                "Running", "Running tests, 8 of 12", 64, "Started 2m ago", false),
            new("Migrate the billing table to the new schema", "arc-ui/api", "chore/billing-schema", "Sonnet 5.5",
                "Ready", "Ready for review", 100, "+42 −18 · 6 files · 12 checks passed", true),
            new("Bump the PDF renderer and fix imports", "arc-ui/docs", "deps/pdf-renderer", "Haiku 5.5",
                "Merged", "Merged as PR #471", 100, "Merged 1h ago · 3 files", false)
        };
        var filter = "All";
        var status = K.Caption("4 tasks: 1 running, 1 ready for review, 1 merged. Runs are simulated.");

        var list = new StackPanel { Spacing = 6 };
        var filters = new WrapPanel { Orientation = Orientation.Horizontal };

        void RenderFilters()
        {
            filters.Children.Clear();
            foreach (var (label, match) in new[]
                     {
                         ("All", "All"), ("Running", "Running"), ("Ready for review", "Ready"), ("Merged", "Merged")
                     })
            {
                var value = match;
                var count = value == "All" ? tasks.Count : tasks.Count(task => task.State == value);
                var chip = K.Chip($"{label} {count}", selected: filter == value);
                chip.Click += (_, _) =>
                {
                    filter = value;
                    RenderFilters();
                    RenderList();
                    status.Text = value == "All" ? "Showing every task." : $"Showing {label.ToLowerInvariant()} tasks.";
                };
                chip.Margin = new Thickness(0, 0, 5, 4);
                filters.Children.Add(chip);
            }
        }

        void RenderList()
        {
            list.Children.Clear();
            foreach (var task in tasks.Where(task => filter == "All" || task.State == filter))
            {
                var stateToken = task.State switch
                {
                    "Running" => ArcTokens.Series1,
                    "Ready" => ArcTokens.Success,
                    _ => ArcTokens.TextMuted
                };
                var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                head.Children.Add(K.Text(task.Title, "arc-body arc-medium", 520));
                var badge = K.Pill(task.State == "Ready" ? "Ready for review" : task.State, task.State switch
                {
                    "Running" => ArcBadgeTone.Accent,
                    "Ready" => ArcBadgeTone.Success,
                    _ => ArcBadgeTone.Neutral
                }, dot: task.State == "Running");
                Grid.SetColumn(badge, 1);
                head.Children.Add(badge);

                var meta = K.Row(6,
                    K.Caption(task.Repo),
                    K.Dot(ArcTokens.BorderStrong, 3),
                    K.Mono(task.Branch, 11),
                    K.Dot(ArcTokens.BorderStrong, 3),
                    K.Caption(task.Model));

                var body = new StackPanel { Spacing = 7, Children = { head, meta } };
                if (task.State == "Running")
                {
                    var cancel = K.Chip("Cancel", icon: "x");
                    cancel.Click += (_, _) =>
                    {
                        tasks[tasks.IndexOf(task)] = task with { State = "Cancelled", Step = "Cancelled", Progress = 0 };
                        RenderFilters();
                        RenderList();
                        status.Text = "Run cancelled. The branch is kept so it can be retried.";
                    };
                    var stepRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                    stepRow.Children.Add(K.Row(7,
                        new ArcIcon { Icon = "loader-circle", Size = 13, Foreground = K.Brush(ArcTokens.Accent) },
                        K.Text(task.Step, "arc-body-sm"),
                        K.Caption($"{task.Progress}%")));
                    Grid.SetColumn(cancel, 1);
                    stepRow.Children.Add(cancel);
                    body.Children.Add(stepRow);
                    body.Children.Add(K.Bar(task.Progress, 100, ArcProgressSize.Sm));
                }
                else if (task.State == "Ready")
                {
                    var open = K.Chip("Create pull request", icon: "arrow-up-right");
                    open.Click += (_, _) =>
                    {
                        tasks[tasks.IndexOf(task)] = task with { State = "Merged", Step = "PR #482 open" };
                        RenderFilters();
                        RenderList();
                        status.Text = "Pull request #482 opened from the result. Simulated.";
                    };
                    var readyRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                    readyRow.Children.Add(K.Row(6,
                        new ArcIcon { Icon = "circle-check", Size = 13, Foreground = K.Brush(ArcTokens.Success) },
                        K.Caption(task.Meta)));
                    Grid.SetColumn(open, 1);
                    readyRow.Children.Add(open);
                    body.Children.Add(readyRow);
                }
                else
                {
                    body.Children.Add(K.Row(6,
                        K.Caption(task.Step),
                        K.Dot(ArcTokens.BorderStrong, 3),
                        K.Caption(task.Meta)));
                }

                list.Children.Add(new Border
                {
                    Background = K.Brush(ArcTokens.Surface),
                    BorderBrush = K.Brush(ArcTokens.BorderSubtle),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(10, 7, 10, 7),
                    Child = body
                });
            }
        }

        var prompt = new ArcInput { Placeholder = "Describe a task for a background agent", Height = 40 };
        AutomationProperties.SetName(prompt, "Task");
        var start = K.Btn("Start", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
        {
            var text = (prompt.Value ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                status.Text = "Describe the task first.";
                return;
            }

            tasks.Insert(0, new Task(text, "arc-ui/web", "agent/rate-limit-middleware", "Opus 5.5",
                "Running", "Reading src/api/rate-limit.ts", 12, "Started just now", false));
            prompt.Value = string.Empty;
            filter = "All";
            RenderFilters();
            RenderList();
            status.Text = "Task started on arc-ui/web. The run is simulated.";
        }, icon: "play");

        var chipRow = K.RowWrap(6,
            K.Chip("arc-ui/web", icon: "folder"),
            K.Chip("main ← agent/task", icon: "arrow-down-up"),
            K.Chip("Opus 5.5", icon: "cpu"));

        var composer = new Border
        {
            Background = K.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(10, 8, 10, 8),
            Child = K.Stack(6, new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children = { prompt, start }
            }, chipRow)
        };
        if (composer.Child is StackPanel composerStack && composerStack.Children[0] is Grid promptRow)
        {
            Grid.SetColumn(promptRow.Children[1], 1);
            promptRow.Children[1].Margin = new Thickness(6, 0, 0, 0);
        }

        RenderFilters();
        RenderList();

        var content = new StackPanel
        {
            Spacing = 9,
            MaxWidth = 780,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        K.Stack(2,
                            K.Text("Background tasks", "arc-heading"),
                            K.Caption("Coding agents work on a branch and come back for review")),
                        K.Row(4, K.IconBtn("refresh-cw", "Refresh tasks"), K.IconBtn("settings", "Task settings"))
                    }
                },
                composer,
                filters,
                K.Scroller(list, 318),
                status
            }
        };

        if (content.Children[0] is Grid head)
        {
            Grid.SetColumn(head.Children[1], 1);
        }

        return K.Surface(new Border
        {
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 880
        }, new Thickness(20, 12, 20, 12));
    }
}
