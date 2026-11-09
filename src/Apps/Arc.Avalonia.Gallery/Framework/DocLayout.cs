using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery;

/// <summary>One scroll target in the document body, mirrored by the right-hand outline.</summary>
public sealed record DocSection(string Key, string Title, Control Anchor);

/// <summary>A built document page plus the anchors its outline points at.</summary>
public sealed record DocView(Control Root, IReadOnlyList<DocSection> Sections);

/// <summary>
/// The Chinese documentation layout for one catalog entry. Eight fixed sections, same order everywhere:
/// summary, purpose, when to use / not to use, completeness preview, relations, contrasts, examples, API.
/// All chrome is built from Arc controls so the gallery documents the library with the library.
/// </summary>
public static class DocLayout
{
    private const double CopyWidth = 860;

    /// <summary>Active copy width; compact layout narrows it to the phone viewport.</summary>
    private static double _copyWidth = CopyWidth;

    /// <summary>
    /// True while the shell or the compact screenshot harness builds a guide page, so pages that own
    /// layout (the home statistics, for example) can stack their columns for a phone viewport.
    /// </summary>
    public static bool CompactLayout { get; set; }

    public static DocView Build(ArcItemPage page, ArcDemoContext demo, Action<string> goToSlug, bool compact = false)
    {
        _copyWidth = compact ? double.PositiveInfinity : CopyWidth;
        var item = page.Item;
        var doc = page.Doc;
        var sections = new List<DocSection>();
        var root = new StackPanel { Spacing = 0, MaxWidth = _copyWidth, HorizontalAlignment = CompactLayout ? HorizontalAlignment.Stretch : HorizontalAlignment.Left };

        root.Children.Add(BuildSummary(page, doc));
        Add(root, sections, "purpose", "用途", null, BuildPurpose(doc));
        Add(root, sections, "when", "何时使用 / 何时不用", null, BuildWhenToUse(doc));
        Add(root, sections, "preview", "预览", "覆盖全部变体、尺寸与状态；切换主题不会丢失当前状态。",
            BuildPreview(page, demo, doc));
        Add(root, sections, "relations", "与其他组件的关系", "同一次选择里最常被一起考虑的条目。",
            BuildRelations(item, doc, goToSlug));
        Add(root, sections, "contrast", "与相似组件的区别", "为什么选它而不是另一个。",
            BuildContrasts(item, doc, goToSlug));
        Add(root, sections, "examples", "用法示例", "可直接复制；示例只做展示，不参与编译。",
            BuildExamples(doc));
        Add(root, sections, "api", item.Kind == "block" ? "组成依赖" : "API", null,
            BuildApi(item, doc, goToSlug));

        return new DocView(root, sections);
    }

    private static Control BuildSummary(ArcItemPage page, ArcDoc? doc)
    {
        var item = page.Item;
        var panel = new StackPanel { Spacing = 10 };

        var breadcrumb = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        breadcrumb.Children.Add(new TextBlock { Text = $"{item.KindLabel} / {item.Category}", Classes = { "arc-caption" } });
        breadcrumb.Children.Add(new ArcBadge
        {
            Content = item.IsPro ? "Pro" : "Free",
            Tone = item.IsPro ? ArcBadgeTone.Accent : ArcBadgeTone.Neutral
        });
        panel.Children.Add(breadcrumb);

        var title = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(doc?.NameZh) ? item.Title : $"{item.Title}  {doc!.NameZh}",
            Classes = { "arc-title-sm" },
            TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(title);

        if (!string.IsNullOrWhiteSpace(doc?.Summary))
        {
            panel.Children.Add(new TextBlock
            {
                Text = doc.Summary,
                Classes = { "arc-body", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = _copyWidth
            });
        }

        var meta = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        meta.Children.Add(Meta("slug", item.Slug));
        if (doc is not null && !string.IsNullOrWhiteSpace(doc.Api.Type))
        {
            meta.Children.Add(Meta("控件", doc.Api.Type!.Split('.').LastOrDefault() ?? doc.Api.Type!));
        }

        meta.Children.Add(Meta("文档", doc is null ? "未提供" : ShortSource(doc.Source)));
        panel.Children.Add(meta);

        return panel;
    }

    private static Control Meta(string label, string value)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 18, 6)
        };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        row.Children.Add(new TextBlock { Text = value, Classes = { "arc-mono", "arc-caption" } });
        return row;
    }

    private static string ShortSource(string source)
    {
        var index = source.IndexOf(".Docs.", StringComparison.Ordinal);
        return index > 0 ? source[..index] + "/Docs/" + source[(index + ".Docs.".Length)..] : source;
    }

    private static Control BuildPurpose(ArcDoc? doc)
    {
        if (doc is null || doc.Purpose.Count == 0)
        {
            return Missing("用途文档尚未编写。");
        }

        var panel = new StackPanel { Spacing = 12 };
        foreach (var paragraph in doc.Purpose)
        {
            panel.Children.Add(Paragraph(paragraph));
        }

        return panel;
    }

    private static Control BuildWhenToUse(ArcDoc? doc)
    {
        if (doc is null)
        {
            return Missing("使用场景尚未编写。");
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        grid.Children.Add(WhenColumn("何时使用", doc.WhenToUse, ArcTokens.Success, 0));
        var avoid = WhenColumn("何时不用", doc.WhenNotToUse, ArcTokens.Danger, 1);
        avoid.Margin = new Thickness(20, 0, 0, 0);
        grid.Children.Add(avoid);
        return grid;
    }

    private static Control WhenColumn(string title, IReadOnlyList<string> bullets, string tone, int column)
    {
        var panel = new StackPanel { Spacing = 8 };
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        heading.Children.Add(new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            VerticalAlignment = VerticalAlignment.Center,
            Background = DemoUi.Brush(tone)
        });
        heading.Children.Add(new TextBlock { Text = title, Classes = { "arc-label" } });
        panel.Children.Add(heading);

        if (bullets.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "（未填写）", Classes = { "arc-body-sm", "arc-muted" } });
        }

        foreach (var bullet in bullets)
        {
            panel.Children.Add(Bullet(bullet));
        }

        Grid.SetColumn(panel, column);
        return panel;
    }

    private static Control BuildPreview(ArcItemPage page, ArcDemoContext demo, ArcDoc? doc)
    {
        var panel = new StackPanel { Spacing = 12 };

        var chips = new WrapPanel();
        if (doc is not null)
        {
            foreach (var variant in doc.Variants)
            {
                chips.Children.Add(VariantChip(variant));
            }
        }

        if (chips.Children.Count > 0)
        {
            panel.Children.Add(chips);
        }

        Control preview;
        try
        {
            preview = page.BuildPreview(demo);
        }
        catch (Exception exception)
        {
            preview = new TextBlock
            {
                Text = "预览构建失败：" + exception.Message,
                Classes = { "arc-body-sm" },
                Foreground = DemoUi.Brush(ArcTokens.Danger),
                TextWrapping = TextWrapping.Wrap
            };
        }

        if (page.DemoOnCanvas)
        {
            var stage = DemoUi.Canvas(preview, _copyWidth < CopyWidth ? 16 : 24);
            panel.Children.Add(WrapPreview(stage));
        }
        else
        {
            // Page-level blocks are composed at gallery width; let the reader pan instead of clipping them.
            panel.Children.Add(new ScrollViewer
            {
                Content = preview,
                HorizontalScrollBarVisibility = _copyWidth < CopyWidth ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                IsScrollChainingEnabled = false
            });
        }

        return panel;
    }

    /// <summary>
    /// Compact pages keep a fixed-width preview inside a horizontally scrollable strip (scrollbar hidden) so
    /// the document column itself never grows past the phone viewport.
    /// </summary>
    private static Control WrapPreview(Control preview)
    {
        if (_copyWidth >= CopyWidth)
        {
            return preview;
        }

        return new ScrollViewer
        {
            Content = preview,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsScrollChainingEnabled = false
        };
    }

    private static Control VariantChip(ArcDocVariant variant)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(new TextBlock { Text = variant.Label, Classes = { "arc-caption", "arc-medium" } });
        if (!string.IsNullOrWhiteSpace(variant.Note))
        {
            row.Children.Add(new TextBlock { Text = "· " + variant.Note, Classes = { "arc-caption" } });
        }

        return new Border
        {
            Padding = new Thickness(10, 3),
            Margin = new Thickness(0, 0, 8, 8),
            CornerRadius = new CornerRadius(999),
            BorderThickness = new Thickness(1),
            BorderBrush = DemoUi.Brush(ArcTokens.Border),
            Child = row
        };
    }

    private static Control BuildRelations(ArcCatalogItem item, ArcDoc? doc, Action<string> goToSlug)
    {
        var groups = new List<(string Title, List<(string Slug, string Note, string Type)> Entries)>();

        void AddGroup(string title, IEnumerable<(string Slug, string Note, string Type)> entries)
        {
            var list = entries.ToList();
            if (list.Count > 0)
            {
                groups.Add((title, list));
            }
        }

        if (doc is not null)
        {
            AddGroup("替代组件", doc.Relations.Where(r => r.Type == ArcRelationType.Alternative)
                .Select(r => (r.Target, r.Note, "alternative")));
            AddGroup("互补组件", doc.Relations.Where(r => r.Type == ArcRelationType.Complement)
                .Select(r => (r.Target, r.Note, "complement")));
            AddGroup("组合使用", doc.Relations.Where(r => r.Type == ArcRelationType.Uses)
                .Select(r => (r.Target, r.Note, "uses")));
            AddGroup("进阶版本", doc.Relations.Where(r => r.Type == ArcRelationType.Extends)
                .Select(r => (r.Target, r.Note, "extends")));
        }

        // Reverse edges are derived so a relation is authored once, on the more specific side.
        var reverse = new List<(string Tier, (string Slug, string Note, string Type) Entry)>();
        foreach (var other in ArcDocStore.All.Values)
        {
            if (string.Equals(other.Slug, item.Slug, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var relation in other.Relations.Where(r =>
                         string.Equals(r.Target, item.Slug, StringComparison.Ordinal)))
            {
                reverse.Add(relation.Type switch
                {
                    ArcRelationType.Uses => ("被使用", (other.Slug, relation.Note, "used-by")),
                    ArcRelationType.Extends => ("基础版本", (other.Slug, relation.Note, "base-of")),
                    _ => ("互相关联", (other.Slug, relation.Note, "mutual"))
                });
            }
        }

        AddGroup("被使用", reverse.Where(entry => entry.Tier == "被使用").Select(entry => entry.Entry));
        AddGroup("基础版本", reverse.Where(entry => entry.Tier == "基础版本").Select(entry => entry.Entry));
        AddGroup("互相关联", reverse.Where(entry => entry.Tier == "互相关联").Select(entry => entry.Entry));

        if (groups.Count == 0)
        {
            return Missing("关系尚未编写。");
        }

        var panel = new StackPanel { Spacing = 18 };
        foreach (var (title, entries) in groups)
        {
            var section = new StackPanel { Spacing = 8 };
            section.Children.Add(new TextBlock { Text = title, Classes = { "arc-caption", "arc-medium" } });
            foreach (var (slug, note, _) in entries.DistinctBy(entry => entry.Slug))
            {
                section.Children.Add(RelationCard(slug, note, goToSlug));
            }

            panel.Children.Add(section);
        }

        return panel;
    }

    private static Control RelationCard(string slug, string note, Action<string> goToSlug)
    {
        var target = ArcCatalog.Get(slug);
        var targetDoc = ArcDocStore.Get(slug);
        var title = target?.Title ?? slug;
        var subtitle = targetDoc?.NameZh;

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(subtitle) ? title : $"{title}  {subtitle}",
            Classes = { "arc-body-sm", "arc-medium" }
        });
        if (!string.IsNullOrWhiteSpace(note))
        {
            text.Children.Add(new TextBlock
            {
                Text = note,
                Classes = { "arc-caption" },
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 620,
                HorizontalAlignment = HorizontalAlignment.Left
            });
        }

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.Children.Add(text);
        var icon = new ArcIcon
        {
            Icon = "arrow-right",
            Size = 15,
            Foreground = DemoUi.Brush(ArcTokens.TextMuted),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 1);
        row.Children.Add(icon);

        var card = new ArcCard { Content = row, Padding = new Thickness(16, 12), Interactive = true, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        card.Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand);
        card.Tapped += (_, _) => goToSlug(slug);
        return card;
    }

    private static Control BuildContrasts(ArcCatalogItem item, ArcDoc? doc, Action<string> goToSlug)
    {
        if (doc is null || doc.Contrasts.Count == 0)
        {
            return Missing("相似组件对照尚未编写。");
        }

        var panel = new StackPanel { Spacing = 22 };
        foreach (var contrast in doc.Contrasts)
        {
            var target = ArcCatalog.Get(contrast.Target);
            var title = string.IsNullOrWhiteSpace(contrast.Title)
                ? $"{item.Title} 与 {target?.Title ?? contrast.Target}"
                : contrast.Title;

            var block = new StackPanel { Spacing = 10 };
            var heading = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                VerticalAlignment = VerticalAlignment.Center
            };
            heading.Children.Add(new TextBlock { Text = title, Classes = { "arc-label" } });
            var link = new ArcButton
            {
                Content = target?.Title ?? contrast.Target,
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                FontSize = 12
            };
            link.Click += (_, _) => goToSlug(contrast.Target);
            heading.Children.Add(link);
            block.Children.Add(heading);

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("160,*,*"),
                RowDefinitions = new RowDefinitions()
            };
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var headers = new[] { "对比维度", item.Title, target?.Title ?? contrast.Target };
            for (var column = 0; column < headers.Length; column++)
            {
                var cell = new TextBlock
                {
                    Text = headers[column],
                    Classes = { "arc-caption", "arc-medium" },
                    Margin = new Thickness(12, 8)
                };
                Grid.SetColumn(cell, column);
                grid.Children.Add(cell);
            }

            for (var row = 0; row < contrast.Rows.Count; row++)
            {
                var entry = contrast.Rows[row];
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var values = new[] { entry.Dimension, entry.Self, entry.Other };
                for (var column = 0; column < values.Length; column++)
                {
                    var cell = new TextBlock
                    {
                        Text = values[column],
                        Classes = { "arc-body-sm", column == 0 ? "arc-secondary" : "arc-body-sm" },
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(12, 10)
                    };
                    Grid.SetRow(cell, row + 1);
                    Grid.SetColumn(cell, column);
                    grid.Children.Add(cell);
                }
            }

            var table = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = DemoUi.Brush(ArcTokens.Border),
                CornerRadius = new CornerRadius(18),
                ClipToBounds = true,
                Child = grid
            };
            block.Children.Add(table);
            panel.Children.Add(block);
        }

        return panel;
    }

    private static Control BuildExamples(ArcDoc? doc)
    {
        if (doc is null || doc.Examples.Count == 0)
        {
            return Missing("用法示例尚未编写。");
        }

        var panel = new StackPanel { Spacing = 16 };
        foreach (var example in doc.Examples)
        {
            var block = new StackPanel { Spacing = 8 };
            if (!string.IsNullOrWhiteSpace(example.Title))
            {
                block.Children.Add(new TextBlock { Text = example.Title, Classes = { "arc-label" } });
            }

            block.Children.Add(new ArcCodeBlock
            {
                Code = example.Code,
                Language = example.Language,
                Filename = example.Title,
                MaxLines = 24
            });
            panel.Children.Add(block);
        }

        return panel;
    }

    private static Control BuildApi(ArcCatalogItem item, ArcDoc? doc, Action<string> goToSlug)
    {
        if (item.Kind == "block")
        {
            var uses = doc?.Relations.Where(relation => relation.Type == ArcRelationType.Uses).ToList() ?? new();
            if (uses.Count == 0)
            {
                return Missing("组成依赖尚未编写。");
            }

            var panel = new StackPanel { Spacing = 8 };
            foreach (var relation in uses)
            {
                panel.Children.Add(RelationCard(relation.Target, relation.Note, goToSlug));
            }

            return panel;
        }

        if (doc is null || !doc.Api.IsDeclared)
        {
            return Missing("API 尚未声明。");
        }

        var type = ArcDocValidator.ResolveType(doc.Api.Type!);
        if (type is null)
        {
            return Missing("API 类型无法解析：" + doc.Api.Type);
        }

        return DocApiTable.Build(type, doc.Api);
    }

    private static Control Paragraph(string text) => new TextBlock
    {
        Text = text,
        Classes = { "arc-body", "arc-secondary" },
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = _copyWidth,
        LineHeight = 26
    };

    private static Control Bullet(string text)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        row.Children.Add(new TextBlock
        {
            Text = "•",
            Classes = { "arc-body-sm", "arc-muted" },
            Margin = new Thickness(0, 0, 8, 0)
        });
        var body = new TextBlock
        {
            Text = text,
            Classes = { "arc-body-sm", "arc-secondary" },
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(body, 1);
        row.Children.Add(body);
        return row;
    }

    private static Control Missing(string text) => new Border
    {
        Padding = new Thickness(16),
        CornerRadius = new CornerRadius(18),
        BorderThickness = new Thickness(1, 0, 0, 0),
        BorderBrush = DemoUi.Brush(ArcTokens.Warning),
        Background = DemoUi.Brush(ArcTokens.WarningSubtle),
        Child = new TextBlock
        {
            Text = text + "（审计会标记这一项）",
            Classes = { "arc-body-sm" },
            Foreground = DemoUi.Brush(ArcTokens.Warning),
            TextWrapping = TextWrapping.Wrap
        }
    };

    private static void Add(
        StackPanel root,
        List<DocSection> sections,
        string key,
        string title,
        string? subtitle,
        Control body)
    {
        var section = new StackPanel { Spacing = 12, Margin = new Thickness(0, 40, 0, 0) };
        var heading = new TextBlock { Text = title, Classes = { "arc-heading" } };
        section.Children.Add(heading);
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            section.Children.Add(new TextBlock
            {
                Text = subtitle,
                Classes = { "arc-body-sm", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = _copyWidth
            });
        }

        section.Children.Add(body);
        root.Children.Add(section);
        sections.Add(new DocSection(key, title, heading));
    }
}


