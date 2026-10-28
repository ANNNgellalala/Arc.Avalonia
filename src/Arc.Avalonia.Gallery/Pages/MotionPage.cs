using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Gallery.Pages;

/// <summary>The motion presets and the rules that keep animation meaningful.</summary>
public sealed class MotionPage : ArcPage
{
    public override string Title => "Motion";

    public override string Group => "Overview";

    public override string? Description =>
        "Motion shows cause and effect. State changes land without overshoot; bounce is reserved for playful moments, and every animation has a reduced-motion branch.";

    public override int Order => 4;

    public override Control Build(ArcDemoContext context)
    {
        var stack = new StackPanel();

        stack.Children.Add(DemoUi.Section(
            "Springs",
            "Anything that reports state lands without overshoot. Press feedback gets the faintest bounce, shared highlights glide on a slightly looser spring.",
            new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    MotionRow("spring.smooth", "0.40s, bounce 0", "panels, height, drawers, progress, layout shifts"),
                    MotionRow("spring.snappy", "0.26s, bounce 0.12", "press feedback, small indicators, icon swaps"),
                    MotionRow("spring.morph", "0.42s, bounce 0.16", "shared highlights, width that follows content")
                }
            }));

        stack.Children.Add(DemoUi.Section(
            "Tweens",
            "Colour, opacity and small fades use the tween scale. Exits are faster than entrances so dismissal feels immediate.",
            new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    MotionRow("duration.instant", "120ms", "small state tweaks, toggles"),
                    MotionRow("duration.fast", "160ms", "hover fills, colour changes"),
                    MotionRow("duration.exit", "180ms", "leaving surfaces"),
                    MotionRow("duration.standard", "240ms", "opacity crossfades"),
                    MotionRow("duration.considered", "480ms", "one orchestrated entrance per page")
                }
            }));

        stack.Children.Add(DemoUi.Section(
            "Rules",
            "The rules that decide whether an animation belongs at all.",
            new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    Rule("One continuous gesture per interaction: a state change is one spatial movement."),
                    Rule("Everything reversible uses a spring, so reversing mid way never jumps."),
                    Rule("Nothing resizes in one frame; animate the size or reserve the space."),
                    Rule("Numbers count with tabular figures and a stable width."),
                    Rule("Presses scale to about 0.97, except elements that anchor a popup."),
                    Rule("Exits are faster than entrances."),
                    Rule("Reduced motion keeps the final state and feedback, and removes travel.")
                }
            }));

        return stack;
    }

    private static Control MotionRow(string name, string timing, string use) => new StackPanel
    {
        Children =
        {
            new TextBlock { Text = name, Classes = { "arc-label" } },
            new TextBlock { Text = $"{timing} · {use}", Classes = { "arc-caption" } }
        }
    };

    private static Control Rule(string text) => new TextBlock
    {
        Text = text,
        Classes = { "arc-body-sm", "arc-secondary" },
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 720,
        HorizontalAlignment = HorizontalAlignment.Left
    };
}

