using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Button group's completeness preview: outline and solid variants, both sizes, both orientations and the
/// trailing menu held open in a reserved box. Declared state keys match Docs/button-group.json.
/// </summary>
public sealed class ButtonGroupPage : ArcItemPage
{
    public override string ItemSlug => "button-group";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-outline", "variant-solid",
        "size-sm", "size-md",
        "orientation-horizontal", "orientation-vertical",
        "state-closed", "state-open"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        var outline = new ArcButtonGroup
        {
            Label = "Document actions",
            Variant = ArcButtonGroupVariant.Outline,
            Items = new[]
            {
                new ArcButtonGroupItem { Label = "Share", Icon = "link" },
                new ArcButtonGroupItem { Label = "Duplicate", Icon = "copy" },
                new ArcButtonGroupItem { Label = "Archive", Icon = "archive" }
            }
        };

        var solid = new ArcButtonGroup
        {
            Label = "Review actions",
            Variant = ArcButtonGroupVariant.Solid,
            Items = new[]
            {
                new ArcButtonGroupItem { Label = "Approve" },
                new ArcButtonGroupItem { Label = "Request changes" },
                new ArcButtonGroupItem { Label = "Draft" }
            }
        };

        var small = new ArcButtonGroup
        {
            Label = "Zoom controls",
            Size = ArcButtonGroupSize.Sm,
            Items = new[]
            {
                new ArcButtonGroupItem { Label = "Zoom out", Icon = "zoom-out", IconOnly = true },
                new ArcButtonGroupItem
                {
                    Label = "Zoom level",
                    Content = new TextBlock
                    {
                        Text = "100%",
                        Classes = { "arc-body-sm" },
                        VerticalAlignment = VerticalAlignment.Center
                    }
                },
                new ArcButtonGroupItem { Label = "Zoom in", Icon = "zoom-in", IconOnly = true }
            }
        };

        var medium = new ArcButtonGroup
        {
            Label = "Layer actions",
            Size = ArcButtonGroupSize.Md,
            Items = new[]
            {
                new ArcButtonGroupItem { Label = "Bring forward", Icon = "arrow-up" },
                new ArcButtonGroupItem { Label = "Send backward", Icon = "arrow-down" }
            }
        };

        var vertical = new ArcButtonGroup
        {
            Label = "Canvas controls",
            Orientation = ArcButtonGroupOrientation.Vertical,
            Size = ArcButtonGroupSize.Sm,
            Items = new[]
            {
                new ArcButtonGroupItem { Label = "Zoom in", Icon = "plus", IconOnly = true },
                new ArcButtonGroupItem { Label = "Zoom out", Icon = "minus", IconOnly = true },
                new ArcButtonGroupItem { Label = "Fit", Icon = "maximize", IconOnly = true }
            }
        };

        var open = new ArcButtonGroup
        {
            Label = "Review actions with more",
            IsOpen = true,
            Menu = new ArcMenuDefinition
            {
                Label = "More review actions",
                Items =
                {
                    new ArcMenuItem { Label = "Request changes", Icon = "pencil" },
                    new ArcMenuItem { Label = "Assign reviewer", Icon = "user" },
                    new ArcMenuItem
                    {
                        Label = "Dismiss review",
                        Icon = "trash-2",
                        IsDestructive = true,
                        SeparatorBefore = true
                    }
                }
            },
            Items = new[]
            {
                new ArcButtonGroupItem { Label = "Approve", Icon = "check" },
                new ArcButtonGroupItem { Label = "Comment", Icon = "message-circle" }
            }
        };

        panel.Children.Add(Row("变体", outline, solid));
        panel.Children.Add(Row("尺寸", small, medium));
        panel.Children.Add(Row("方向与菜单", vertical, Reserved(300, 250, open)));

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

    private static Control Reserved(double width, double height, Control content) => new Panel
    {
        Width = width,
        Height = height,
        HorizontalAlignment = HorizontalAlignment.Left,
        Children = { content }
    };
}
