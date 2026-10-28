using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>Filtering and sorting structured records.</summary>
public sealed class DataPage : ArcPage
{
    public override string Title => "Data";

    public override string Group => "Feedback";

    public override string? Description =>
        "Filter and sort structured records without leaving the list.";

    public override int Order => 301;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Filter toolbar",
                "Use above tables and lists to keep filters close and easy to reset, with search, chips, sort, and a count.",
                DataDemos.Filters()),
            DemoUi.Section(
                "Sortable data table",
                "Use for records people sort and select; wide tables scroll inside their own frame.",
                DataDemos.Table())
        }
    };
}
