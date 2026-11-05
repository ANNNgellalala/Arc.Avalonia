using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Toast's completeness preview: the title-only form, the form with a description and the form with a
/// trailing action. Duration is zero so every form stays visible. Keys match Docs/toast.json.
/// </summary>
public sealed class ToastPage : ArcItemPage
{
    public override string ItemSlug => "toast";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-title-only", "variant-description", "variant-action"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 18, HorizontalAlignment = HorizontalAlignment.Left, Width = 460 };

        panel.Children.Add(Labelled("Title only", new ArcToast
        {
            Title = "Invite sent to Sam",
            Duration = 0
        }));

        panel.Children.Add(Labelled("With description", new ArcToast
        {
            Title = "Changes saved",
            Description = "Your billing email is updated.",
            Duration = 0
        }));

        panel.Children.Add(Labelled("With action", new ArcToast
        {
            Title = "Project archived",
            Description = "It moves to the archive after 30 days.",
            ActionLabel = "Undo",
            Duration = 0
        }));

        return panel;
    }

    private static Control Labelled(string label, Control content)
    {
        var column = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Left };
        column.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        column.Children.Add(content);
        return column;
    }
}
