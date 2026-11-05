using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Pages;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;
using Arc.Avalonia.Overlays.Controls;

namespace Arc.Avalonia.Overlays.Pages;

/// <summary>
/// Dialog's completeness preview: an open destructive confirmation with a footer, an open confirm dialog with a
/// single primary action, and an informational dialog without a footer. Declared state keys match
/// Docs/dialog.json so the audit can compare the two. Each dialog is hosted in a fixed stage because a dialog
/// fills the nearest panel and dims everything behind it.
/// </summary>
public sealed class DialogPage : ArcItemPage
{
    public override string ItemSlug => "dialog";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-open", "state-destructive", "state-confirm", "layout-footer", "layout-no-footer"
    };

    // The stage already paints the muted surface and a mock page behind the scrim, so the page-level canvas
    // wrapper would only nest two backgrounds.
    public override bool DemoOnCanvas => false;

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Captioned(Destructive(), "破坏性决策：Danger 主行动放在底栏最右，按 Escape 或点遮罩关闭。")));

        panel.Children.Add(Row(
            Captioned(StaticFooter(), "底栏动作（静态）：一主一次，主行动用 Primary，确认后立刻生效。"),
            Captioned(StaticNoFooter(), "无底栏（静态）：面板只承载信息，用 Escape 或点遮罩收起。")));

        return panel;
    }

    private static Control Destructive()
    {
        var stage = ActionDemos.Stage(600, 300);

        var dialog = new ArcDialog
        {
            Title = "删除工作区",
            Description = "Acme Studio 里的项目、文件与邀请都会被删除，且无法撤销。",
            IsOpen = true,
            Content = DemoUi.Body("将删除 12 个项目、48 个文件与 3 个集成。", 320)
        };

        var cancel = new ArcButton
        {
            Content = "取消",
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm
        };
        var delete = new ArcButton
        {
            Content = "删除工作区",
            Variant = ArcButtonVariant.Danger,
            Size = ArcButtonSize.Sm
        };
        cancel.Click += (_, _) => dialog.IsOpen = false;
        delete.Click += (_, _) => dialog.IsOpen = false;

        dialog.Footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { cancel, delete }
        };

        stage.Children.Add(dialog);
        return stage;
    }

    /// <summary>Static footer example: no overlay and no focus race, so every declared state is visible.</summary>
    private static Control StaticFooter()
    {
        var panel = new StackPanel { Spacing = 14, Width = 380 };
        panel.Children.Add(new TextBlock { Text = "同步已暂停", Classes = { "arc-label" } });
        panel.Children.Add(new TextBlock
        {
            Text = "沙盒环境不会自动同步，直到你手动恢复。",
            Classes = { "arc-body-sm", "arc-secondary" },
            TextWrapping = TextWrapping.Wrap
        });
        var resume = new ArcButton
        {
            Content = "立即恢复",
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        panel.Children.Add(resume);
        return new Border
        {
            Padding = new Thickness(22),
            CornerRadius = new CornerRadius(22),
            BorderThickness = new Thickness(1),
            BorderBrush = DemoUi.Brush(ArcTokens.Border),
            Background = DemoUi.Brush(ArcTokens.Surface),
            Child = panel
        };
    }

    /// <summary>Static informational panel: the footerless layout without opening a second dialog.</summary>
    private static Control StaticNoFooter()
    {
        var panel = new StackPanel { Spacing = 10, Width = 380 };
        panel.Children.Add(new TextBlock { Text = "已连接到沙盒", Classes = { "arc-label" } });
        panel.Children.Add(new TextBlock
        {
            Text = "这里的数据每天 02:00 重置，改动不会同步到生产环境。",
            Classes = { "arc-body-sm", "arc-secondary" },
            TextWrapping = TextWrapping.Wrap
        });
        return new Border
        {
            Padding = new Thickness(22),
            CornerRadius = new CornerRadius(22),
            BorderThickness = new Thickness(1),
            BorderBrush = DemoUi.Brush(ArcTokens.Border),
            Background = DemoUi.Brush(ArcTokens.Surface),
            Child = panel
        };
    }

    private static Control Row(params Control[] columns)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var column in columns)
        {
            column.Margin = new Thickness(0, 0, 16, 16);
            row.Children.Add(column);
        }

        return row;
    }

    private static Control Captioned(Control stage, string caption)
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(stage);
        panel.Children.Add(new TextBlock
        {
            Text = caption,
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 600,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        return panel;
    }
}
