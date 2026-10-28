using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>Source and payload inspection.</summary>
public sealed class DataViewsPage : ArcPage
{
    public override string Title => "Data views";

    public override string Group => "Feedback";

    public override string? Description =>
        "Read source with copy and collapse, or inspect a JSON payload as a tree.";

    public override int Order => 305;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Code block",
                "Use for documentation snippets and install commands, with copy and an optional collapse.",
                DataDemos.Code()),
            DemoUi.Section(
                "JSON viewer",
                "Use to inspect API payloads and webhook events, with search, copy path, and paging for long arrays.",
                DataDemos.Json())
        }
    };
}
