using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Effects.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Effects.Pages;

/// <summary>
/// Expandable card's completeness preview: a collapsed card with fixed widths, an expanded card that also
/// grows wider, and a fluid card that fills its container. The expanded card opens from code once it is
/// attached, so its details are part of the still frame. Declared state keys match
/// Docs/expandable-card.json so the audit can compare the two lists.
/// </summary>
public sealed class ExpandableCardPage : ArcItemPage
{
    public override string ItemSlug => "expandable-card";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-collapsed", "state-expanded", "variant-fixed-width", "variant-fluid-width"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };

        row.Children.Add(Captioned(Collapsed(), "收起态：固定宽度上限 420，只有标题与一行描述。"));

        row.Children.Add(Captioned(Expanded(), "展开态：细节铺开，宽度从 420 弹性长到 470，箭头旋转。"));

        row.Children.Add(Captioned(Fluid(), "流体宽度：不设宽度上限，卡片填满 420 的容器，只在高度上展开。"));

        return row;
    }

    private static ArcExpandableCard Collapsed() => new()
    {
        Title = "Pro plan",
        Description = "Billed yearly, renews 4 March",
        CollapsedWidth = 420,
        ExpandedWidth = 470,
        Content = Details()
    };

    /// <summary>Starts collapsed in the initializer, then expands after the first layout so its height measures correctly.</summary>
    private static ArcExpandableCard Expanded()
    {
        var card = new ArcExpandableCard
        {
            Title = "Pro plan",
            Description = "Billed yearly, renews 4 March",
            CollapsedWidth = 420,
            ExpandedWidth = 470,
            Content = Details()
        };

        var expanded = false;
        void ApplyOnFirstLayout(object? sender, EventArgs e)
        {
            if (expanded || card.Bounds.Width <= 0)
            {
                return;
            }

            expanded = true;
            card.LayoutUpdated -= ApplyOnFirstLayout;
            card.IsExpanded = true;
        }

        card.LayoutUpdated += ApplyOnFirstLayout;
        return card;
    }

    private static Control Fluid() => new Border
    {
        Width = 420,
        Padding = new Thickness(0),
        Child = new ArcExpandableCard
        {
            // Stretch lets the card take the container's width instead of its content width.
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Title = "Team plan",
            Description = "Seats, invoices and retention",
            Content = Details()
        }
    };

    private static Control Details() => new StackPanel
    {
        Spacing = 8,
        Children =
        {
            DetailRow("Seats", "12 of 20 used"),
            DetailRow("Renews", "4 March 2027"),
            DetailRow("Payment", "Visa ending 4242")
        }
    };

    private static Control DetailRow(string label, string value)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var text = new TextBlock
        {
            Text = value,
            Classes = { "arc-body-sm" },
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private static Control Captioned(Control content, string caption)
    {
        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 0, 20, 16)
        };
        panel.Children.Add(content);
        panel.Children.Add(new TextBlock
        {
            Text = caption,
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        return panel;
    }
}
