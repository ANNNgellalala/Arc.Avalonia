using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Media.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Media.Pages;

/// <summary>
/// Scroll area's completeness preview: a vertical list, a horizontal strip, a two axis board that shows
/// both bars at once and a fitted panel with nothing to scroll. Every state is laid out statically.
/// Declared state keys match Docs/scroll-area.json so the audit can compare the two lists.
/// </summary>
public sealed class ScrollAreaPage : ArcItemPage
{
    public override string ItemSlug => "scroll-area";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "orientation-vertical", "orientation-horizontal", "orientation-both",
        "scrollbars-always", "state-fitted"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };

        row.Children.Add(Captioned(
            Framed(new ArcScrollArea
            {
                Label = "Recent activity",
                Width = 340,
                Height = 190,
                Fade = 24,
                Scrollbars = ArcScrollbarsMode.Always,
                Content = ActivityList(12)
            }),
            "纵向：列表比 190px 高，底部渐隐与右侧滚动条一起出现。"));

        row.Children.Add(Captioned(
            Framed(new ArcScrollArea
            {
                Label = "Templates",
                Orientation = ArcScrollOrientation.Horizontal,
                Width = 340,
                Height = 190,
                Fade = 20,
                Scrollbars = ArcScrollbarsMode.Always,
                Content = TemplateStrip(7)
            }),
            "横向：竖向滚轮会被转成横向滚动，右侧没有条。"));

        row.Children.Add(Captioned(
            Framed(new ArcScrollArea
            {
                Label = "Quarterly board",
                Orientation = ArcScrollOrientation.Both,
                Width = 340,
                Height = 190,
                Fade = 20,
                Scrollbars = ArcScrollbarsMode.Always,
                Content = WideBoard()
            }),
            "双向：内容在两个方向都溢出，两条滚动条同时可见。"));

        row.Children.Add(Captioned(
            Framed(new ArcScrollArea
            {
                Label = "Status",
                Width = 240,
                Height = 190,
                Fade = 24,
                Content = ActivityList(2)
            }),
            "装得下：没有滚动条，也没有边缘渐隐，表面保持干净。"));

        return row;
    }

    private static Control Framed(Control child) => new Border
    {
        Background = DemoUi.Brush(ArcTokens.Surface),
        BorderBrush = DemoUi.Brush(ArcTokens.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(20),
        ClipToBounds = true,
        Child = child
    };

    private static Control ActivityList(int count)
    {
        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(16) };
        for (var index = 1; index <= count; index++)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            row.Children.Add(new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(99),
                Background = DemoUi.Brush(ArcTokens.Accent),
                VerticalAlignment = VerticalAlignment.Center
            });

            var text = new TextBlock
            {
                Text = $"Event {index} · updated the workspace",
                Classes = { "arc-body-sm" },
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 2,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            panel.Children.Add(row);
        }

        return panel;
    }

    private static Control TemplateStrip(int count)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(16)
        };
        for (var index = 1; index <= count; index++)
        {
            panel.Children.Add(new Border
            {
                Width = 116,
                Height = 128,
                CornerRadius = new CornerRadius(18),
                BorderThickness = new Thickness(1),
                BorderBrush = DemoUi.Brush(ArcTokens.Border),
                Background = DemoUi.Brush(ArcTokens.GradientSoft),
                Padding = new Thickness(12),
                Child = new TextBlock
                {
                    Text = $"Template {index}",
                    Classes = { "arc-caption" }
                }
            });
        }

        return panel;
    }

    /// <summary>A board wider and taller than the viewport so both axes report overflow.</summary>
    private static Control WideBoard()
    {
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
        for (var row = 1; row <= 8; row++)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            for (var column = 1; column <= 6; column++)
            {
                line.Children.Add(new Border
                {
                    Width = 140,
                    Height = 64,
                    CornerRadius = new CornerRadius(14),
                    Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
                    Padding = new Thickness(10),
                    Child = new TextBlock
                    {
                        Text = $"R{row} · C{column}",
                        Classes = { "arc-caption" }
                    }
                });
            }

            panel.Children.Add(line);
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
            MaxWidth = 340,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        return panel;
    }
}
