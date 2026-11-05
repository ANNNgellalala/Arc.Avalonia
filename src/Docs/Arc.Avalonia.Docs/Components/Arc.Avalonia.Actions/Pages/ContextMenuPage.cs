using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Context menu's completeness preview: the menu opened over a file row, so its checked item, shortcut hint
/// and destructive row are all visible, plus the same target closed. Declared state keys match
/// Docs/context-menu.json so the audit can compare the two lists.
/// </summary>
public sealed class ContextMenuPage : ArcItemPage
{
    public override string ItemSlug => "context-menu";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-open", "state-closed", "variant-checked-item", "variant-destructive-item"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };

        row.Children.Add(Captioned(Opened(), "打开态：菜单在目标下沿展开；Show in folder 带勾，Delete file 用危险色收尾。"));

        row.Children.Add(Captioned(Closed(), "收起态：只有被操作的对象本身，右键或 Shift+F10 才会展开菜单。"));

        return row;
    }

    private static Control Opened() => Reserved(new ArcContextMenu
    {
        VerticalAlignment = VerticalAlignment.Top,
        Label = "Actions for Q3 forecast.xlsx",
        Content = FileRow(),
        IsOpen = true,
        Items = new List<ArcMenuItem>
        {
            new() { Label = "Open", Icon = "external-link" },
            new() { Label = "Copy link", Icon = "link", Shortcut = "⌘C" },
            new() { Label = "Show in folder", Icon = "folder", IsChecked = true },
            new()
            {
                Label = "Delete file",
                Icon = "trash-2",
                IsDestructive = true,
                SeparatorBefore = true
            }
        }
    });

    private static Control Closed() => new ArcContextMenu
    {
        VerticalAlignment = VerticalAlignment.Top,
        Label = "Actions for Q3 forecast.xlsx",
        Content = FileRow(),
        Items = new List<ArcMenuItem>
        {
            new() { Label = "Open", Icon = "external-link" },
            new() { Label = "Show in folder", Icon = "folder", IsChecked = true }
        }
    };

    private static Control FileRow() => new Border
    {
        Width = 340,
        Padding = new Thickness(16),
        CornerRadius = new CornerRadius(18),
        Background = DemoUi.Brush(ArcTokens.Surface),
        BorderBrush = DemoUi.Brush(ArcTokens.Border),
        BorderThickness = new Thickness(1),
        Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children =
            {
                new ArcIcon { Icon = "file", Size = 20, VerticalAlignment = VerticalAlignment.Center },
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = "Q3 forecast.xlsx", Classes = { "arc-label" } },
                        new TextBlock { Text = "4.2 MB · edited today", Classes = { "arc-caption" } }
                    }
                }
            }
        }
    };

    /// <summary>A fixed box that leaves room for the open surface without covering the next column.</summary>
    private static Control Reserved(Control content) => new Panel
    {
        Width = 380,
        Height = 300,
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
            MaxWidth = 380,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        return panel;
    }
}
