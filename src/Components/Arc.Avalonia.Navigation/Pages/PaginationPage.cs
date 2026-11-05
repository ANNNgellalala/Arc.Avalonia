using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Navigation.Controls;

namespace Arc.Avalonia.Navigation.Pages;

/// <summary>
/// Pagination's completeness preview: the first page (previous arrow disabled), a middle page with ellipses
/// on both sides, the last page (next arrow disabled) and a short collection that lists every page.
/// Declared state keys match Docs/pagination.json so the audit can compare the two.
/// </summary>
public sealed class PaginationPage : ArcItemPage
{
    public override string ItemSlug => "pagination";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-first", "state-middle", "state-last", "variant-short"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Labelled("第一页", "上一页箭头禁用，窗口从第 1 页开始。",
            new ArcPagination { Label = "Results", Page = 1, PageCount = 12 }));

        panel.Children.Add(Labelled("中间页", "首尾保留，窗口两侧用省略号跳过。",
            new ArcPagination { Label = "Results", Page = 6, PageCount = 12 }));

        panel.Children.Add(Labelled("最后一页", "下一页箭头禁用。",
            new ArcPagination { Label = "Results", Page = 12, PageCount = 12 }));

        panel.Children.Add(Labelled("短集合", "七页以内列出全部页码，不出现省略号。",
            new ArcPagination { Label = "Results", Page = 3, PageCount = 5 }));

        return panel;
    }

    private static Control Labelled(string label, string note, Control content)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        panel.Children.Add(content);
        panel.Children.Add(DemoUi.Caption(note));
        return panel;
    }
}
