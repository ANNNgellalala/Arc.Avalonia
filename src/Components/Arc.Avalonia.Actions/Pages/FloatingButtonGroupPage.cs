using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Floating button group's completeness preview: the muted page tray, the floating selection toolbar,
/// icon-only mode, both sizes and both orientations, plus a pressed mode action and a disabled slot.
/// Declared state keys match Docs/floating-button-group.json.
/// </summary>
public sealed class FloatingButtonGroupPage : ArcItemPage
{
    public override string ItemSlug => "floating-button-group";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-muted", "variant-floating", "variant-icon-only",
        "size-sm", "size-md",
        "orientation-horizontal", "orientation-vertical",
        "state-pressed", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        var board = new ArcFloatingButtonGroup
        {
            Label = "Board actions",
            Items = new[]
            {
                new ArcFloatingButtonItem { Id = "undo", Label = "Undo", Icon = "undo-2", IconOnly = true, Shortcut = "Ctrl+Z", IsDisabled = true },
                new ArcFloatingButtonItem { Id = "redo", Label = "Redo", Icon = "redo-2", IconOnly = true, Shortcut = "Ctrl+Shift+Z" },
                new ArcFloatingButtonItem { IsSeparator = true },
                new ArcFloatingButtonItem { Id = "comment", Label = "Comment", Icon = "message-circle", IconOnly = true, Shortcut = "C" },
                new ArcFloatingButtonItem { Id = "share", Label = "Share", Icon = "link" },
                new ArcFloatingButtonItem { IsSeparator = true },
                new ArcFloatingButtonItem { Id = "present", Label = "Present", Icon = "play", IconOnly = true, IsPressed = true }
            }
        };

        var selection = new ArcFloatingButtonGroup
        {
            Label = "Selection actions",
            Variant = ArcFloatingButtonVariant.Floating,
            IconOnly = true,
            Items = new[]
            {
                new ArcFloatingButtonItem { Id = "bold", Label = "Bold", Icon = "pencil", Tooltip = "Bold", Shortcut = "Ctrl+B" },
                new ArcFloatingButtonItem { Id = "copy", Label = "Copy", Icon = "copy", Tooltip = "Copy", Shortcut = "Ctrl+C" },
                new ArcFloatingButtonItem { Id = "link", Label = "Copy link", Icon = "link", Tooltip = "Copy link", Shortcut = "Ctrl+L" }
            }
        };

        var rail = new ArcFloatingButtonGroup
        {
            Label = "Canvas tools",
            Orientation = ArcFloatingButtonOrientation.Vertical,
            Size = ArcFloatingButtonSize.Sm,
            Items = new[]
            {
                new ArcFloatingButtonItem { Id = "zoom-in", Label = "Zoom in", Icon = "zoom-in", IconOnly = true },
                new ArcFloatingButtonItem { Id = "zoom-out", Label = "Zoom out", Icon = "zoom-out", IconOnly = true },
                new ArcFloatingButtonItem { Id = "fit", Label = "Zoom to fit", Icon = "maximize", IconOnly = true }
            }
        };

        panel.Children.Add(Row("页面托盘", board));
        panel.Children.Add(Row("浮动与纯图标", selection));
        panel.Children.Add(Row("尺寸与方向", rail));

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
