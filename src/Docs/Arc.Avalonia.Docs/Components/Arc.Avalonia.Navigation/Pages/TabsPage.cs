using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Navigation.Controls;

namespace Arc.Avalonia.Navigation.Pages;

/// <summary>
/// Tabs' completeness preview: the selected and unselected triggers under the gliding pill, an icon tab and
/// a disabled tab. Declared state keys match Docs/tabs.json so the audit can compare the two.
/// </summary>
public sealed class TabsPage : ArcItemPage
{
    public override string ItemSlug => "tabs";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-selected", "state-unselected", "state-disabled", "state-icon"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("选中与未选中", "胶囊滑到当前标签，其余标签保持次要文字色", Basic()),
            Card("图标与禁用", "图标随选中状态变色；禁用的标签保留位置但不可聚焦", Icons())));

        return panel;
    }

    private static Control Row(params Control[] cards)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var card in cards)
        {
            card.Margin = new Thickness(0, 0, 16, 16);
            row.Children.Add(card);
        }

        return row;
    }

    private static Control Card(string title, string note, Control content) => DemoUi.Card(title, note, content);

    private static ArcTabs Basic() => new()
    {
        Label = "项目分区",
        SelectedIndex = 1,
        Width = 370,
        Items = new[]
        {
            new ArcTabItem("overview", "概览", DemoUi.Body("十二个项目、四位评审，周五发布。", 400)),
            new ArcTabItem("activity", "活动", DemoUi.Body("Maya 合并了结算分支，随后在定价页请求了评审。", 400)),
            new ArcTabItem("settings", "设置", DemoUi.Body("通知只发到频道，不发邮件；只有所有者可以归档。", 400))
        }
    };

    private static ArcTabs Icons() => new()
    {
        Label = "工作区分区",
        SelectedIndex = 0,
        Width = 370,
        Items = new[]
        {
            new ArcTabItem("members", "成员", DemoUi.Body("十二位成员，其中三位是管理员。", 400)),
            new ArcTabItem
            {
                Value = "billing",
                Header = "账单",
                Icon = "credit-card",
                Content = DemoUi.Body("下一张发票会在 3 月 12 日生成。", 400)
            },
            new ArcTabItem
            {
                Value = "usage",
                Header = "用量",
                Icon = "zap",
                Content = DemoUi.Body("缓存命中的请求不计费。", 400)
            },
            new ArcTabItem
            {
                Value = "archive",
                Header = "归档",
                Icon = "file",
                IsDisabled = true,
                Content = DemoUi.Body("归档需要所有者权限。", 400)
            }
        }
    };
}
