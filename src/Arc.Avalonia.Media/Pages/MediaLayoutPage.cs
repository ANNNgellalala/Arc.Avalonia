using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Media.Pages;

/// <summary>Scrolling inside a fixed box, and split surfaces whose dividers trade space.</summary>
public sealed class MediaLayoutPage : ArcPage
{
    public override string Title => "Media layout";

    public override string Group => "Media";

    public override string? Description =>
        "Surfaces that hold content larger than their box, or that trade space between two panes.";

    public override int Order => 1002;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Scroll area",
                "Use wherever content scrolls inside a fixed box; the native scroll keeps its keyboard and touch behaviour.",
                MediaDemos.ScrollArea()),
            DemoUi.Section(
                "Resizable panels",
                "Use for editor, mail and dashboard layouts where people trade space between side by side panes.",
                MediaDemos.ResizablePanels())
        }
    };
}
