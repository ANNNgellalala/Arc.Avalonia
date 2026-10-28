using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>Live presence: stacked faces, typing and idle states, overflow, and the roster.</summary>
public sealed class PresencePage : ArcPage
{
    public override string Title => "Presence";

    public override string Group => "Avatars";

    public override string? Description =>
        "See who is here now, what they are doing, and how the stack makes room as people come and go.";

    public override int Order => 308;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Presence stack",
                "Use in a live header, a call roster, or a shared document where joins, leaves, and typing should be felt.",
                ProDemos.Presence()),
            DemoUi.Section(
                "Who's here",
                "Use the list alongside the stack when every name and status must be readable without hovering.",
                ProDemos.WhosHere())
        }
    };
}
