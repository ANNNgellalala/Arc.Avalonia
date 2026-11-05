using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Theme;

namespace TopControlSkinPoc;

/// <summary>
/// Diagnostic (<c>--probe</c>): prints the Arc token values, which candidate Fluent keys exist, which of
/// them the AFTER panel actually overrides, and the resolved visual-tree properties of every sampled
/// control in both states. This is the evidence behind the ADR 0004 classification table.
/// </summary>
public static class SkinProbe
{
    /// <summary>Candidate Fluent 12.1.4 resource keys, grouped by control (names verified against the
    /// real FluentTheme resource inventory dumped by this probe; the 11.x names that no longer exist are
    /// kept in the list so the ADR can show which names silently do nothing).</summary>
    private static readonly (string Control, string[] Keys)[] Candidates =
    {
        ("Global", new[]
        {
            "ControlCornerRadius", "OverlayCornerRadius", "ControlContentThemeFontSize",
            "ContentControlThemeFontSize", "SystemAccentColor", "SystemControlHighlightAccentBrush"
        }),
        ("TextBox", new[]
        {
            "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused",
            "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused",
            "TextControlForeground", "TextControlForegroundFocused", "TextControlPlaceholderForeground",
            "TextControlSelectionHighlightColor", "TextControlThemeMinHeight", "TextControlThemeMinWidth",
            "TextControlThemePadding", "TextControlPadding", "TextControlBorderThemeThickness",
            "TextControlBorderThemeThicknessFocused"
        }),
        ("ComboBox", new[]
        {
            "ComboBoxBackground", "ComboBoxBackgroundPointerOver", "ComboBoxBackgroundFocused",
            "ComboBoxBorderBrush", "ComboBoxBorderBrushPointerOver", "ComboBoxForeground",
            "ComboBoxPlaceHolderForeground", "ComboBoxPlaceholderForeground",
            "ComboBoxDropDownBackground", "ComboBoxDropDownBorderBrush", "ComboBoxThemeMinWidth",
            "ComboBoxPadding", "ComboBoxItemBackgroundPointerOver", "ComboBoxItemBackgroundSelected",
            "ComboBoxItemForeground", "ComboBoxItemForegroundSelected"
        }),
        ("TabControl/TabItem", new[]
        {
            "TabItemHeaderFontSize", "TabItemMargin", "TabItemHeaderMargin",
            "TabItemHeaderBackgroundUnselected", "TabItemHeaderBackgroundSelected",
            "TabItemHeaderBackgroundUnselectedPointerOver",
            "TabItemHeaderForegroundUnselected", "TabItemHeaderForegroundSelected",
            "TabItemHeaderSelectedPipeFill", "TabbedPageTabItemHeaderFontSize"
        }),
        ("ScrollBar", new[]
        {
            "ScrollBarSize", "ScrollBarBackground", "ScrollBarBackgroundPointerOver",
            "ScrollBarThumbBackgroundColor", "ScrollBarThumbFill", "ScrollBarThumbFillPointerOver",
            "ScrollBarThumbFillPressed", "ScrollBarTrackFill", "ScrollBarTrackFillPointerOver",
            "ScrollBarTrackStroke", "ScrollBarBorderBrush", "ScrollBarPanningThumbBackground"
        })
    };

    public static async Task DumpAsync(Application app, MainWindow window)
    {
        Console.WriteLine("== POC-B PROBE: FluentTheme resource inventory ==");
        var fluent = FindFluentTheme(app);
        if (fluent is not null)
        {
            Console.WriteLine($"FluentTheme children: {string.Join(", ", fluent.Select(s => s.GetType().Name))}");
            DumpDictionary(fluent.Resources, "FluentTheme.Resources", 0, new HashSet<object>());
        }
        else
        {
            Console.WriteLine("FluentTheme not found");
        }

        Console.WriteLine("== POC-B PROBE: Arc token values (from Foundation ArcPalette) ==");
        foreach (var scheme in new[] { ArcColorScheme.Light, ArcColorScheme.Dark })
        {
            var palette = ArcPalette.Build(scheme, ArcAccent.Neutral);
            string Hex(string key) => (palette[key] as ISolidColorBrush)?.Color.ToString() ?? "?";
            Console.WriteLine($"[{scheme}] Background={Hex(ArcTokens.Background)} Surface={Hex(ArcTokens.Surface)} "
                              + $"SurfaceMuted={Hex(ArcTokens.SurfaceMuted)} Border={Hex(ArcTokens.Border)} "
                              + $"BorderSubtle={Hex(ArcTokens.BorderSubtle)} BorderStrong={Hex(ArcTokens.BorderStrong)} "
                              + $"Foreground={Hex(ArcTokens.Foreground)} Secondary={Hex(ArcTokens.TextSecondary)} "
                              + $"Muted={Hex(ArcTokens.TextMuted)} Accent={Hex(ArcTokens.Accent)} "
                              + $"RadiusControl={palette[ArcTokens.RadiusControl]} HeightMd={palette[ArcTokens.ControlHeightMd]}");
        }

        var before = window.FindControl<Border>("BeforePanel")!;
        var after = window.FindControl<Border>("AfterPanel")!;

        await SweepAsync(app, window, before);

        foreach (var scheme in new[] { ArcColorScheme.Light, ArcColorScheme.Dark })
        {
            ArcThemeManager.Apply(app, scheme, ArcAccent.Neutral);
            ApplyPageTheme(window);
            await SettleAsync();
            var theme = app.ActualThemeVariant;
            Console.WriteLine($"== POC-B PROBE [{scheme}] (theme variant {theme}) ==");

            foreach (var (control, keys) in Candidates)
            {
                Console.WriteLine($"-- {control} --");
                foreach (var key in keys)
                {
                    var beforeValue = Resolve(before, key, theme);
                    var afterValue = Resolve(after, key, theme);
                    Console.WriteLine($"   {key,-42} before={beforeValue,-28} after={afterValue}");
                }
            }

            foreach (var name in new[] { "BeforeTextBox", "AfterTextBox", "BeforeComboBox", "AfterComboBox" })
            {
                var control = window.FindControl<Control>(name);
                Console.WriteLine($"-- {name} --");
                DescribeControl(control);
                DumpVisualTree(control, 4);
            }

            foreach (var name in new[] { "BeforeTabControl", "AfterTabControl" })
            {
                var tab = window.FindControl<TabControl>(name);
                Console.WriteLine($"-- {name} --");
                foreach (var item in tab?.Items.OfType<TabItem>() ?? Enumerable.Empty<TabItem>())
                {
                    Console.WriteLine($"   TabItem '{item.Header}' IsSelected={item.IsSelected} Background={item.Background} "
                                      + $"Foreground={item.Foreground} Padding={item.Padding} MinHeight={item.MinHeight} FontSize={item.FontSize}");
                }

                if (tab is not null)
                {
                    foreach (var item in tab.GetVisualDescendants().OfType<TabItem>())
                    {
                        Console.WriteLine($"   visual TabItem '{item.Header}' selected={item.IsSelected}");
                        DumpVisualTree(item, 3);
                    }
                }
            }

            foreach (var name in new[] { "BeforeScrollBarV", "AfterScrollBarV", "BeforeScrollBarH", "AfterScrollBarH" })
            {
                var scrollBar = window.FindControl<ScrollBar>(name);
                Console.WriteLine($"-- {name} --");
                if (scrollBar is null)
                {
                    continue;
                }

                Console.WriteLine($"   ScrollBar Bounds={scrollBar.Bounds.Width:0.#}x{scrollBar.Bounds.Height:0.#} "
                                  + $"Background={scrollBar.Background} Foreground={scrollBar.Foreground} "
                                  + $"MinWidth={scrollBar.MinWidth} Width={scrollBar.Width} Orientation={scrollBar.Orientation}");
                DumpVisualTree(scrollBar, 6, includeBrushes: true);
            }
        }
    }

    /// <summary>Direct test of a specific key (e.g. XAML in a theme file) against the BEFORE panel.</summary>
    private static async Task SweepAsync(Application app, Window window, Border before)
    {
        var scrollBar = window.FindControl<ScrollBar>("BeforeScrollBarV")!;
        var thumb = scrollBar.GetVisualDescendants().OfType<Thumb>().FirstOrDefault();
        var tabItems = window.FindControl<TabControl>("BeforeTabControl")!.GetVisualDescendants().OfType<TabItem>().ToList();

        Console.WriteLine("== POC-B PROBE: key sweep (set one key at a time on Application.Resources, then re-read) ==");
        Console.WriteLine($"   baseline thumb.Background={thumb?.Background} "
                          + $"tabItem.Padding={tabItems.FirstOrDefault()?.Padding} tabItem.MinHeight={tabItems.FirstOrDefault()?.MinHeight}");

        foreach (var key in new[]
                 {
                     "ScrollBarThumbFill", "ScrollBarThumbBackgroundColor", "ScrollBarThumbFillPointerOver",
                     "ScrollBarForeground", "ScrollBarBorderBrush", "ScrollBarTrackFill",
                     "ScrollBarPanningThumbBackground", "SystemControlForegroundBaseMediumLowBrush",
                     "SystemControlForegroundBaseMediumBrush"
                 })
        {
            app.Resources[key] = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Colors.Red);
            await SettleAsync();
            Console.WriteLine($"   SWEEP {key,-44} thumb.Background={thumb?.Background}");
            app.Resources.Remove(key);
            await SettleAsync();
        }

        foreach (var (key, value) in new (string, object)[]
                 {
                     ("TabItemHeaderMargin", new Thickness(0, 0, 0, 0)),
                     ("TabItemMinHeight", 0d),
                     ("TabItemPipeThickness", 6d),
                     ("TabItemVerticalPipeHeight", 6d),
                     ("TabControlTopPlacementItemMargin", new Thickness(0, 0, 30, 0))
                 })
        {
            app.Resources[key] = value;
            await SettleAsync();
            Console.WriteLine($"   SWEEP {key,-44} tabItem.Padding={tabItems.FirstOrDefault()?.Padding} "
                              + $"tabItem.MinHeight={tabItems.FirstOrDefault()?.MinHeight} "
                              + $"tabItem.Margin={tabItems.FirstOrDefault()?.Margin}");
            app.Resources.Remove(key);
            await SettleAsync();
        }
    }

    /// <summary>Paints the "page" with Arc tokens so both panels sit on an Arc-like surface.</summary>
    public static void ApplyPageTheme(Window window)
    {
        var root = window.FindControl<Border>("Root")!;
        root.Background = ArcResources.Brush(ArcTokens.Background);
        window.FindControl<TextBlock>("Headline")!.Foreground = ArcResources.Brush(ArcTokens.Foreground);
        window.FindControl<TextBlock>("ThemeLine")!.Foreground = ArcResources.Brush(ArcTokens.TextSecondary);
        foreach (var name in new[] { "BeforePanel", "AfterPanel" })
        {
            var panel = window.FindControl<Border>(name)!;
            panel.Background = ArcResources.Brush(ArcTokens.SurfaceMuted);
            panel.BorderBrush = ArcResources.Brush(ArcTokens.Border);
        }
    }

    private static string Resolve(IResourceHost host, string key, ThemeVariant? theme) =>
        host.TryFindResource(key, theme, out var value) ? value?.ToString() ?? "<null>" : "<missing>";

    private static FluentTheme? FindFluentTheme(Application app) =>
        FindNested<FluentTheme>(app.Styles).FirstOrDefault();

    private static IEnumerable<T> FindNested<T>(IEnumerable<IStyle> styles) where T : class, IStyle
    {
        foreach (var style in styles)
        {
            if (style is T match)
            {
                yield return match;
            }

            if (style is Styles nested)
            {
                foreach (var child in FindNested<T>(nested))
                {
                    yield return child;
                }
            }
        }
    }

    /// <summary>Walks the actual FluentTheme resource dictionaries and lists every key it defines.</summary>
    private static void DumpDictionary(IResourceProvider provider, string label, int depth, HashSet<object> visited)
    {
        if (!visited.Add(provider))
        {
            return;
        }

        var indent = new string(' ', depth * 2);
        if (provider is not IResourceDictionary dictionary)
        {
            Console.WriteLine($"{indent}{label}: {provider.GetType().Name} (not an IResourceDictionary)");
            return;
        }

        Console.WriteLine($"{indent}{label}: values={dictionary.Count} merged={dictionary.MergedDictionaries.Count} themeDicts={dictionary.ThemeDictionaries.Count}");
        foreach (var pair in dictionary)
        {
            var text = pair.Value is IBrush brush ? brush.ToString() : pair.Value?.ToString() ?? "<null>";
            Console.WriteLine($"{indent}  {pair.Key} = {text}");
        }

        foreach (var theme in dictionary.ThemeDictionaries)
        {
            Console.WriteLine($"{indent}  [theme {theme.Key}]");
            DumpDictionary(theme.Value, "dict", depth + 2, visited);
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            var source = merged.GetType().Name;
            Console.WriteLine($"{indent}  [merged {source}]");
            DumpDictionary(merged, "dict", depth + 2, visited);
        }
    }

    private static void DescribeControl(Control? control)
    {
        switch (control)
        {
            case TextBox textBox:
                Console.WriteLine($"   TextBox Background={textBox.Background} BorderBrush={textBox.BorderBrush} "
                                  + $"Padding={textBox.Padding} MinHeight={textBox.MinHeight} FontSize={textBox.FontSize} "
                                  + $"CornerRadius={textBox.CornerRadius} Bounds={textBox.Bounds.Size}");
                break;
            case ComboBox comboBox:
                Console.WriteLine($"   ComboBox Background={comboBox.Background} BorderBrush={comboBox.BorderBrush} "
                                  + $"Padding={comboBox.Padding} MinHeight={comboBox.MinHeight} FontSize={comboBox.FontSize} "
                                  + $"MinWidth={comboBox.MinWidth} CornerRadius={comboBox.CornerRadius} Bounds={comboBox.Bounds.Size}");
                break;
            case null:
                Console.WriteLine("   <not found>");
                break;
            default:
                Console.WriteLine($"   {control.GetType().Name}");
                break;
        }
    }

    private static void DumpVisualTree(Visual? root, int maxDepth, bool includeBrushes = false)
    {
        if (root is null)
        {
            return;
        }

        void Walk(Visual visual, int depth)
        {
            if (depth > maxDepth)
            {
                return;
            }

            var indent = new string(' ', 3 + depth * 2);
            var name = (visual as StyledElement)?.Name;
            var label = $"{visual.GetType().Name}{(string.IsNullOrEmpty(name) ? string.Empty : "#" + name)}";
            var details = visual switch
            {
                Border border => $"Background={border.Background} BorderBrush={border.BorderBrush} "
                                 + $"BorderThickness={border.BorderThickness} CornerRadius={border.CornerRadius} "
                                 + $"Bounds={border.Bounds.Size}",
                Thumb thumb => $"Background={thumb.Background} Bounds={thumb.Bounds.Size}",
                RepeatButton button => $"Background={button.Background} BorderBrush={button.BorderBrush} Bounds={button.Bounds.Size}",
                ContentPresenter presenter => $"Background={presenter.Background} BorderBrush={presenter.BorderBrush} "
                                              + $"CornerRadius={presenter.CornerRadius} Bounds={presenter.Bounds.Size}",
                TextBlock textBlock => $"Foreground={textBlock.Foreground} FontSize={textBlock.FontSize} Text=\"{textBlock.Text}\"",
                Panel panel => $"Background={panel.Background} Bounds={panel.Bounds.Size}",
                _ => includeBrushes ? $"Bounds={visual.Bounds.Size}" : string.Empty
            };
            Console.WriteLine($"{indent}{label} {details}");

            foreach (var child in visual.GetVisualChildren())
            {
                Walk(child, depth + 1);
            }
        }

        Walk(root, 0);
    }

    private static async Task SettleAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await Task.Delay(160);
    }
}
