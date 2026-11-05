using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Navigation.Controls;

namespace Arc.Avalonia.Navigation.Pages;

/// <summary>
/// Breadcrumb's completeness preview: the basic four-level path, a long path that wraps inside a narrow
/// container, and the current-page crumb rendered as a label rather than a link. Declared state keys match
/// Docs/breadcrumb.json so the audit can compare the two.
/// </summary>
public sealed class BreadcrumbPage : ArcItemPage
{
    public override string ItemSlug => "breadcrumb";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-basic", "variant-long", "state-current"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Labelled("基础路径", "四级路径；最后一级是当前页，不是链接。", new ArcBreadcrumb
        {
            Label = "位置",
            Items = new[]
            {
                new ArcBreadcrumbItem("Workspace"),
                new ArcBreadcrumbItem("Settings"),
                new ArcBreadcrumbItem("Billing"),
                new ArcBreadcrumbItem("Invoices")
            }
        }));

        var longTrail = new ArcBreadcrumb
        {
            Label = "位置",
            Width = 520,
            Items = new[]
            {
                new ArcBreadcrumbItem("Workspace"),
                new ArcBreadcrumbItem("Settings"),
                new ArcBreadcrumbItem("Billing"),
                new ArcBreadcrumbItem("Invoices"),
                new ArcBreadcrumbItem("March 2026"),
                new ArcBreadcrumbItem("Acme Studio"),
                new ArcBreadcrumbItem("Line items"),
                new ArcBreadcrumbItem("Seat upgrade")
            }
        };

        panel.Children.Add(Labelled("长路径", "层级多时在容器宽度内自动换行。", longTrail));

        return panel;
    }

    private static Control Labelled(string label, string note, Control content)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        panel.Children.Add(content);
        panel.Children.Add(DemoUi.Caption(note));
        return panel;
    }
}
