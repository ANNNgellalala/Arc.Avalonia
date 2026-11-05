using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Dropdown menu's completeness preview: one menu opened so its item icons, shortcut hints, separator and
/// destructive row are all visible, a closed trigger and a closed icon trigger. Declared state keys match
/// Docs/dropdown-menu.json so the audit can compare the two lists.
/// </summary>
public sealed class DropdownMenuPage : ArcItemPage
{
    public override string ItemSlug => "dropdown-menu";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-open", "state-closed", "variant-icon", "variant-shortcuts", "variant-destructive"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };

        row.Children.Add(Captioned(Opened(), "打开态：首行高亮，行尾显示快捷键，最后一条危险动作以分隔线收尾。"));

        row.Children.Add(Captioned(Closed(), "收起态：只有触发按钮与朝下的箭头。"));

        row.Children.Add(Captioned(IconTrigger(), "带图标：触发器前置 Lucide 图标，标签仍是动作名称。"));

        return row;
    }

    private static Control Opened() => Reserved(new ArcDropdownMenu
    {
        VerticalAlignment = VerticalAlignment.Top,
        Label = "Actions",
        Icon = "more-horizontal",
        IsOpen = true,
        Items = new List<ArcMenuItem>
        {
            new() { Label = "Rename", Icon = "pencil", Shortcut = "⌘R" },
            new() { Label = "Duplicate", Icon = "copy", Shortcut = "⌘D" },
            new() { Label = "Pin to sidebar", Icon = "pin" },
            new()
            {
                Label = "Delete project",
                Icon = "trash-2",
                IsDestructive = true,
                SeparatorBefore = true
            }
        }
    });

    private static Control Closed() => new ArcDropdownMenu
    {
        VerticalAlignment = VerticalAlignment.Top,
        Label = "Sort",
        Items = new List<ArcMenuItem>
        {
            new() { Label = "Newest first" },
            new() { Label = "Oldest first" }
        }
    };

    private static Control IconTrigger() => new ArcDropdownMenu
    {
        VerticalAlignment = VerticalAlignment.Top,
        Label = "Filter",
        Icon = "list-filter",
        Items = new List<ArcMenuItem>
        {
            new() { Label = "Unread only" },
            new() { Label = "Has attachment" }
        }
    };

    /// <summary>A fixed box that gives the open panel somewhere to land without covering the next column.</summary>
    private static Control Reserved(Control content) => new Panel
    {
        Width = 250,
        Height = 250,
        HorizontalAlignment = HorizontalAlignment.Left,
        Children = { content }
    };

    private static Control Captioned(Control content, string caption)
    {
        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 0, 16, 16)
        };
        panel.Children.Add(content);
        panel.Children.Add(new TextBlock
        {
            Text = caption,
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 260,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        return panel;
    }
}
