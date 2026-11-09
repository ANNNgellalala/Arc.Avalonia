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
/// The documentation home: what this gallery is, how the documentation is organised, and live progress for
/// the 294-entry rewrite. Every link is a real navigation through the shell.
/// </summary>
public sealed class HomePage : ArcPage
{
    public override string Title => "文档首页";

    public override string Group => "Overview";

    public override string? Description => null;

    public override int Order => 0;

    public override Control Build(ArcDemoContext context)
    {
        var items = ArcCatalog.AllItems;
        var docs = ArcDocStore.Count;
        var pages = ArcDocValidator.ItemPages().Count;
        var errors = ArcDocValidator.Validate().Count(issue => issue.IsError);

        var root = new StackPanel { Spacing = 0, MaxWidth = 860, HorizontalAlignment = HorizontalAlignment.Left };

        var eyebrow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        eyebrow.Children.Add(new ArcBadge { Content = "文档平台", Tone = ArcBadgeTone.Accent });
        eyebrow.Children.Add(new ArcBadge { Content = "中文", Tone = ArcBadgeTone.Neutral });
        root.Children.Add(eyebrow);

        root.Children.Add(new TextBlock
        {
            Text = "Arc for Avalonia",
            Classes = { "arc-title" },
            Margin = new Thickness(0, 14, 0, 0)
        });
        root.Children.Add(new TextBlock
        {
            Text = "一个用 Avalonia 组件自身搭建的中文组件文档站：每个条目一页，讲清用途、何时使用、与其他组件的关系、与相似组件的区别，并在同一页里给出可交互的完整性预览与可复制的用法示例。",
            Classes = { "arc-body", "arc-secondary" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 760,
            LineHeight = 26,
            Margin = new Thickness(0, 12, 0, 0)
        });

        root.Children.Add(Stats(items.Count, docs, pages, errors));
        root.Children.Add(Section("怎么读这份文档", new[]
        {
            "侧栏按「组件 → 特殊组件 → 区块」组织，与 uiarc.dev 的分类一致；展开分类后选择条目。",
            "每个条目页固定八个区块：摘要、用途、何时使用/不用、预览、组件关系、相似组件区别、用法示例、API。",
            "预览区覆盖该条目的全部变体与状态；切换右上角主题或强调色时，预览会一起变化。",
            "关系卡片与对照表都可以点击，直接跳到相关条目。"
        }));
        root.Children.Add(QuickStart());
        root.Children.Add(Section("当前进度", new[]
        {
            $"目录条目 {items.Count} 个：组件 128、特殊组件 55、区块 111。",
            $"已编写中文文档 {docs} 篇；已建条目页 {pages} 个；仍有 {errors} 个阻断项。",
            "文档状态页会逐条列出缺失章节、悬空关系、未覆盖的预览状态，并可直接跳转。"
        }));

        var status = new ArcButton
        {
            Content = "打开文档状态页",
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Md,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 28, 0, 0)
        };
        status.Click += (_, _) => DocNavigation.Status();
        root.Children.Add(status);

        return root;
    }

    private static Control Stats(int items, int docs, int pages, int errors)
    {
        if (DocLayout.CompactLayout)
        {
            return CompactStats(items, docs, pages, errors);
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            Margin = new Thickness(0, 30, 0, 6)
        };
        var values = new (string Label, string Value, string Tone)[]
        {
            ("目录条目", items.ToString(), ArcTokens.Foreground),
            ("中文文档", docs.ToString(), ArcTokens.Accent),
            ("条目页", pages.ToString(), ArcTokens.Foreground),
            ("阻断项", errors.ToString(), errors == 0 ? ArcTokens.Success : ArcTokens.Danger)
        };

        for (var column = 0; column < values.Length; column++)
        {
            var (label, value, tone) = values[column];
            var card = new ArcCard
            {
                Padding = new Thickness(18),
                Margin = new Thickness(column == 0 ? 0 : 8, 0, column == values.Length - 1 ? 0 : 8, 0),
                Content = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = value,
                            Classes = { "arc-title-sm" },
                            Foreground = DemoUi.Brush(tone)
                        },
                        new TextBlock { Text = label, Classes = { "arc-caption" } }
                    }
                }
            };
            Grid.SetColumn(card, column);
            grid.Children.Add(card);
        }

        return grid;
    }

    /// <summary>Two-column counter stack for the phone layout; desktop keeps the four-column grid.</summary>
    private static Control CompactStats(int items, int docs, int pages, int errors)
    {
        var wrap = new WrapPanel { Margin = new Thickness(0, 30, 0, 6) };
        var values = new (string Label, string Value, string Tone)[]
        {
            ("目录条目", items.ToString(), ArcTokens.Foreground),
            ("中文文档", docs.ToString(), ArcTokens.Accent),
            ("条目页", pages.ToString(), ArcTokens.Foreground),
            ("阻断项", errors.ToString(), errors == 0 ? ArcTokens.Success : ArcTokens.Danger)
        };

        foreach (var (label, value, tone) in values)
        {
            wrap.Children.Add(new ArcCard
            {
                Width = 172,
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 8, 8),
                Content = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = value,
                            Classes = { "arc-title-sm" },
                            Foreground = DemoUi.Brush(tone)
                        },
                        new TextBlock { Text = label, Classes = { "arc-caption" } }
                    }
                }
            });
        }

        return wrap;
    }

    private static Control Section(string title, IReadOnlyList<string> bullets)
    {
        var panel = new StackPanel { Spacing = 7, Margin = new Thickness(0, 34, 0, 0) };
        panel.Children.Add(new TextBlock { Text = title, Classes = { "arc-heading" } });
        foreach (var bullet in bullets)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "•  " + bullet,
                Classes = { "arc-body-sm", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 800
            });
        }

        return panel;
    }

    private static Control QuickStart()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(0, 34, 0, 0) };
        panel.Children.Add(new TextBlock { Text = "从这里开始", Classes = { "arc-heading" } });
        var row = new WrapPanel();
        foreach (var slug in new[] { "button", "input", "select", "tabs", "dialog", "data-grid" })
        {
            var item = ArcCatalog.Get(slug);
            if (item is null)
            {
                continue;
            }

            var chip = new ArcButton
            {
                Content = item.Title,
                Variant = ArcButtonVariant.Secondary,
                Size = ArcButtonSize.Sm,
                Margin = new Thickness(0, 0, 10, 10)
            };
            chip.Click += (_, _) => DocNavigation.Go(slug);
            row.Children.Add(chip);
        }

        panel.Children.Add(row);
        return panel;
    }
}
