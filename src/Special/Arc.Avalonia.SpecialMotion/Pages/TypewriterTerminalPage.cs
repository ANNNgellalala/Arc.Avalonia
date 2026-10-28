using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialMotion.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMotion.Pages;

/// <summary>Typewriter terminal: a scripted CLI session with a seeded cadence and playback controls.</summary>
public sealed class TypewriterTerminalPage : ArcItemPage
{
    public override string ItemSlug => "typewriter-terminal";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A landing page hero or install section for a developer tool, SDK or CLI.",
        "Docs quick starts that show what each command prints before people run it.",
        "Changelog or launch posts that show a new command in action."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Commands people need to read and copy without waiting; use a code block.",
        "Real, live output from a running process; use a log stream.",
        "Showing how a file changes between versions; use a code diff."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The scrubber is a slider with its value in seconds; Left and Right jump between commands, Home and End go to the ends.",
        "Space or K plays and pauses, R replays; play, pause and replay are real controls with specific names.",
        "Status never relies on colour alone: success lines carry a tick, tasks resolve to a tick with a timing, progress shows a percentage.",
        "The animated screen is decorative; the transcript is exposed through the control name and help text."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Typing follows a seeded human cadence: a short think before the first key, bursts inside words, stalls at spaces.",
        "Each output line rises and fades in; progress fills quickly, stalls around the middle, then finishes steadily.",
        "Reduced motion shows the finished session at once, stops the caret blink, and still lets the scrubber jump between steps."
    };

    public override string? ImplementationNotes =>
        "Platform approximations: the demo draws from one session clock instead of writing to the DOM, so the " +
        "screen follows the newest line without per-frame layout. The transcript is the control's accessible name " +
        "and help text rather than a separate hidden list. Everything shown is a simulation, not a live shell.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var terminal = new ArcTypewriterTerminal();
        var stage = SmPage.Stage(terminal, height: 340, background: ArcTokens.SurfaceMuted);

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                stage,
                SmPage.Hint(
                    "A scripted install session: play, pause, replay or drag the scrubber. The cadence and every output line are seeded.")
            }
        };
    }
}
