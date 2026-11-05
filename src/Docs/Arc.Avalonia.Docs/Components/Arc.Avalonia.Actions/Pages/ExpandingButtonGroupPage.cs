using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Expanding button group's completeness preview: one action holds the label while its neighbours stay
/// collapsed, a destructive action takes the label to show its danger tone, and the small size carries a
/// disabled slot. Declared state keys match Docs/expanding-button-group.json.
/// </summary>
public sealed class ExpandingButtonGroupPage : ArcItemPage
{
    public override string ItemSlug => "expanding-button-group";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-sm", "size-md", "tone-danger",
        "state-expanded", "state-collapsed", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        var messageActions = new ArcExpandingButtonGroup
        {
            Label = "Message actions",
            DefaultExpanded = "delete",
            Items = new[]
            {
                new ArcExpandingButtonItem { Id = "reply", Label = "Reply", Icon = "message-circle" },
                new ArcExpandingButtonItem { Id = "forward", Label = "Forward", Icon = "send" },
                new ArcExpandingButtonItem { Id = "archive", Label = "Archive", Icon = "archive", DoneLabel = "Archived" },
                new ArcExpandingButtonItem { Id = "snooze", Label = "Snooze", Icon = "clock", DoneLabel = "Snoozed" },
                new ArcExpandingButtonItem
                {
                    Id = "delete",
                    Label = "Delete",
                    Icon = "trash-2",
                    DoneLabel = "Deleted",
                    Tone = ArcExpandingButtonTone.Danger
                }
            }
        };

        var fileActions = new ArcExpandingButtonGroup
        {
            Label = "File actions",
            Size = ArcExpandingButtonSize.Sm,
            DefaultExpanded = "copy-link",
            Items = new[]
            {
                new ArcExpandingButtonItem { Id = "open", Label = "Open", Icon = "file" },
                new ArcExpandingButtonItem
                {
                    Id = "copy-link",
                    Label = "Copy link",
                    Icon = "link",
                    DoneLabel = "Copied",
                    IsDisabled = true
                },
                new ArcExpandingButtonItem
                {
                    Id = "delete",
                    Label = "Delete",
                    Icon = "trash-2",
                    DoneLabel = "Deleted",
                    Tone = ArcExpandingButtonTone.Danger
                }
            }
        };

        panel.Children.Add(Row("标签展开与让位", messageActions));
        panel.Children.Add(Row("尺寸与不可用", fileActions));

        return panel;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 16, 0);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
