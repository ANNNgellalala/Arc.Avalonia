using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialMotion.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMotion.Pages;

/// <summary>Keycap keyboard: a sculpted mechanical board that sinks and lights under real typing.</summary>
public sealed class KeycapKeyboardPage : ArcItemPage
{
    public override string ItemSlug => "keycap-keyboard";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Landing pages for keyboard-first products and developer tools.",
        "Onboarding that teaches shortcuts by lighting the keys they use.",
        "A playful empty state or a launch page easter egg."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Form input; use a real text field.",
        "On-screen keyboards for accessibility needs; use the platform's."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Every cap is a hit target named by its character or function, such as Space, Delete, Return or Shift.",
        "Typing presses the matching cap; Shift, Tab and Caps light their caps without typing a character.",
        "Arrow keys move between caps and Enter or Space presses the focused one; Tab still leaves the board.",    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "A cap drops most of its travel on key down and returns in 200ms with a small lift past rest.",
        "A faint light blooms on the board under the cap and fades over 700ms, so fast typing leaves a trail.",
        "Reduced motion removes cap travel and caret blinking; the light and legend colour still respond."
    };

    public override string? ImplementationNotes =>
        "Platform approximations: legends are drawn with the Inter face on a rounded cap rather than a sculpted " +
        "doubleshot key, and the cap's profile is two rounded rectangles instead of a mesh. The glow is an alpha " +
        "bloom, and the seeded screenshot state shows the sample text with the last three keys still lit. The board is one focus stop with " +
        "an internal cap index.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var board = new ArcKeycapKeyboard
        {
            Text = "hello, arc"
        };

        var clear = SmPage.Toggle("Clear", () => board.Text = string.Empty);
        var sample = SmPage.Toggle("Sample", () => board.Text = "hello, arc");

        var stage = SmPage.Stage(board, height: 380, background: ArcTokens.SurfaceMuted);

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                stage,
                SmPage.Hint(
                    "Click the board, then type: each cap sinks and lights, and the line above grows.",
                    clear,
                    sample)
            }
        };
    }
}
