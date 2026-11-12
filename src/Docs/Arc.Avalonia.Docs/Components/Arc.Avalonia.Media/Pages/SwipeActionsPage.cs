using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Media.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Media.Pages;

/// <summary>
/// Swipe actions' completeness preview: a tappable row at rest, a row with the leading action revealed, a
/// row with two trailing actions revealed (the last one destructive) and a second row at rest whose hidden
/// trailing actions include the destructive delete. The first row's content is a Button, proving that a tap
/// reaches the row content while a horizontal drag still reveals the actions (the press is observed in the
/// tunnel phase and the pointer is only captured once the horizontal axis wins). The last row is the visual
/// regression assertion for the closed-state colour leak: a resting row must not paint a single danger pixel,
/// because both action hosts are invisible while the surface covers them (see tools/verify-swipe-leak.ps1).
/// The revealed rows are opened from code once they are attached, so every declared state is visible as a
/// still frame without any input. Declared state keys match Docs/swipe-actions.json so the audit can compare
/// the two lists.
/// </summary>
public sealed class SwipeActionsPage : ArcItemPage
{
    public override string ItemSlug => "swipe-actions";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-closed", "state-leading-open", "state-trailing-open",
        "variant-two-actions", "variant-danger-action", "variant-tappable-row"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 18, HorizontalAlignment = HorizontalAlignment.Left };

        var tapCount = 0;
        var tapStatus = DemoUi.Caption("点一下这一行：上面的计数会更新；从行上横向拖动则会露出动作，且不会触发点击。");

        panel.Children.Add(Captioned(
            Row("Design review", "Design review", "9:41",
                onActivated: () => tapStatus.Text = $"行内容被点击 {++tapCount} 次（横向滑动不会触发点击）。"),
            "收起态 + 可点击行：行内容是一个 Button，轻点触发 Click；横向拖动仍会露出两侧动作，两者不冲突。"));
        panel.Children.Add(tapStatus);

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

        // Regression assertion for docs/analysis/swipe-leak: this row is closed, yet its hidden trailing
        // actions include the destructive delete. ArcSwipeActions keeps both action hosts invisible until
        // the row opens, so the resting surface must show no ArcDanger pixel at all. The scan in
        // tools/verify-swipe-leak.ps1 targets this last row and uses the opened row above as a positive
        // control for the danger-colour predicate.
        panel.Children.Add(Captioned(
            Row("Fortnightly report", "Fortnightly report", "Fri",
                trailing: new List<ArcSwipeAction>
                {
                    new("Archive", "archive"),
                    new("Delete", "trash-2", ArcSwipeActionTone.Danger)
                }),
            "收起态回归断言：行停在原位，右侧含危险色的动作完全藏起；这一行的表面上不得出现任何危险色像素。"));

        return panel;
    }

    private static ArcSwipeActions Row(string sender, string subject, string time,
        IList<ArcSwipeAction>? trailing = null, System.Action? onActivated = null)
    {
        Control content = MailRow(sender, subject, time);
        if (onActivated is not null)
        {
            // The tappable-row pattern: an ordinary button inside Content. The component lets the tap
            // through and only steals the pointer once a horizontal drag starts.
            var button = new Button
            {
                Content = content,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                MinHeight = 0,
                CornerRadius = new CornerRadius(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            button.Click += (_, _) => onActivated();
            content = button;
        }

        return new ArcSwipeActions
        {
            Label = sender,
            Width = 640,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = content,
            Trailing = trailing ?? new List<ArcSwipeAction>
            {
                new("Flag", "flag"),
                new("Archive", "archive")
            }
        };
    }

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
