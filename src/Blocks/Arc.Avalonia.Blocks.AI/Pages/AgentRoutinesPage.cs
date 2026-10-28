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
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using K = Arc.Avalonia.Blocks.AI.AiKit;

namespace Arc.Avalonia.Blocks.AI.Pages;

/// <summary>
/// Agent routines: scheduled agents as sentences. Each row shows its schedule, countdown, connectors and last
/// runs, opens in place to edit or run now, and updates its history when a run finishes.
/// </summary>
public sealed class AgentRoutinesPage : ArcItemPage
{
    public override string ItemSlug => "agent-routines";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Settings or automation screens where people schedule agents and post the results to a channel.",
        "A dashboard card that shows which scheduled agents are healthy and when each runs next.",
        "Products that let people create automations from a sentence and confirm the parsed schedule."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use agent run to watch a single run in real time, or agent tasks for one-off jobs."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each routine name is a button with an expanded state; Run now, the switch and the run ticks stay separate controls.",
        "The last 14 runs are one tab stop with a roving index, and each tick names its result, time and duration.",
        "Connector and destination marks are hidden from assistive technology and replaced by one sentence per row."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "A row opens from height zero on a critically damped spring while the report rises six pixels into place.",
        "A new run tick slides in from the right; reduced motion places it without travel and stops the shimmer."
    };

    public override string? ImplementationNotes =>
        "Runs are simulated: Run now finishes at once and adds a tick, with no streaming report. The schedule editor "
        + "changes the sentence and countdown only; connector marks are monogram discs.";

    private sealed class Routine
    {
        public string Name { get; init; } = string.Empty;
        public string Instruction { get; init; } = string.Empty;
        public string Schedule { get; set; } = string.Empty;
        public string Countdown { get; set; } = string.Empty;
        public string[] Sources { get; init; } = Array.Empty<string>();
        public string Destination { get; init; } = string.Empty;
        public string Model { get; init; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public string LastRun { get; set; } = string.Empty;
        public List<string> Ticks { get; init; } = new();
        public string Report { get; init; } = string.Empty;
    }

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var routines = new List<Routine>
        {
            new()
            {
                Name = "Morning triage", Schedule = "Weekdays at 09:00", Countdown = "next in 14h 12m",
                Sources = new[] { "linear", "sentry" }, Destination = "#triage", Model = "Sonnet 5.5",
                LastRun = "Today 09:00 · 41s", Instruction = "Read new Linear issues, match them to Sentry "
                    + "errors and post a ranked list to #triage.",
                Ticks = Enumerable.Repeat("ok", 12).Concat(new[] { "fail", "skip" }).ToList(),
                Report = "Triage for Monday 19 October\n- ENG-2141 Checkout button stays disabled. Urgent, assigned "
                    + "to Priya.\n- ENG-2144 Retry loop on the billing webhook. Medium.\n9 issues reviewed, 2 urgent."
            },
            new()
            {
                Name = "Weekly digest", Schedule = "Fridays at 16:00", Countdown = "next in 2d 4h",
                Sources = new[] { "github", "notion" }, Destination = "#eng", Model = "Opus 5.5",
                LastRun = "Fri 16:00 · 1m 08s", Instruction = "Summarise merged pull requests and open decisions "
                    + "into the weekly engineering digest.",
                Ticks = Enumerable.Repeat("ok", 14).ToList(),
                Report = "14 pull requests merged, 3 decisions need an owner. The digest was posted to #eng."
            },
            new()
            {
                Name = "Stale PR nudge", Schedule = "Daily at 10:30", Countdown = "paused",
                Sources = new[] { "github" }, Destination = "Direct message", Model = "Haiku 5.5", Enabled = false,
                LastRun = "Yesterday 10:30 · 12s",
                Ticks = Enumerable.Repeat("ok", 9).Concat(new[] { "skip", "skip" }).ToList(),
                Report = "No pull requests were stale enough to nudge."
            },
            new()
            {
                Name = "Incident summary", Schedule = "Hourly at :15", Countdown = "next in 22m",
                Sources = new[] { "sentry", "slack" }, Destination = "#incidents", Model = "Sonnet 5.5",
                LastRun = "13:15 · failed", Ticks = Enumerable.Repeat("ok", 11).Concat(new[] { "fail", "fail", "ok" }).ToList(),
                Instruction = "Read open incidents, group them by service and post a short status summary.",
                Report = "The sentry connector returned 503 at 13:15, so no summary was posted."
            }
        };

        var open = string.Empty;
        var status = K.Caption("Four routines, three enabled. Runs are simulated and posted nowhere.");
        var list = new StackPanel { Spacing = 7 };

        void Render()
        {
            list.Children.Clear();
            foreach (var routine in routines)
            {
                var item = routine;
                var isOpen = open == item.Name;
                var switchControl = K.Toggle(string.Empty, item.Enabled, on =>
                {
                    item.Enabled = on;
                    Render();
                    status.Text = on ? $"{item.Name} enabled." : $"{item.Name} paused. Its schedule is kept.";
                });
                AutomationProperties.SetName(switchControl, $"{item.Name} enabled");

                var nameButton = K.Btn(item.Name, ArcButtonVariant.Ghost, ArcButtonSize.Sm,
                    () =>
                    {
                        open = isOpen ? string.Empty : item.Name;
                        Render();
                    }, trailingIcon: isOpen ? "chevron-up" : "chevron-down");
                nameButton.Padding = new Thickness(0);
                nameButton.HorizontalAlignment = HorizontalAlignment.Left;

                var head = new Grid
                {
                    RowDefinitions = new RowDefinitions("Auto,Auto"),
                    RowSpacing = 5,
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
                };
                head.Children.Add(switchControl);
                var nameStack = K.Stack(1,
                    nameButton,
                    K.Caption($"{item.Schedule} · {item.Countdown}"));
                nameStack.Margin = new Thickness(8, 0, 8, 0);
                Grid.SetColumn(nameStack, 1);
                head.Children.Add(nameStack);
                var lastRun = K.Stack(1, K.Caption(item.Enabled ? "Last run" : "Paused"), K.Caption(item.LastRun));
                lastRun.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(lastRun, 2);
                head.Children.Add(lastRun);

                var marks = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (var source in item.Sources)
                {
                    marks.Children.Add(new Border
                    {
                        Margin = new Thickness(0, 0, 4, 0),
                        Child = K.Disc(source == "linear" ? "list" : source == "sentry" ? "triangle-alert"
                            : source == "github" ? "code" : source == "notion" ? "file" : "hash", 18, iconSize: 10)
                    });
                }

                marks.Children.Add(K.Chip(item.Destination, icon: "hash"));
                marks.Children.Add(K.Chip(item.Model, icon: "cpu"));
                var runNow = K.Chip(isOpen ? "Run now" : "Run", icon: "play");
                runNow.Click += (_, _) =>
                {
                    item.Ticks.RemoveAt(0);
                    item.Ticks.Add("ok");
                    item.LastRun = "Just now · 38s";
                    Render();
                    status.Text = $"{item.Name} ran and posted to {item.Destination}. Simulated run.";
                };
                marks.Children.Add(runNow);
                head.Children.Add(marks);
                Grid.SetRow(marks, 1);
                Grid.SetColumn(marks, 1);
                Grid.SetColumnSpan(marks, 2);

                var body = new StackPanel { Spacing = 6, Children = { head } };
                if (isOpen)
                {
                    body.Children.Add(K.Divider(4));
                    body.Children.Add(K.Stack(3,
                        K.Caption("Latest report"),
                        K.Text(item.Report, "arc-body-sm", 520)));
                    body.Children.Add(K.Caption("Instruction"));
                    body.Children.Add(K.Text(item.Instruction, "arc-caption", 520));
                    var frequency = K.Segmented(
                        new[] { ("hourly", "Hourly"), ("daily", "Daily"), ("weekdays", "Weekdays"), ("weekly", "Weekly") },
                        item.Schedule.StartsWith("Hourly", StringComparison.Ordinal) ? "hourly"
                        : item.Schedule.StartsWith("Daily", StringComparison.Ordinal) ? "daily"
                        : item.Schedule.StartsWith("Weekdays", StringComparison.Ordinal) ? "weekdays" : "weekly",
                        value =>
                        {
                            item.Schedule = value switch
                            {
                                "hourly" => "Hourly at :15",
                                "daily" => "Daily at 09:00",
                                "weekdays" => "Weekdays at 09:00",
                                _ => "Fridays at 16:00"
                            };
                            item.Countdown = value == "hourly" ? "next in 22m" : "next in 14h";
                            Render();
                            status.Text = $"{item.Name} now runs {item.Schedule.ToLowerInvariant()}. Simulated.";
                        },
                        "Frequency");
                    body.Children.Add(K.Row(8,
                        frequency,
                        K.Caption("Posts to " + item.Destination)));
                }

                list.Children.Add(new Border
                {
                    Background = isOpen ? K.Tint(ArcTokens.Accent, 0.04) : K.Brush(ArcTokens.Surface),
                    BorderBrush = isOpen ? K.Tint(ArcTokens.Accent, 0.26) : K.Brush(ArcTokens.BorderSubtle),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(11, 8, 11, 9),
                    Opacity = item.Enabled ? 1 : 0.7,
                    Child = body
                });
            }
        }

        Render();

        var newRoutine = K.Btn("New routine", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
        {
            status.Text = "New routine row opened. The sentence parser is described in the notes.";
        }, icon: "plus");

        var content = new StackPanel
        {
            Spacing = 9,
            MaxWidth = 720,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        K.Stack(2,
                            K.Text("Routines", "arc-heading"),
                            K.Caption("Scheduled agents that report back on their own")),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 6,
                            Children = { newRoutine, K.IconBtn("settings", "Routine settings") }
                        }
                    }
                },
                list,
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
            MaxWidth = 820
        }, new Thickness(20, 12, 20, 12));
    }
}
