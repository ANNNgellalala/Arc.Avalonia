using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery;

/// <summary>
/// The gallery shell. Navigation mirrors uiarc.dev's documentation sidebar: Components are grouped into the
/// site's categories, followed by Blocks and Special components, with a tier filter and search. Clicking an
/// item opens its item page (or the shared demo page, scrolled to that component's section).
/// </summary>
public sealed class GalleryWindow : Window
{
    private readonly IReadOnlyList<ArcPage> _pages = ArcPageRegistry.Discover();

    // Raw page content, keyed by page title: reused for section lookups (FindHeading) and to avoid rebuilding.
    private readonly Dictionary<string, Control> _builtContent = new(StringComparer.Ordinal);

    // The control tree actually shown in the content host (header + content for grouped pages, the page
    // itself for item pages). A page view belongs to exactly one ScrollViewer for its whole lifetime, so a
    // cached page can be shown again without ever being reparented into a second container.
    private readonly Dictionary<string, Control> _builtViews = new(StringComparer.Ordinal);

    private readonly Dictionary<string, Control> _sectionIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArcPage> _sectionPage = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private readonly List<(ArcCatalogGroup Group, string Section)> _catalogGroups = new();

    // Live sidebar state. Selecting an item updates two existing buttons instead of rebuilding the whole
    // sidebar (the rebuild used to cost 270-570 ms and 28-108 MB per click). A category materialises its item
    // rows once on first expand and afterwards only flips IsVisible.
    private readonly Dictionary<string, StackPanel> _groupContainers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StackPanel> _groupItems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArcButton> _itemButtons = new(StringComparer.Ordinal);

    private Border? _sidebarHost;
    private ScrollViewer? _contentScroller;
    private StackPanel? _sidebarStack;
    private ArcPage? _currentPage;
    private ArcCatalogItem? _selected;
    private ArcButton? _selectedItemButton;
    private string _tierFilter = "all";
    private string _search = string.Empty;

    public GalleryWindow()
    {
        Title = "Arc for Avalonia";
        Width = 1440;
        Height = 900;
        MinWidth = 1024;
        MinHeight = 640;
        Background = DemoUi.Brush(ArcTokens.Background);

        foreach (var section in ArcCatalog.Sections)
        {
            foreach (var group in section.Groups)
            {
                _catalogGroups.Add((group, section.Name));
            }
        }

        BuildShell();
    }

    /// <summary>
    /// Builds the window once: the sidebar and the content host live for the lifetime of the window.
    /// Navigating only swaps the sidebar items and the content view, so a cached page is never re-parented
    /// into a freshly created shell (which is what threw "already has a visual parent").
    /// </summary>
    private void BuildShell()
    {
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*") };

        _sidebarHost = new Border
        {
            Background = DemoUi.Brush(ArcTokens.Surface),
            BorderBrush = DemoUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = BuildSidebar()
        };
        root.Children.Add(_sidebarHost);

        _contentScroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var contentHost = new Border { Padding = new Thickness(56, 44, 56, 40), Child = _contentScroller };
        Grid.SetColumn(contentHost, 1);
        root.Children.Add(contentHost);

        Content = root;

        ShowPage(DefaultPage());
    }

    private ArcPage? DefaultPage() =>
        _pages.Where(page => page is not ArcItemPage && page.Order == 0)
            .OrderBy(page => page.Order)
            .FirstOrDefault();

    /// <summary>Rebuilds the sidebar in place, for example after a navigation selection or a theme swap.</summary>
    private void RebuildSidebar()
    {
        if (_sidebarHost is not null)
        {
            _sidebarHost.Child = BuildSidebar();
        }
    }

    private Control BuildSidebar()
    {
        var stack = new StackPanel { Spacing = 2 };
        _sidebarStack = stack;

        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        brand.Children.Add(new Border
        {
            Width = 28,
            Height = 28,
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
        brandText.Children.Add(new TextBlock { Text = "Arc", FontSize = 15, FontWeight = FontWeight.Medium });
        brandText.Children.Add(new TextBlock { Text = "for Avalonia", Classes = { "arc-caption" } });
        brand.Children.Add(brandText);
        stack.Children.Add(new Border { Child = brand, Margin = new Thickness(8, 0, 0, 14) });

        var filters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(8, 0, 0, 10) };
        filters.Children.Add(TierButton("All", "all"));
        filters.Children.Add(TierButton("Free", "free"));
        filters.Children.Add(TierButton("Pro", "pro"));
        stack.Children.Add(filters);

        var search = new TextBox
        {
            Text = _search,
            Watermark = "Search 294 items",
            Margin = new Thickness(8, 0, 8, 12),
            MinHeight = 34,
            FontSize = 13,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 0, 12, 0)
        };
        search.TextChanged += (_, _) =>
        {
            _search = search.Text ?? string.Empty;
            RefreshNavigation();
        };
        stack.Children.Add(search);

        RefreshNavigation();
        return new ScrollViewer
        {
            Content = stack,
            Padding = new Thickness(12, 22, 12, 16),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
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
        if (_sidebarStack is null)
        {
            return;
        }

        // The children below are recreated from scratch, so the incremental state must not point at the old
        // buttons any more.
        _groupContainers.Clear();
        _groupItems.Clear();
        _itemButtons.Clear();
        _selectedItemButton = null;

        while (_sidebarStack.Children.Count > 3)
        {
            _sidebarStack.Children.RemoveAt(_sidebarStack.Children.Count - 1);
        }

        var query = _search.Trim();
        var matches = string.IsNullOrEmpty(query)
            ? null
            : ArcCatalog.AllItems
                .Where(item => item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                               || item.Slug.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Where(PassesTier)
                .ToList();

        if (matches is not null)
        {
            _sidebarStack.Children.Add(Caption($"Results ({matches.Count})"));
            foreach (var item in matches.Take(80))
            {
                _sidebarStack.Children.Add(ItemButton(item));
            }

            if (matches.Count > 80)
            {
                _sidebarStack.Children.Add(new TextBlock
                {
                    Text = $"and {matches.Count - 80} more; keep typing",
                    Classes = { "arc-caption" },
                    Margin = new Thickness(12, 6, 0, 0)
                });
            }
        }
        else
        {
            _sidebarStack.Children.Add(Caption("Overview"));
            foreach (var page in OverviewPages())
            {
                _sidebarStack.Children.Add(PageButton(page));
            }

            foreach (var section in ArcCatalog.Sections)
            {
                _sidebarStack.Children.Add(SectionHeading(section.Name));
                foreach (var group in section.Groups)
                {
                    _sidebarStack.Children.Add(CategoryRow(section.Name, group));
                }
            }

            _sidebarStack.Children.Add(SectionHeading("Demo pages"));
            foreach (var page in DemoPages())
            {
                _sidebarStack.Children.Add(PageButton(page));
            }
        }

        _sidebarStack.Children.Add(SectionHeading("Appearance"));
        var themeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(10, 0, 0, 6) };
        themeRow.Children.Add(ThemeButton("Light", ArcColorScheme.Light));
        themeRow.Children.Add(ThemeButton("Dark", ArcColorScheme.Dark));
        _sidebarStack.Children.Add(themeRow);

        var accents = new WrapPanel { Margin = new Thickness(10, 6, 0, 4) };
        foreach (var accent in Enum.GetValues<ArcAccent>())
        {
            accents.Children.Add(AccentChip(accent));
        }

        _sidebarStack.Children.Add(accents);
        _sidebarStack.Children.Add(new TextBlock
        {
            Text = "Recreated from uiarc.dev. Avalonia 12, net10.0.",
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10, 18, 10, 8)
        });
    }

    private bool PassesTier(ArcCatalogItem item) => _tierFilter switch
    {
        "free" => !item.IsPro,
        "pro" => item.IsPro,
        _ => true
    };

    private IEnumerable<ArcPage> OverviewPages() =>
        _pages.Where(page => page is not ArcItemPage && (page.Group == "Overview" || page.Order == 0))
            .OrderBy(page => page.Order);

    private IEnumerable<ArcPage> DemoPages() =>
        _pages.Where(page => page is not ArcItemPage && page.Group != "Overview" && page.Order != 0)
            .OrderBy(page => page.Order);

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        Classes = { "arc-caption" },
        Margin = new Thickness(10, 12, 0, 4)
    };

    private static TextBlock SectionHeading(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeight.Medium,
        Foreground = DemoUi.Brush(ArcTokens.TextSecondary),
        Margin = new Thickness(10, 18, 0, 6)
    };

    private Control PageButton(ArcPage page)
    {
        var button = new ArcButton
        {
            Content = page.Title,
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 0),
            MinHeight = 30,
            FontSize = 13
        };
        button.Click += (_, _) => ShowPage(page);
        return button;
    }

    private Control CategoryRow(string section, ArcCatalogGroup group)
    {
        var key = section + "/" + group.Name;
        var expanded = _expanded.Contains(key) || (_selected is not null && group.Items.Contains(_selected));
        if (expanded)
        {
            _expanded.Add(key);
        }

        var headerContent = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        headerContent.Children.Add(new TextBlock { Text = group.Name, FontSize = 13 });
        var count = CountingLabel(group.Items.Count);
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
            MinHeight = 32
        };

        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(header);
        _groupContainers[key] = stack;

        if (expanded)
        {
            EnsureGroupItems(key, group);
        }

        header.Click += (_, _) =>
        {
            if (_expanded.Remove(key))
            {
                if (_groupItems.TryGetValue(key, out var host))
                {
                    host.IsVisible = false;
                }

                return;
            }

            _expanded.Add(key);
            EnsureGroupItems(key, group);
        };

        return stack;
    }

    /// <summary>
    /// Materialises one category's item rows on first expand. Later expands only flip IsVisible, so opening a
    /// category is one insertion rather than a full sidebar rebuild.
    /// </summary>
    private StackPanel EnsureGroupItems(string key, ArcCatalogGroup group)
    {
        if (_groupItems.TryGetValue(key, out var host))
        {
            host.IsVisible = true;
            return host;
        }

        host = new StackPanel { Spacing = 0 };
        foreach (var item in group.Items.Where(PassesTier))
        {
            host.Children.Add(ItemButton(item));
        }

        _groupItems[key] = host;
        if (_groupContainers.TryGetValue(key, out var container))
        {
            container.Children.Add(host);
        }

        return host;
    }

    private static Control CountingLabel(int count) => new TextBlock
    {
        Text = count.ToString(),
        Classes = { "arc-caption" },
        VerticalAlignment = VerticalAlignment.Center
    };

    private Control ItemButton(ArcCatalogItem item)
    {
        var selected = _selected?.Slug == item.Slug;
        var button = new ArcButton
        {
            Content = item.Title + (item.IsPro ? "  ·  Pro" : string.Empty),
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(22, 0, 8, 0),
            MinHeight = 28,
            FontSize = 12.5,
            FontWeight = selected ? FontWeight.Medium : FontWeight.Normal
        };
        _itemButtons[item.Slug] = button;

        if (selected)
        {
            _selectedItemButton = button;
            button.Background = DemoUi.Brush(ArcTokens.AccentSubtle);
            button.Foreground = DemoUi.Brush(ArcTokens.AccentStrong);
        }

        button.Click += (_, _) =>
        {
            _selected = item;
            ExpandGroupsFor(item);
            SelectItemButton(button);
            ShowItem(item);
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    button.BringIntoView();
                }
                catch
                {
                    // Scrolling is best effort.
                }
            }, DispatcherPriority.Loaded);
        };
        return button;
    }

    /// <summary>
    /// Ensures the category that owns the item is expanded, materialising only that category's rows. A no-op
    /// when the rows already exist. In search results mode the category containers do not exist yet, so this
    /// only records the expansion for the next full refresh.
    /// </summary>
    private void ExpandGroupsFor(ArcCatalogItem item)
    {
        foreach (var (group, section) in _catalogGroups)
        {
            if (!group.Items.Contains(item))
            {
                continue;
            }

            var key = section + "/" + group.Name;
            _expanded.Add(key);
            if (_groupContainers.ContainsKey(key))
            {
                EnsureGroupItems(key, group);
            }
        }
    }

    /// <summary>
    /// Moves the selection highlight between two existing item buttons (measured at 0.7 ms and a few bytes,
    /// versus 270-570 ms for rebuilding the sidebar). The brushes are snapshots, so a theme or accent swap
    /// still goes through RebuildAll and re-applies the highlight.
    /// </summary>
    private void SelectItemButton(ArcButton button)
    {
        if (ReferenceEquals(_selectedItemButton, button))
        {
            return;
        }

        if (_selectedItemButton is { } previous)
        {
            previous.ClearValue(TemplatedControl.BackgroundProperty);
            previous.ClearValue(TemplatedControl.ForegroundProperty);
            previous.FontWeight = FontWeight.Normal;
        }

        _selectedItemButton = button;
        button.Background = DemoUi.Brush(ArcTokens.AccentSubtle);
        button.Foreground = DemoUi.Brush(ArcTokens.AccentStrong);
        button.FontWeight = FontWeight.Medium;
    }

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

    /// <summary>
    /// Refreshes everything whose brushes are resolved once (rather than through DynamicResource): the
    /// window background, the sidebar and every cached page. The shell itself is not recreated.
    /// </summary>
    private void RebuildAll()
    {
        Background = DemoUi.Brush(ArcTokens.Background);
        InvalidateBuiltContent();
        RebuildSidebar();
        ShowPage(_currentPage ?? DefaultPage());
    }

    private void InvalidateBuiltContent()
    {
        _builtContent.Clear();
        _builtViews.Clear();
        _sectionIndex.Clear();
        _sectionPage.Clear();
    }

    private void ShowPage(ArcPage? page)
    {
        if (_contentScroller is null || page is null)
        {
            return;
        }

        _currentPage = page;
        var view = GetPageView(page);
        _contentScroller.Content = view;
        _contentScroller.Offset = default;
    }

    /// <summary>
    /// Returns the control shown for a page, building it once. The view is cached, so switching back to a
    /// page moves the same control back into the one scrolling host instead of adding already-parented
    /// children to a new panel.
    /// </summary>
    private Control GetPageView(ArcPage page)
    {
        if (_builtViews.TryGetValue(page.Title, out var cached))
        {
            return cached;
        }

        var content = GetPageContent(page);
        Control view;
        if (page is ArcItemPage)
        {
            // Item pages render their own documentation header.
            view = content;
        }
        else
        {
            var header = new StackPanel { Spacing = 10, Margin = new Thickness(0, 0, 0, 32) };
            header.Children.Add(new TextBlock { Text = page.Title, Classes = { "arc-title-sm" } });
            if (!string.IsNullOrWhiteSpace(page.Description))
            {
                header.Children.Add(new TextBlock
                {
                    Text = page.Description,
                    Classes = { "arc-body", "arc-secondary" },
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    MaxWidth = 760
                });
            }

            view = new StackPanel { Children = { header, content } };
        }

        _builtViews[page.Title] = view;
        return view;
    }

    private Control GetPageContent(ArcPage page)
    {
        if (_builtContent.TryGetValue(page.Title, out var cached))
        {
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
                Text = $"Page failed to build: {exception.Message}",
                Classes = { "arc-body-sm" },
                Foreground = DemoUi.Brush(ArcTokens.Danger),
                TextWrapping = TextWrapping.Wrap
            };
        }

        _builtContent[page.Title] = content;
        return content;
    }

    private void ShowItem(ArcCatalogItem item)
    {
        var itemPage = _pages.OfType<ArcItemPage>()
            .FirstOrDefault(page => string.Equals(page.ItemSlug, item.Slug, StringComparison.Ordinal));
        if (itemPage is not null)
        {
            ShowPage(itemPage);
            return;
        }

        // Shared demo pages: the item has no section of its own, so open the page that demonstrates it.
        foreach (var pageTitle in ArcCatalogAliases.PageTitles(item.Slug))
        {
            var aliasPage = _pages.FirstOrDefault(page => string.Equals(page.Title, pageTitle, StringComparison.Ordinal));
            if (aliasPage is not null)
            {
                ShowPage(aliasPage);
                return;
            }
        }

        var (page, section) = FindComponentSection(item);
        if (page is not null)
        {
            ShowPage(page);
            if (section is not null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        section.BringIntoView();
                    }
                    catch
                    {
                        // Scrolling is best effort.
                    }
                }, DispatcherPriority.Loaded);
            }

            return;
        }

        ShowPage(new CatalogEntryPage(item));
    }

    private (ArcPage? Page, Control? Section) FindComponentSection(ArcCatalogItem item)
    {
        if (_sectionIndex.TryGetValue(item.Slug, out var cached))
        {
            return (_sectionPage[item.Slug], cached);
        }

        var target = Normalize(item.Title);
        foreach (var page in _pages.Where(page => page is not ArcItemPage))
        {
            if (!_builtContent.TryGetValue(page.Title, out var content))
            {
                try
                {
                    content = page.Build(new ArcDemoContext(ArcThemeManager.Scheme, ArcThemeManager.Accent));
                }
                catch
                {
                    continue;
                }

                _builtContent[page.Title] = content;
            }

            var heading = FindHeading(content, target);
            if (heading is not null)
            {
                _sectionIndex[item.Slug] = heading;
                _sectionPage[item.Slug] = page;
                return (page, heading);
            }
        }

        return (null, null);
    }

    private static Control? FindHeading(ILogical root, string normalized)
    {
        if (root is TextBlock text && text.Classes.Contains("arc-heading")
            && Normalize(text.Text ?? string.Empty) == normalized)
        {
            return text;
        }

        foreach (var child in root.LogicalChildren)
        {
            var found = FindHeading(child, normalized);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static string Normalize(string value)
    {
        var buffer = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer.Append(char.ToLowerInvariant(character));
            }
        }

        return buffer.ToString();
    }

    /// <summary>Shown for a catalog item that has no dedicated page or matching section yet.</summary>
    private sealed class CatalogEntryPage : ArcItemPage
    {
        private readonly ArcCatalogItem _item;

        public CatalogEntryPage(ArcCatalogItem item) => _item = item;

        public override string ItemSlug => _item.Slug;

        public override bool DemoOnCanvas => false;

        protected override Control BuildDemo(ArcDemoContext context) => new ArcCard
        {
            Padding = new Thickness(28),
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Demo not merged yet", Classes = { "arc-label" } },
                    new TextBlock
                    {
                        Text = "This catalog entry is listed for parity with uiarc.dev. Its interactive demo is not part of the current gallery build.",
                        Classes = { "arc-body-sm", "arc-secondary" },
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 640
                    }
                }
            }
        };
    }
}

