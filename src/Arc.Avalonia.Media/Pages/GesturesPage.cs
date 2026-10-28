using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Media.Pages;

/// <summary>Deliberate, reversible gestures: a hold, a swipe and a reaction.</summary>
public sealed class GesturesPage : ArcPage
{
    public override string Title => "Gestures";

    public override string Group => "Media";

    public override string? Description =>
        "Gestures that replace a confirmation dialog: hold for a destructive action, swipe a triage row, and acknowledge with one tap.";

    public override int Order => 1001;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Hold to confirm",
                "Use for a destructive action whose consequence is already on screen; a tap alone never fires it.",
                MediaDemos.HoldToConfirm()),
            DemoUi.Section(
                "Swipe actions",
                "Use for touch-first triage rows with one or two quick actions per side; the keyboard reaches the same actions.",
                MediaDemos.SwipeActions()),
            DemoUi.Section(
                "Reactions",
                "Use for lightweight acknowledgement on comments and release notes. The picker tray opens as a light-dismiss popup.",
                MediaDemos.Reactions())
        }
    };
}
