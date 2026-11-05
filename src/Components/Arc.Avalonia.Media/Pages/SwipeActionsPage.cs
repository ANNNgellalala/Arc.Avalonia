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
/// Swipe actions' completeness preview: a row at rest, a row with the leading action revealed and a row
/// with two trailing actions revealed (the last one destructive). The revealed rows are opened from code
/// once they are attached, so every declared state is visible as a still frame without any input.
/// Declared state keys match Docs/swipe-actions.json so the audit can compare the two lists.
/// </summary>
public sealed class SwipeActionsPage : ArcItemPage
{
    public override string ItemSlug => "swipe-actions";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-closed", "state-leading-open", "state-trailing-open",
        "variant-two-actions", "variant-danger-action"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 18, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Captioned(
            Row("Design review", "Design review", "9:41"),
            "收起态：行回到原位，两侧动作完全藏起。"));

        panel.Children.Add(Captioned(
            Opened(Row("Invoice 2041", "Invoice 2041", "Yesterday"), side: 1,
                leading: new List<ArcSwipeAction>
                {
                    new("Unread", "mail", ArcSwipeActionTone.Accent) { KeepRow = true }
                }),
            "左侧露出：向右拖动后停靠，露出强调色填充的「未读」动作；键盘用户聚焦行后按 Right 也能到这里。"));

        panel.Children.Add(Captioned(
            Opened(Row("Release notes", "Release notes", "Mon"), side: -1,
                trailing: new List<ArcSwipeAction>
                {
                    new("Archive", "archive"),
                    new("Delete", "trash-2", ArcSwipeActionTone.Danger)
                }),
            "右侧露出：同一侧并排两个动作，最外侧的删除使用危险色，大滑松手会直接执行它。"));

        return panel;
    }

    private static ArcSwipeActions Row(string sender, string subject, string time) => new()
    {
        Label = sender,
        Width = 640,
        HorizontalAlignment = HorizontalAlignment.Left,
        Content = MailRow(sender, subject, time),
        Trailing = new List<ArcSwipeAction>
        {
            new("Flag", "flag"),
            new("Archive", "archive")
        }
    };

    /// <summary>Opens one side after the first layout, so the template and its action buttons already exist.</summary>
    private static ArcSwipeActions Opened(ArcSwipeActions row, int side,
        IList<ArcSwipeAction>? leading = null, IList<ArcSwipeAction>? trailing = null)
    {
        row.Leading = leading;
        row.Trailing = trailing;

        var opened = false;
        void OpenOnFirstLayout(object? sender, EventArgs e)
        {
            if (opened || row.Bounds.Width <= 0)
            {
                return;
            }

            opened = true;
            row.LayoutUpdated -= OpenOnFirstLayout;
            row.Open(side);
        }

        row.LayoutUpdated += OpenOnFirstLayout;
        return row;
    }

    private static Control MailRow(string sender, string subject, string time)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };

        var avatar = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(9999),
            Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = sender.Substring(0, 1),
                FontSize = ArcResources.Double(ArcTokens.TextXs),
                FontWeight = FontWeight.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        grid.Children.Add(avatar);

        var left = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        left.Children.Add(new TextBlock { Text = sender, Classes = { "arc-label" } });
        left.Children.Add(new TextBlock { Text = subject, Classes = { "arc-caption" } });
        Grid.SetColumn(left, 1);
        grid.Children.Add(left);

        var stamp = new TextBlock
        {
            Text = time,
            Classes = { "arc-caption" },
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        Grid.SetColumn(stamp, 2);
        grid.Children.Add(stamp);
        return grid;
    }

    private static Control Captioned(Control content, string caption)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(content);
        panel.Children.Add(new TextBlock
        {
            Text = caption,
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 640,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        return panel;
    }
}
