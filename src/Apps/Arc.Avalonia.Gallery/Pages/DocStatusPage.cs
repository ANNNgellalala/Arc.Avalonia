using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery.Pages;

/// <summary>
/// Coverage and quality report for the 294-entry documentation rewrite: one row per catalog entry with its
/// blocking errors and warnings, and a jump link. The same checks run from <c>--audit</c>.
/// </summary>
public sealed class DocStatusPage : ArcPage
{
    private static IReadOnlyList<ArcDocIssue>? _report;

    public override string Title => "文档状态";

    public override string Group => "文档状态";

    public override string? Description => "逐条列出缺失章节、悬空关系、未覆盖的预览状态与示例缺失，点击可直接跳到对应条目。";

    public override int Order => 1;

    public override Control Build(ArcDemoContext context)
    {
        var report = GetReport();
        var errors = report.Where(issue => issue.IsError).ToList();
        var warnings = report.Where(issue => !issue.IsError).ToList();
        var bySlug = report.GroupBy(issue => issue.Slug, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var root = new StackPanel { Spacing = 0, MaxWidth = 980, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(new TextBlock { Text = "文档覆盖率与质量", Classes = { "arc-title-sm" } });
        root.Children.Add(new TextBlock
        {
            Text = "审计同时运行在 --audit：阻断项不为 0 时命令以非零退出。表格里的每一行都可以点击跳转。",
            Classes = { "arc-body-sm", "arc-secondary" },
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var summary = new WrapPanel { Margin = new Thickness(0, 24, 0, 24) };
        summary.Children.Add(SummaryCard("目录条目", ArcCatalog.AllItems.Count.ToString(), ArcTokens.Foreground));
        summary.Children.Add(SummaryCard("已写中文文档", ArcDocStore.Count.ToString(), ArcTokens.Accent));
        summary.Children.Add(SummaryCard("阻断错误", errors.Count.ToString(), errors.Count == 0 ? ArcTokens.Success : ArcTokens.Danger));
        summary.Children.Add(SummaryCard("警告", warnings.Count.ToString(), warnings.Count == 0 ? ArcTokens.Success : ArcTokens.Warning));
        root.Children.Add(summary);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("92,*,*") };
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        AddHeader(grid, 0, "状态", "条目", "问题");
        var row = 1;
        foreach (var item in ArcCatalog.AllItems)
        {
            var issues = bySlug.TryGetValue(item.Slug, out var list) ? list : null;
            var status = issues is null
                ? ("通过", ArcTokens.Success)
                : issues.Any(issue => issue.IsError)
                    ? ("阻断", ArcTokens.Danger)
                    : ("警告", ArcTokens.Warning);

            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddCell(grid, row, 0, new TextBlock
            {
                Text = status.Item1,
                Classes = { "arc-caption", "arc-medium" },
                Foreground = DemoUi.Brush(status.Item2),
                Margin = new Thickness(12, 8)
            });

            var link = new ArcButton
            {
                Content = item.Title,
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(6, 0),
                MinHeight = 28,
                FontSize = 12.5
            };
            link.Click += (_, _) => DocNavigation.Go(item.Slug);
            AddCell(grid, row, 1, link);

            var message = issues is null
                ? "文档完整"
                : string.Join("；", issues.Select(issue => issue.Message).Distinct().Take(3));
            AddCell(grid, row, 2, new TextBlock
            {
                Text = message,
                Classes = { "arc-caption" },
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12, 8)
            });
            row++;
        }

        root.Children.Add(new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = DemoUi.Brush(ArcTokens.Border),
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            Child = grid
        });

        return root;
    }

    private static IReadOnlyList<ArcDocIssue> GetReport() => _report ??= ArcDocValidator.Validate();

    private static Control SummaryCard(string label, string value, string tone)
    {
        var card = new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(0, 0, 12, 8),
            MinWidth = 130
        };
        card.Children.Add(new TextBlock
        {
            Text = value,
            Classes = { "arc-title-sm" },
            Foreground = DemoUi.Brush(tone)
        });
        card.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        return card;
    }

    private static void AddHeader(Grid grid, int row, string first, string second, string third)
    {
        AddCell(grid, row, 0, new TextBlock { Text = first, Classes = { "arc-caption", "arc-medium" }, Margin = new Thickness(12, 8) });
        AddCell(grid, row, 1, new TextBlock { Text = second, Classes = { "arc-caption", "arc-medium" }, Margin = new Thickness(12, 8) });
        AddCell(grid, row, 2, new TextBlock { Text = third, Classes = { "arc-caption", "arc-medium" }, Margin = new Thickness(12, 8) });
    }

    private static void AddCell(Grid grid, int row, int column, Control cell)
    {
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }
}
