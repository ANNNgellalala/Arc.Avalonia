using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.DataDisplay.Pages;

/// <summary>Nested hierarchies and a year of activity.</summary>
public sealed class ExplorerPage : ArcPage
{
    public override string Title => "Explorer";

    public override string Group => "Feedback";

    public override string? Description =>
        "Browse a nested hierarchy, then see daily activity as one tinted square per day.";

    public override int Order => 306;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Tree view",
                "Use for files, folders, and nested categories people expand and select with the keyboard.",
                DataDemos.Tree()),
            DemoUi.Section(
                "Activity heatmap",
                "Use when rhythm, streaks, and quiet weeks matter more than exact comparison.",
                DataDemos.Heatmap())
        }
    };
}
