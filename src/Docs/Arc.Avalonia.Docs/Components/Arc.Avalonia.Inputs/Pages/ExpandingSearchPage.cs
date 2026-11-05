using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Expanding search's completeness preview: collapsed icon, expanded field with suggestions, both anchor
/// directions and the disabled state. Declared state keys match Docs/expanding-search.json.
/// </summary>
public sealed class ExpandingSearchPage : ArcItemPage
{
    public override string ItemSlug => "expanding-search";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-collapsed", "state-expanded", "state-suggestions",
        "variant-anchor-start", "variant-anchor-end", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var items = new List<ArcExpandingSearchItem>
        {
            new("p1", "Arc website", "Projects", "今天更新", new[] { "marketing", "site" }),
            new("p2", "Northwind dashboard", "Projects", "3 天前", new[] { "finance" }),
            new("d1", "Motion tokens", "Docs", "Spring, duration", new[] { "spring", "motion" }),
            new("d2", "Focus rings", "Docs", "Accessibility", new[] { "keyboard", "focus" })
        };

        var suggestions = new List<ArcExpandingSearchItem>
        {
            new("s1", "Arc website", "Recent", "昨天"),
            new("s2", "Motion tokens", "Recent", "上周")
        };

        var collapsed = new ArcExpandingSearch
        {
            Width = 56,
            Label = "搜索项目与文档",
            Items = items,
            Suggestions = suggestions
        };

        var disabled = new ArcExpandingSearch
        {
            Width = 56,
            Label = "搜索项目与文档",
            Items = items,
            IsEnabled = false
        };

        var expanded = new ArcExpandingSearch
        {
            Width = 440,
            Label = "搜索项目与文档",
            Items = items,
            Anchor = ArcExpandingSearchAnchor.End,
            ExpandedWidth = 400,
            IsExpanded = true
        };

        var withSuggestions = new ArcExpandingSearch
        {
            Width = 440,
            Label = "搜索项目与文档",
            Items = items,
            Suggestions = suggestions,
            SuggestionsLabel = "最近搜索",
            Anchor = ArcExpandingSearchAnchor.Start,
            ExpandedWidth = 400,
            IsExpanded = true
        };

        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("折叠与禁用",
            Stage(collapsed, 150, 100, "Collapsed：只占一枚图标"),
            Stage(disabled, 150, 100, "Disabled")));

        panel.Children.Add(Row("展开",
            Stage(expanded, 460, 280, "Expanded，Anchor=End：字段向右长"),
            Stage(withSuggestions, 460, 280, "Suggestions，Anchor=Start：空查询列出最近项")));

        return panel;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var wrap = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 18, 14);
            wrap.Children.Add(child);
        }

        row.Children.Add(wrap);
        return row;
    }

    /// <summary>Reserves room for the results panel, which overlays beneath the field.</summary>
    private static Control Stage(Control content, double width, double height, string caption)
    {
        var stack = new StackPanel { Spacing = 6, Width = width };
        stack.Children.Add(new TextBlock { Text = caption, Classes = { "arc-caption" } });
        stack.Children.Add(new Border { Height = height, Child = content });
        return stack;
    }
}
