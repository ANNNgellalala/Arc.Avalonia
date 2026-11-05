using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Toast stack's completeness preview: a host holding two toasts, and another holding three with an action.
/// Toasts use Duration zero so the static preview keeps them on screen. Keys match Docs/toast-stack.json.
/// </summary>
public sealed class ToastStackPage : ArcItemPage
{
    public override string ItemSlug => "toast-stack";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "stack-two", "stack-three", "stack-action"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var twoToasts = new ArcToastHost();
        twoToasts.Show("Changes saved", "Your billing email is updated.", null, null, 0);
        twoToasts.Show("Invite sent to Sam", null, null, null, 0);

        var threeToasts = new ArcToastHost();
        threeToasts.Show("Import finished", "148 rows added.", null, null, 0);
        threeToasts.Show("Project archived", null, "Undo", null, 0);
        threeToasts.Show("Export ready", null, null, null, 0);

        var first = ActionDemos.Stage(360, 280, mockPage: false);
        first.Children.Add(twoToasts);

        var second = ActionDemos.Stage(360, 280, mockPage: false);
        second.Children.Add(threeToasts);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 20,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        row.Children.Add(Labelled("两条消息", first));
        row.Children.Add(Labelled("三条消息 · 含行动", second));

        return row;
    }

    private static Control Labelled(string label, Control content)
    {
        var column = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Left };
        column.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        column.Children.Add(content);
        return column;
    }
}
