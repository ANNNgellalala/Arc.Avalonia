using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Media.Controls;

namespace Arc.Avalonia.Media.Pages;

/// <summary>
/// Resizable panels' completeness preview: a horizontal split, a vertical split, a three pane layout and a
/// layout with one collapsible pane already collapsed. Shares and collapse states are set before layout, so
/// every declared state is a still frame. Declared state keys match Docs/resizable-panels.json so the audit
/// can compare the two lists.
/// </summary>
public sealed class ResizablePanelsPage : ArcItemPage
{
    public override string ItemSlug => "resizable-panels";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "orientation-horizontal", "orientation-vertical", "layout-three-panes", "state-collapsed"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };

        row.Children.Add(Captioned(Horizontal(), "左右分栏：30% / 70%，拖动分隔条即时换份额。"));

        row.Children.Add(Captioned(ThreePanes(), "三个窗格、两条分隔条：份额 24% / 46% / 30%，缩放时比例保持。"));

        row.Children.Add(Captioned(Vertical(), "上下分栏：方向换成 Vertical，分隔条横向拖动。"));

        row.Children.Add(Captioned(Collapsed(), "折叠态：左侧可折叠窗格收起，留下 chevron 作为恢复入口。"));

        return row;
    }

    private static Control Horizontal() => new ArcResizablePanels
    {
        Label = "Mail",
        Width = 380,
        Height = 190,
        Panes = new List<ArcResizablePane>
        {
            new("folders", "Folders", 30, 80, PaneContent("Folders", "Inbox", "Starred", "Drafts", "Sent")),
            new("preview", "Preview", 70, 120, PaneContent("Preview", "Carousel copy", "Maya Ali · 2:14 pm"))
        }
    };

    private static Control ThreePanes() => new ArcResizablePanels
    {
        Label = "Mail with preview",
        Width = 380,
        Height = 190,
        Panes = new List<ArcResizablePane>
        {
            new("folders", "Folders", 24, 60, PaneContent("Folders", "Inbox", "Drafts")),
            new("messages", "Messages", 46, 90, PaneContent("Messages", "Re: design review", "Invoice 2041")),
            new("preview", "Preview", 30, 90, PaneContent("Preview", "Carousel copy", "One sentence per slide."))
        }
    };

    private static Control Vertical() => new ArcResizablePanels
    {
        Label = "Inspector",
        Width = 380,
        Height = 220,
        Orientation = ArcPanelOrientation.Vertical,
        Panes = new List<ArcResizablePane>
        {
            new("layers", "Layers", 58, 60, PaneContent("Layers", "Hero", "Carousel", "Footer")),
            new("properties", "Properties", 42, 60, PaneContent("Properties", "Width · 1180", "Radius · 26", "Gap · 24"))
        }
    };

    private static Control Collapsed() => new ArcResizablePanels
    {
        Label = "Mail with a collapsed folder rail",
        Width = 380,
        Height = 180,
        Panes = new List<ArcResizablePane>
        {
            new("folders", "Folders", 24, 60, PaneContent("Folders", "Inbox", "Drafts"))
            {
                Collapsible = true,
                DefaultCollapsed = true
            },
            new("messages", "Messages", 46, 90, PaneContent("Messages", "Re: design review", "Invoice 2041")),
            new("preview", "Preview", 30, 90, PaneContent("Preview", "Carousel copy", "One sentence per slide."))
        }
    };

    private static Control PaneContent(string title, params string[] rows)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(12) };
        panel.Children.Add(new TextBlock { Text = title, Classes = { "arc-label" } });
        foreach (var row in rows)
        {
            panel.Children.Add(new TextBlock
            {
                Text = row,
                Classes = { "arc-caption" },
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        return panel;
    }

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
