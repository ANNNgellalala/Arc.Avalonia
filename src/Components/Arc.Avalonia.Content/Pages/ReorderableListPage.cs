using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Content.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Content.Pages;

/// <summary>
/// Reorderable list's completeness preview: a user-ordered queue, and a second queue with a disabled row
/// that keeps its place but cannot be lifted. Both lists are static; the reorder affordance is the grip on
/// every row, so the preview reads without a pointer.
/// </summary>
public sealed class ReorderableListPage : ArcItemPage
{
    public override string ItemSlug => "reorderable-list";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-default", "variant-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };

        row.Children.Add(Card(
            "可拖动列表",
            "每行左侧是 36px 的抓手；拖动或按空格提起、方向键移动、再按空格放下，Esc 取消。",
            includeDisabled: false));

        row.Children.Add(Card(
            "含禁用行",
            "Disabled=true 的行保留原位并显示 Disabled 徽标，不能被提起，也不会随其他行移动。",
            includeDisabled: true));

        return row;
    }

    private static Control Card(string title, string note, bool includeDisabled)
    {
        var items = new List<ArcReorderItem>
        {
            new() { Id = "pricing", Title = "Ship pricing page", Detail = "Due Friday" },
            new() { Id = "login", Title = "Fix login redirect", Detail = "Blocked by SSO" },
            new() { Id = "changelog", Title = "Write changelog", Detail = "Draft ready" },
            new() { Id = "email", Title = "Refresh onboarding email", Detail = "Waiting on brand", Disabled = includeDisabled },
            new() { Id = "demo", Title = "Record the demo", Detail = "Thursday" }
        };

        var list = new ArcReorderableList
        {
            Items = items,
            Label = "Release priorities",
            Width = 560
        };

        return DemoUi.Card(title, note, list);
    }
}
