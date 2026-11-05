using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.DataDisplay.Pages;

/// <summary>
/// Tree view's completeness preview: the file tree with folders open and a leaf selected, and the same tree
/// with every folder collapsed. Both trees are static and the hierarchy is a fixed sample, so the preview is
/// deterministic.
/// </summary>
public sealed class TreeViewPage : ArcItemPage
{
    public override string ItemSlug => "tree-view";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-expanded", "variant-collapsed", "state-selected"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };

        var expanded = Card("已展开、叶子选中", "src 与 components 展开，card.tsx 处于选中态；缩进与滑动的选中底纹表达层级。",
            expanded: true);
        var collapsed = Card("全部收起", "DefaultExpandedIds 为空时只显示根节点；右箭头或点击逐层展开。",
            expanded: false);

        row.Children.Add(expanded);
        row.Children.Add(collapsed);
        return row;
    }

    private static Control Card(string title, string note, bool expanded)
    {
        var tree = new ArcTreeView
        {
            Nodes = Nodes(),
            Label = "Files",
            DefaultExpandedIds = expanded ? new[] { "src", "components", "app" } : new string[0],
            SelectedId = expanded ? "card" : "src",
            Width = 340,
            Height = 320
        };

        return DemoUi.Card(title, note, tree);
    }

    private static List<ArcTreeNode> Nodes() => new()
    {
        new()
        {
            Id = "src",
            Label = "src",
            Children = new List<ArcTreeNode>
            {
                new()
                {
                    Id = "components",
                    Label = "components",
                    Children = new List<ArcTreeNode>
                    {
                        new() { Id = "button", Label = "button.tsx" },
                        new() { Id = "card", Label = "card.tsx" },
                        new() { Id = "dialog", Label = "dialog.tsx" }
                    }
                },
                new()
                {
                    Id = "app",
                    Label = "app",
                    Children = new List<ArcTreeNode>
                    {
                        new() { Id = "page", Label = "page.tsx" },
                        new() { Id = "layout", Label = "layout.tsx" }
                    }
                },
                new()
                {
                    Id = "lib",
                    Label = "lib",
                    Children = new List<ArcTreeNode> { new() { Id = "format", Label = "format.ts" } }
                }
            }
        },
        new()
        {
            Id = "public",
            Label = "public",
            Children = new List<ArcTreeNode> { new() { Id = "favicon", Label = "favicon.ico" } }
        },
        new() { Id = "package", Label = "package.json" },
        new() { Id = "readme", Label = "README.md" }
    };
}
