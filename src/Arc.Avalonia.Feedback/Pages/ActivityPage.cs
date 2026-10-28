using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>Activity feeds and threaded comments.</summary>
public sealed class ActivityPage : ArcPage
{
    public override string Title => "Activity";

    public override string Group => "Feedback";

    public override string? Description =>
        "What happened, newest first, and the discussion attached to one object.";

    public override int Order => 304;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Timeline",
                "Use for project history, audit logs, and deploy streams where order and recency matter.",
                FeedbackDemos.Timeline()),
            DemoUi.Section(
                "Comment thread",
                "Use for review comments attached to a document, design, or record, with replies and resolve.",
                FeedbackDemos.CommentThread())
        }
    };
}
