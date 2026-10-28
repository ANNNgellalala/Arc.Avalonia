using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialAIWidgets.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialAIWidgets.Pages;

/// <summary>
/// Gallery page for the background run dock. The state buttons move one dock through running, needs attention
/// and paused, a second section shows the idle dock sitting in a window footer, and every action reports its
/// intent underneath.
/// </summary>
public sealed class BackgroundRunDockPage : ArcPage
{
    public override string Title => "Background run dock";

    public override string Group => "P0 · Agent UI";

    public override string? Description =>
        "A compact dock that says the agent is still working in the background: state, task, progress and the pause, open and stop intents. The tray entry and system notification stay with the host.";

    public override int Order => 20006;

    public override Control Build(ArcDemoContext context)
    {
        var dock = new ArcBackgroundRunDock { Width = 580, HorizontalAlignment = HorizontalAlignment.Left };
        var log = DemoUi.Caption("Intent events appear here; the host owns the background run.");

        dock.PauseRequested += (_, _) =>
        {
            dock.Status = dock.Status == ArcDockStatus.Paused ? ArcDockStatus.Running : ArcDockStatus.Paused;
            log.Text = dock.Status == ArcDockStatus.Paused
                ? "PauseRequested · the host pauses the background run."
                : "PauseRequested · the host resumes the background run.";
        };
        dock.StopRequested += (_, _) =>
        {
            dock.Status = ArcDockStatus.Idle;
            log.Text = "StopRequested · the host cancels the background run and keeps the partial result.";
        };
        dock.ExpandRequested += (_, _) =>
            log.Text = "ExpandRequested · the host brings the run transcript to the front.";

        void Apply(string state)
        {
            switch (state)
            {
                case "attention":
                    dock.Status = ArcDockStatus.NeedsAttention;
                    dock.TaskLabel = "Weekly meal plan needs a grocery choice";
                    dock.Progress = 0.68;
                    log.Text = "NeedsAttention · the host may also show a tray badge or a system notification.";
                    break;
                case "paused":
                    dock.Status = ArcDockStatus.Paused;
                    dock.TaskLabel = "Inbox triage is paused";
                    dock.Progress = 0.31;
                    log.Text = "Paused · nothing runs until the person resumes the background run.";
                    break;
                default:
                    dock.Status = ArcDockStatus.Running;
                    dock.TaskLabel = "Morning brief is running";
                    dock.Progress = 0.42;
                    log.Text = "Running · the dock stays quiet and the progress line reports the host's value.";
                    break;
            }
        }

        var buttons = DemoUi.Row(
            StateButton("Running", "running", Apply),
            StateButton("Needs attention", "attention", Apply),
            StateButton("Paused", "paused", Apply));

        var interactive = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                buttons,
                dock,
                log
            }
        };

        var idleDock = new ArcBackgroundRunDock
        {
            Status = ArcDockStatus.Idle,
            TaskLabel = "Nothing queued",
            Progress = 0,
            Width = 520,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var footer = new Border
        {
            Background = ArcUi.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusPanel),
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    ArcUi.HStack(
                        8,
                        ArcUi.Icon("calendar", 16, ArcTokens.TextSecondary),
                        ArcUi.Text("Today", "arc-body-sm arc-medium"),
                        ArcUi.Caption("Thursday · 4 events · 3 reminders")),
                    ArcUi.Divider(),
                    idleDock
                }
            }
        };

        Apply("running");

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Background run dock",
                    "Reuse the same dock in a window footer, a sidebar footer or a floating corner. It renders the host's state and progress and never runs anything itself.",
                    interactive),
                DemoUi.Section(
                    "Where it sits",
                    "A quiet idle dock in an app footer; hide it entirely when the host has no background runs.",
                    footer)
            }
        };
    }

    private static Control StateButton(string label, string state, System.Action<string> apply)
    {
        var button = new ArcButton
        {
            Content = label,
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm
        };
        ArcUi.Name(button, $"{label} dock state");
        button.Click += (_, _) => apply(state);
        return button;
    }
}
