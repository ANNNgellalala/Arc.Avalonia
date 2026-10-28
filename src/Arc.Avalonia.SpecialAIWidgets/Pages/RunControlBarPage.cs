using Avalonia.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialAIWidgets.Controls;

namespace Arc.Avalonia.SpecialAIWidgets.Pages;

/// <summary>
/// Gallery page for the run control bar. The state buttons move the bar through running, paused and taken
/// over, and every action button reports its intent in the line underneath instead of changing the run.
/// </summary>
public sealed class RunControlBarPage : ArcPage
{
    public override string Title => "Run control bar";

    public override string Group => "P0 · Agent UI";

    public override string? Description =>
        "A strip that turns a running agent into a controllable run: pause, finish the current step, stop now, steer with a new instruction or take over.";

    public override int Order => 20002;

    public override Control Build(ArcDemoContext context)
    {
        var bar = new ArcRunControlBar();
        var log = DemoUi.Caption("Intent events appear here; the host owns the actual run state.");

        bar.StopRequested += (_, _) =>
        {
            bar.Status = ArcRunBarStatus.Stopped;
            log.Text = "StopRequested · the host abandons the current capability call and writes the audit entry.";
        };
        bar.StopAfterStepRequested += (_, _) =>
            log.Text = "StopAfterStepRequested · the host finishes the current step, then stops at a safe point.";
        bar.PauseRequested += (_, _) =>
        {
            bar.Status = ArcRunBarStatus.Paused;
            log.Text = "PauseRequested · the host pauses after the current capability call returns.";
        };
        bar.ResumeRequested += (_, _) =>
        {
            bar.Status = ArcRunBarStatus.Running;
            log.Text = "ResumeRequested · the host resumes the plan where it paused.";
        };
        bar.SteerRequested += (_, text) =>
            log.Text = $"SteerRequested · \"{text}\" enters the transcript as the newest instruction.";
        bar.TakeoverRequested += (_, _) =>
        {
            bar.Status = ArcRunBarStatus.TakenOver;
            log.Text = "TakeoverRequested · the host hands the next action to the person.";
        };

        void Apply(string state)
        {
            switch (state)
            {
                case "paused":
                    bar.Status = ArcRunBarStatus.Paused;
                    bar.CurrentStep = "Waiting to resume the limiter tests";
                    bar.StepLabel = "Step 3 of 5";
                    bar.ElapsedSeconds = 96;
                    log.Text = "Status · Paused · the host can resume or stop the run at a safe point.";
                    break;
                case "taken":
                    bar.Status = ArcRunBarStatus.TakenOver;
                    bar.CurrentStep = "You are editing src/middleware/limits.ts";
                    bar.StepLabel = "Step 3 of 5";
                    bar.ElapsedSeconds = 112;
                    log.Text = "Status · TakenOver · the agent waits until the run is handed back.";
                    break;
                default:
                    bar.Status = ArcRunBarStatus.Running;
                    bar.CurrentStep = "Editing src/middleware/limits.ts";
                    bar.StepLabel = "Step 3 of 5";
                    bar.ElapsedSeconds = 72;
                    log.Text = "Status · Running · Stop now asks for a second click before it raises StopRequested.";
                    break;
            }
        }

        var buttons = DemoUi.Row(
            StateButton("Running", "running", Apply),
            StateButton("Paused", "paused", Apply),
            StateButton("Taken over", "taken", Apply));

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                buttons,
                bar,
                log
            }
        };

        Apply("running");

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Run control bar",
                    "The control renders the run state it is given. Pause, resume, stop, steer and take over all raise intents, so the same bar can sit above a transcript, in a side panel or in a window footer.",
                    content)
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
        ArcUi.Name(button, $"{label} run state");
        button.Click += (_, _) => apply(state);
        return button;
    }
}
