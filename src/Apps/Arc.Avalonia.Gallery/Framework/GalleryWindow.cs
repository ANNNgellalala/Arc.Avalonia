using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Services;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery;

/// <summary>
/// The documentation shell: a persistent three-column layout (navigation, document body, outline). Navigating
/// swaps the body only, so a built document keeps its visual parent and can be shown again without rebuilding.
/// The sidebar materialises one category's rows on first expand; selection moves between existing rows instead
/// of rebuilding the tree.
/// </summary>
public sealed class GalleryWindow : ArcWindow
{
    private const int CacheLimit = 16;

    private readonly IReadOnlyList<ArcPage> _pages = ArcPageRegistry.Discover();
    private readonly Dictionary<string, ArcItemPage> _itemPages = new(StringComparer.Ordinal);
    private readonly List<ArcPage> _guides = new();
    private readonly Dictionary<string, ArcCatalogItem> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _itemOrder = new(StringComparer.Ordinal);

    private readonly Dictionary<string, Control> _viewCache = new(StringComparer.Ordinal);
    private readonly List<string> _cacheOrder = new();
    private readonly Dictionary<string, ArcButton> _navButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StackPanel> _groupItems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _expandedGroups = new(StringComparer.Ordinal);

    private Grid? _rootGrid;
    private Border? _sidebarHost;
    private ScrollViewer? _contentScroller;
    private Border? _contentHost;
    private Border? _tocHost;
    private ScrollViewer? _tocScroller;
    private StackPanel? _tocStack;
    private StackPanel? _navHost;
    private TextBox? _searchBox;

    private ArcButton? _selectedNav;
    private IReadOnlyList<DocSection> _sections = Array.Empty<DocSection>();
    private readonly Dictionary<string, ArcButton> _tocButtons = new(StringComparer.Ordinal);
    private string _activeTocKey = string.Empty;

    private string _search = string.Empty;
    private string _tierFilter = "all";
    private string? _currentSlug;
    private ArcPage? _currentPage;

    public GalleryWindow(string? initialSlug = null)
    {
        Title = "Arc for Avalonia · 中文文档";
        Width = 1460;
        Height = 940;
        MinWidth = 1120;
        MinHeight = 700;
        Background = DemoUi.Brush(ArcTokens.Background);

        foreach (var item in ArcCatalog.AllItems)
        {
            _items[item.Slug] = item;
            _itemOrder[item.Slug] = _itemOrder.Count;
        }

        foreach (var page in _pages)
        {
            if (page is ArcItemPage itemPage)
            {
                _itemPages.TryAdd(itemPage.ItemSlug, itemPage);
            }
            else
            {
                _guides.Add(page);
            }
        }

        BuildShell();
        DocNavigation.Navigate = ShowSlug;
        DocNavigation.OpenStatus = ShowStatusPage;

        if (!string.IsNullOrWhiteSpace(initialSlug) && _items.ContainsKey(initialSlug))
        {
            ShowSlug(initialSlug);
        }
        else
        {
            ShowHome();
        }
    }

    public string? CurrentSlug => _currentSlug;

    public int DocumentCount => ArcDocStore.Count;

    public IReadOnlyList<ArcItemPage> ItemPages =>
        _itemPages.Values.OrderBy(page => _itemOrder.TryGetValue(page.ItemSlug, out var order) ? order : int.MaxValue).ToList();

    private void BuildShell()
    {
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("286,*,224") };
        _rootGrid = root;

        _sidebarHost = new Border
        {
            Background = DemoUi.Brush(ArcTokens.Surface),
            BorderBrush = DemoUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(0, 0, 1, 0)
        };
        root.Children.Add(_sidebarHost);

        _contentScroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        _contentScroller.ScrollChanged += (_, _) => UpdateActiveSection();
        _contentHost = new Border
        {
            Padding = new Thickness(56, 44, 56, 56),
            Child = _contentScroller
        };
        Grid.SetColumn(_contentHost, 1);
        root.Children.Add(_contentHost);

        _tocScroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(4, 44, 20, 24)
        };
        _tocStack = new StackPanel { Spacing = 1 };
        _tocScroller.Content = _tocStack;
        _tocHost = new Border
        {
            BorderBrush = DemoUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = _tocScroller
        };
        Grid.SetColumn(_tocHost, 2);
        root.Children.Add(_tocHost);

        PageHost.Content = root;
        RebuildSidebar();
    }

    // ---------------------------------------------------------------- sidebar

    public void RebuildSidebar()
    {
        if (_sidebarHost is null)
        {
            return;
        }

        var stack = new StackPanel { Spacing = 2 };

        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        brand.Children.Add(new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(10),
            Background = DemoUi.Brush(ArcTokens.Accent),
            Child = new TextBlock
            {
                Text = "A",
                Foreground = DemoUi.Brush(ArcTokens.AccentForeground),
                FontSize = 14,
                FontWeight = FontWeight.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });
        var brandText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        brandText.Children.Add(new TextBlock { Text = "Arc for Avalonia", FontSize = 14, FontWeight = FontWeight.Medium });
        brandText.Children.Add(new TextBlock { Text = "组件文档", Classes = { "arc-caption" } });
        brand.Children.Add(brandText);
        stack.Children.Add(new Border { Child = brand, Margin = new Thickness(8, 0, 0, 14) });

        var filters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(8, 0, 0, 10) };
        filters.Children.Add(TierButton("全部", "all"));
        filters.Children.Add(TierButton("免费", "free"));
        filters.Children.Add(TierButton("Pro", "pro"));
        stack.Children.Add(filters);

        _searchBox = new TextBox
        {
            Text = _search,
            PlaceholderText = "搜索组件、中文名或文档内容",
            Margin = new Thickness(8, 0, 8, 12),
            MinHeight = 36,
            FontSize = 13,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 0, 12, 0)
        };
        _searchBox.TextChanged += (_, _) =>
        {
            _search = _searchBox.Text ?? string.Empty;
            RefreshNavigation();
        };
        stack.Children.Add(_searchBox);

        _navHost = new StackPanel { Spacing = 1 };
        stack.Children.Add(_navHost);

        stack.Children.Add(SectionHeading("外观"));
        var themeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(10, 0, 0, 6) };
        themeRow.Children.Add(ThemeButton("浅色", ArcColorScheme.Light));
        themeRow.Children.Add(ThemeButton("深色", ArcColorScheme.Dark));
        stack.Children.Add(themeRow);

        var accents = new WrapPanel { Margin = new Thickness(10, 6, 0, 4) };
        foreach (var accent in Enum.GetValues<ArcAccent>())
        {
            accents.Children.Add(AccentChip(accent));
        }

        stack.Children.Add(accents);
        stack.Children.Add(new TextBlock
        {
            Text = "294 个条目 · 中文文档 · Avalonia 12 / net10.0",
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10, 18, 10, 8)
        });

        _sidebarHost.Child = new ScrollViewer
        {
            Content = stack,
            Padding = new Thickness(12, 22, 12, 16),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        RefreshNavigation();
    }

    private Control TierButton(string label, string tier)
    {
        var active = _tierFilter == tier;
        var button = new ArcButton
        {
            Content = label,
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Padding = new Thickness(10, 0),
            MinHeight = 30,
            FontSize = 12,
            FontWeight = active ? FontWeight.Medium : FontWeight.Normal
        };
        if (active)
        {
            button.Background = DemoUi.Brush(ArcTokens.AccentSubtle);
            button.Foreground = DemoUi.Brush(ArcTokens.AccentStrong);
        }

        button.Click += (_, _) =>
        {
            _tierFilter = tier;
            RebuildSidebar();
        };
        return button;
    }

    private void RefreshNavigation()
    {
        if (_navHost is null)
        {
            return;
        }

        _navButtons.Clear();
        _groupItems.Clear();
        _selectedNav = null;
        _navHost.Children.Clear();

        var query = _search.Trim();
        if (query.Length > 0)
        {
            var hits = DocSearch.Query(query, _tierFilter);
            _navHost.Children.Add(Caption($"搜索结果 · {hits.Count}"));
            if (hits.Count == 0)
            {
                _navHost.Children.Add(new TextBlock
                {
                    Text = "没有匹配的条目。试试组件名或中文名，例如 Button、按钮、选择器。",
                    Classes = { "arc-caption" },
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 4, 10, 0)
                });
                return;
            }

            foreach (var hit in hits.Take(60))
            {
                _navHost.Children.Add(ItemButton(hit.Item, hit.Snippet));
            }

            if (hits.Count > 60)
            {
                _navHost.Children.Add(Caption($"还有 {hits.Count - 60} 条，继续输入以缩小范围"));
            }

            HighlightSelection();
            return;
        }

        _navHost.Children.Add(NavButton("首页", () => ShowHome(), null, null));

        var guideTitles = new HashSet<string>(StringComparer.Ordinal)
        {
            "Foundations", "Tokens", "Icons", "Motion", "Kit proof", "Virtualized list"
        };
        var guides = _guides
            .Where(page => page is not Pages.HomePage and not Pages.DocStatusPage)
            .OrderBy(page => page.Order)
            .ToList();

        var realGuides = guides.Where(page => guideTitles.Contains(page.Title)).ToList();
        if (realGuides.Count > 0)
        {
            _navHost.Children.Add(SectionHeading("指南"));
            foreach (var page in realGuides)
            {
                _navHost.Children.Add(NavButton(page.Title, () => ShowPage(page), null, page.Title));
            }
        }

        foreach (var section in OrderedSections())
        {
            _navHost.Children.Add(SectionHeading(section.Name));
            foreach (var group in section.Groups)
            {
                _navHost.Children.Add(CategoryRow(section.Name, group));
            }
        }

        var legacy = guides.Where(page => !guideTitles.Contains(page.Title)).ToList();
        if (legacy.Count > 0)
        {
            _navHost.Children.Add(SectionHeading("场景示例"));
            foreach (var page in legacy)
            {
                _navHost.Children.Add(NavButton(page.Title, () => ShowPage(page), null, page.Title));
            }
        }

        _navHost.Children.Add(SectionHeading("文档状态"));
        var status = _guides.FirstOrDefault(page => page is Pages.DocStatusPage);
        if (status is not null)
        {
            _navHost.Children.Add(NavButton("覆盖率 / 质量审计", () => ShowPage(status), null, status.Title));
        }

        HighlightSelection();
    }

    private static IEnumerable<ArcCatalogSection> OrderedSections()
    {
        var sections = ArcCatalog.Sections;
        foreach (var name in new[] { "Components", "Special components", "Blocks" })
        {
            var section = sections.FirstOrDefault(candidate => candidate.Name == name);
            if (section is not null)
            {
                yield return section;
            }
        }

        foreach (var section in sections.Where(section =>
                     section.Name is not ("Components" or "Special components" or "Blocks")))
        {
            yield return section;
        }
    }

    private Control CategoryRow(string sectionName, ArcCatalogGroup group)
    {
        var key = sectionName + "/" + group.Name;
        var expanded = _expandedGroups.TryGetValue(key, out var state) && state;

        var headerContent = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Width = 240 };
        headerContent.Children.Add(new TextBlock { Text = group.Name, FontSize = 13 });
        var count = new TextBlock
        {
            Text = group.Items.Count(item => DocSearch.PassesTier(item, _tierFilter)).ToString(),
            Classes = { "arc-caption" },
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(count, 1);
        headerContent.Children.Add(count);

        var header = new ArcButton
        {
            Content = headerContent,
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 0),
            MinHeight = 32,
            FontWeight = expanded ? FontWeight.Medium : FontWeight.Normal
        };

        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(header);

        var itemsHost = new StackPanel { Spacing = 0, IsVisible = expanded };
        if (expanded)
        {
            MaterializeGroup(key, group, itemsHost);
        }

        stack.Children.Add(itemsHost);
        header.Click += (_, _) =>
        {
            var open = !(_expandedGroups.TryGetValue(key, out var current) && current);
            _expandedGroups[key] = open;
            header.FontWeight = open ? FontWeight.Medium : FontWeight.Normal;
            if (open && itemsHost.Children.Count == 0)
            {
                MaterializeGroup(key, group, itemsHost);
            }

            itemsHost.IsVisible = open;
        };

        return stack;
    }

    private void MaterializeGroup(string key, ArcCatalogGroup group, StackPanel host)
    {
        if (_groupItems.ContainsKey(key))
        {
            return;
        }

        _groupItems[key] = host;
        foreach (var item in group.Items.Where(item => DocSearch.PassesTier(item, _tierFilter)))
        {
            host.Children.Add(ItemButton(item, null));
        }
    }

    private Control ItemButton(ArcCatalogItem item, string? snippet)
    {
        var row = new StackPanel { Spacing = 0 };
        row.Children.Add(new TextBlock
        {
            Text = item.Title + (item.IsPro ? "  ·  Pro" : string.Empty),
            FontSize = 12.5,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (!string.IsNullOrWhiteSpace(snippet))
        {
            row.Children.Add(new TextBlock
            {
                Text = snippet,
                Classes = { "arc-caption" },
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2
            });
        }

        var button = new ArcButton
        {
            Content = row,
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(22, 3, 8, 3),
            MinHeight = 30
        };
        _navButtons[item.Slug] = button;
        button.Click += (_, _) => ShowSlug(item.Slug);
        return button;
    }

    private Control NavButton(string label, Action action, string? slug, string? pageTitle)
    {
        var button = new ArcButton
        {
            Content = label,
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 0),
            MinHeight = 32,
            FontSize = 13
        };
        button.Click += (_, _) => action();
        if (slug is not null)
        {
            _navButtons[slug] = button;
        }

        return button;
    }

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        Classes = { "arc-caption" },
        Margin = new Thickness(10, 10, 0, 2)
    };

    private static TextBlock SectionHeading(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeight.Medium,
        Foreground = DemoUi.Brush(ArcTokens.TextSecondary),
        Margin = new Thickness(10, 18, 0, 6)
    };

    private Control ThemeButton(string label, ArcColorScheme scheme)
    {
        var active = ArcThemeManager.Scheme == scheme;
        var button = new ArcButton
        {
            Content = label,
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Padding = new Thickness(10, 0),
            MinHeight = 30,
            FontSize = 12,
            FontWeight = active ? FontWeight.Medium : FontWeight.Normal
        };
        if (active)
        {
            button.Background = DemoUi.Brush(ArcTokens.AccentSubtle);
            button.Foreground = DemoUi.Brush(ArcTokens.AccentStrong);
        }

        button.Click += (_, _) =>
        {
            ArcThemeManager.SetScheme(Application.Current!, scheme);
            RebuildAll();
        };
        return button;
    }

    private Control AccentChip(ArcAccent accent)
    {
        var palette = ArcPalette.Build(ArcThemeManager.Scheme, accent);
        var color = palette[ArcTokens.Accent] is ISolidColorBrush brush ? brush.Color : Colors.Black;
        var active = ArcThemeManager.Accent == accent;
        var outer = new Border
        {
            Padding = new Thickness(3),
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(active ? 1.5 : 0),
            BorderBrush = DemoUi.Brush(ArcTokens.Foreground),
            Child = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(color)
            },
            Margin = new Thickness(0, 0, 6, 6),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        outer.PointerPressed += (_, _) =>
        {
            ArcThemeManager.SetAccent(Application.Current!, accent);
            RebuildAll();
        };
        ToolTip.SetTip(outer, accent.ToString());
        return outer;
    }

    // ---------------------------------------------------------------- navigation

    public void ShowSlug(string slug)
    {
        if (!_items.TryGetValue(slug, out var item))
        {
            return;
        }

        if (_itemPages.TryGetValue(slug, out var page))
        {
            ShowDoc(page);
            return;
        }

        ShowMissing(item);
    }

    public void ShowPage(ArcPage page)
    {
        if (_contentScroller is null)
        {
            return;
        }

        _currentPage = page;
        _currentSlug = null;
        var view = GetPlainPageView(page);
        _contentScroller.Content = view;
        _contentScroller.Offset = default;
        ShowToc(Array.Empty<DocSection>());
        HighlightSelection();
    }

    public void ShowHome()
    {
        var home = _guides.FirstOrDefault(page => page is Pages.HomePage)
                   ?? _guides.OrderBy(page => page.Order).FirstOrDefault();
        if (home is not null)
        {
            ShowPage(home);
        }
    }

    public void Search(string text)
    {
        _search = text;
        if (_searchBox is not null)
        {
            _searchBox.Text = text;
        }

        RefreshNavigation();
    }

    public void RebuildAll()
    {
        Background = DemoUi.Brush(ArcTokens.Background);
        _viewCache.Clear();
        _cacheOrder.Clear();
        RebuildSidebar();
        if (!string.IsNullOrWhiteSpace(_currentSlug))
        {
            ShowSlug(_currentSlug!);
        }
        else if (_currentPage is not null)
        {
            ShowPage(_currentPage);
        }
        else
        {
            ShowHome();
        }
    }

    private void ShowDoc(ArcItemPage page)
    {
        if (_contentScroller is null)
        {
            return;
        }

        var slug = page.ItemSlug;
        _currentSlug = slug;
        _currentPage = page;

        var view = GetDocView(page);
        _contentScroller.Content = view;
        _contentScroller.Offset = default;
        ShowToc(GetSections(page));
        HighlightSelection();
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                _navButtons.TryGetValue(slug, out var button);
                button?.BringIntoView();
            }
            catch
            {
                // Scrolling the sidebar is best effort.
            }
        }, DispatcherPriority.Loaded);
    }

    private void ShowMissing(ArcCatalogItem item)
    {
        if (_contentScroller is null)
        {
            return;
        }

        _currentSlug = item.Slug;
        _currentPage = null;

        var panel = new StackPanel { Spacing = 14, MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new TextBlock { Text = item.Title, Classes = { "arc-title-sm" } });
        panel.Children.Add(new TextBlock { Text = item.Description, Classes = { "arc-body", "arc-secondary" }, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new Border
        {
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(18),
            Background = DemoUi.Brush(ArcTokens.WarningSubtle),
            BorderThickness = new Thickness(1),
            BorderBrush = DemoUi.Brush(ArcTokens.Warning),
            Child = new TextBlock
            {
                Text = "这个条目的中文文档与条目页尚未编写（M2/M3 阶段补齐）。审计会把它列为缺失。",
                Classes = { "arc-body-sm" },
                Foreground = DemoUi.Brush(ArcTokens.Warning),
                TextWrapping = TextWrapping.Wrap
            }
        });
        var statusButton = new ArcButton
        {
            Content = "查看文档覆盖率",
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        statusButton.Click += (_, _) => ShowStatusPage();
        panel.Children.Add(statusButton);

        _contentScroller.Content = panel;
        _contentScroller.Offset = default;
        ShowToc(Array.Empty<DocSection>());
        HighlightSelection();
    }

    private void ShowStatusPage()
    {
        var status = _guides.FirstOrDefault(page => page.Title == "文档状态");
        if (status is not null)
        {
            ShowPage(status);
        }
    }

    private IReadOnlyList<DocSection> GetSections(ArcItemPage page)
    {
        if (page.Doc is null)
        {
            return Array.Empty<DocSection>();
        }

        return _sectionsCache.TryGetValue(page.ItemSlug, out var sections)
            ? sections
            : Array.Empty<DocSection>();
    }

    private readonly Dictionary<string, IReadOnlyList<DocSection>> _sectionsCache = new(StringComparer.Ordinal);

    private Control GetDocView(ArcItemPage page)
    {
        var key = page.ItemSlug;
        if (_viewCache.TryGetValue(key, out var cached))
        {
            Touch(key);
            return cached;
        }

        var demo = new ArcDemoContext(ArcThemeManager.Scheme, ArcThemeManager.Accent);
        var built = DocLayout.Build(page, demo, ShowSlug);
        _sectionsCache[key] = built.Sections;

        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(built.Root);
        stack.Children.Add(BuildFooter(page));
        _viewCache[key] = stack;
        Touch(key);
        Evict();
        return stack;
    }

    private Control GetPlainPageView(ArcPage page)
    {
        var key = "page:" + page.Title;
        if (_viewCache.TryGetValue(key, out var cached))
        {
            Touch(key);
            return cached;
        }

        Control content;
        try
        {
            content = page.Build(new ArcDemoContext(ArcThemeManager.Scheme, ArcThemeManager.Accent));
        }
        catch (Exception exception)
        {
            content = new TextBlock
            {
                Text = "页面构建失败：" + exception.Message,
                Classes = { "arc-body-sm" },
                Foreground = DemoUi.Brush(ArcTokens.Danger),
                TextWrapping = TextWrapping.Wrap
            };
        }

        var stack = new StackPanel { Spacing = 0 };
        if (page is Pages.HomePage or Pages.DocStatusPage)
        {
            stack.Children.Add(content);
            _viewCache[key] = stack;
            Touch(key);
            Evict();
            return stack;
        }

        var header = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 28) };
        header.Children.Add(new TextBlock { Text = page.Title, Classes = { "arc-title-sm" } });
        if (!string.IsNullOrWhiteSpace(page.Description))
        {
            header.Children.Add(new TextBlock
            {
                Text = page.Description,
                Classes = { "arc-body", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 820
            });
        }

        stack.Children.Add(header);
        stack.Children.Add(content);
        _viewCache[key] = stack;
        Touch(key);
        Evict();
        return stack;
    }

    private Control BuildFooter(ArcItemPage page)
    {
        var index = _itemOrder.TryGetValue(page.ItemSlug, out var order) ? order : -1;
        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Margin = new Thickness(0, 60, 0, 0)
        };

        if (index > 0)
        {
            var previous = ArcCatalog.AllItems[index - 1];
            var button = FooterButton("上一个", previous.Title, () => ShowSlug(previous.Slug));
            button.HorizontalAlignment = HorizontalAlignment.Left;
            footer.Children.Add(button);
        }

        if (index >= 0 && index < ArcCatalog.AllItems.Count - 1)
        {
            var next = ArcCatalog.AllItems[index + 1];
            var button = FooterButton("下一个", next.Title, () => ShowSlug(next.Slug));
            button.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(button, 1);
            footer.Children.Add(button);
        }

        return footer;
    }

    private static Control FooterButton(string direction, string title, Action action)
    {
        var content = new StackPanel { Spacing = 2 };
        content.Children.Add(new TextBlock { Text = direction, Classes = { "arc-caption" } });
        content.Children.Add(new TextBlock { Text = title, Classes = { "arc-body-sm", "arc-medium" } });
        var button = new ArcButton
        {
            Content = content,
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            Padding = new Thickness(16, 10)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void Touch(string key)
    {
        _cacheOrder.Remove(key);
        _cacheOrder.Add(key);
    }

    private void Evict()
    {
        while (_cacheOrder.Count > CacheLimit)
        {
            var oldest = _cacheOrder[0];
            _cacheOrder.RemoveAt(0);
            _viewCache.Remove(oldest);
            _sectionsCache.Remove(oldest);
        }
    }

    // ---------------------------------------------------------------- outline

    private void ShowToc(IReadOnlyList<DocSection> sections)
    {
        _sections = sections;
        _tocButtons.Clear();
        _activeTocKey = string.Empty;
        if (_tocStack is not null)
        {
            _tocStack.Children.Clear();
            if (sections.Count > 0)
            {
                _tocStack.Children.Add(new TextBlock
                {
                    Text = "本页目录",
                    Classes = { "arc-caption", "arc-medium" },
                    Margin = new Thickness(12, 0, 0, 8)
                });

                foreach (var section in sections)
                {
                    var target = section;
                    var button = new ArcButton
                    {
                        Content = target.Title,
                        Variant = ArcButtonVariant.Ghost,
                        Size = ArcButtonSize.Sm,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Padding = new Thickness(12, 0),
                        MinHeight = 30,
                        FontSize = 12.5
                    };
                    button.Click += (_, _) =>
                    {
                        try
                        {
                            target.Anchor.BringIntoView();
                            _activeTocKey = target.Key;
                            UpdateTocHighlight();
                        }
                        catch
                        {
                            // Best effort scrolling.
                        }
                    };
                    _tocButtons[target.Key] = button;
                    _tocStack.Children.Add(button);
                }
            }
        }

        if (_tocHost is not null)
        {
            _tocHost.IsVisible = sections.Count > 0;
        }

        if (_rootGrid is not null)
        {
            _rootGrid.ColumnDefinitions[2].Width = sections.Count > 0 ? new GridLength(224) : new GridLength(0);
        }
    }

    private void UpdateActiveSection()
    {
        if (_sections.Count == 0 || _contentScroller is null)
        {
            return;
        }

        var active = _sections[0].Key;
        foreach (var section in _sections)
        {
            var point = section.Anchor.TranslatePoint(new Point(0, 0), _contentScroller);
            if (point is not null && point.Value.Y <= 150)
            {
                active = section.Key;
            }
        }

        if (active == _activeTocKey)
        {
            return;
        }

        _activeTocKey = active;
        UpdateTocHighlight();
    }

    private void UpdateTocHighlight()
    {
        foreach (var (key, button) in _tocButtons)
        {
            if (key == _activeTocKey)
            {
                button.Background = DemoUi.Brush(ArcTokens.AccentSubtle);
                button.Foreground = DemoUi.Brush(ArcTokens.AccentStrong);
                button.FontWeight = FontWeight.Medium;
            }
            else
            {
                button.ClearValue(TemplatedControl.BackgroundProperty);
                button.ClearValue(TemplatedControl.ForegroundProperty);
                button.FontWeight = FontWeight.Normal;
            }
        }
    }

    // ---------------------------------------------------------------- selection

    private void HighlightSelection()
    {
        var slug = _currentSlug;
        if (slug is null)
        {
            if (_selectedNav is not null)
            {
                _selectedNav.ClearValue(TemplatedControl.BackgroundProperty);
                _selectedNav.ClearValue(TemplatedControl.ForegroundProperty);
                _selectedNav.FontWeight = FontWeight.Normal;
                _selectedNav = null;
            }

            return;
        }

        if (!_navButtons.TryGetValue(slug, out var button) || ReferenceEquals(button, _selectedNav))
        {
            return;
        }

        if (_selectedNav is not null)
        {
            _selectedNav.ClearValue(TemplatedControl.BackgroundProperty);
            _selectedNav.ClearValue(TemplatedControl.ForegroundProperty);
            _selectedNav.FontWeight = FontWeight.Normal;
        }

        _selectedNav = button;
        button.Background = DemoUi.Brush(ArcTokens.AccentSubtle);
        button.Foreground = DemoUi.Brush(ArcTokens.AccentStrong);
        button.FontWeight = FontWeight.Medium;
    }
}
