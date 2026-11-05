using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App.Pages;

/// <summary>
/// Docs sidebar: a documentation layout with a filterable nav tree, a version switcher, an article with a
/// code sample, and an on-this-page outline that follows the chosen heading. Content is sample reference
/// material; there is no scroll spy because the article never scrolls in the frame.
/// </summary>
public sealed class DocsSidebarPage : ArcItemPage
{
    public override string ItemSlug => "docs-sidebar";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-nav-tree", "variant-version-switcher", "variant-on-this-page", "variant-article"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Product or API documentation with a nav tree and a page outline.",
        "Docs that need a version switcher and a filterable tree."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use tree-view for a plain hierarchy outside a docs layout.",
        "Use sidebar-rail for product navigation instead of documentation."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The filter field narrows the tree and announces the match count; the active page and heading expose their state.",
        "Arrow keys move within the tree in the reference; here each row is a focusable button with Enter and Space, and Escape clears the filter."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The outline indicator glides to the chosen heading and nav groups expand by height as the filter opens branches.",
        "Reduced motion makes the glide and the folds instant."
    };

    public override string? ImplementationNotes =>
        "The scroll-spy outline is approximated: the article is a fixed panel, so the outline follows the heading you pick rather than a scroll position. Version switching and page changes only update local state.";

    private sealed record DocPage(string Id, string Group, string Title, string Summary);

    private static readonly DocPage[] Pages =
    {
        new("install", "Getting started", "Install", "Add the package and the first component."),
        new("theming", "Getting started", "Theming", "Tokens, schemes and the accent scale."),
        new("button", "Components", "Button", "One clear action with quiet secondary states."),
        new("input", "Components", "Input", "A single line field with labels and states."),
        new("dialog", "Components", "Dialog", "A focused surface for decisions."),
        new("server", "API", "Server SDK", "Create clients and stream responses."),
        new("webhooks", "API", "Webhooks", "Verify signatures and retry delivery.")
    };

    private static readonly string[] Outline = { "Install the package", "Render the first component", "Theming" };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var active = "button";
        var filter = string.Empty;
        var version = "1.4";
        var versionOpen = false;
        var heading = 1;
        var treeHost = new ContentControl();
        var articleHost = new ContentControl();
        var outlineHost = new ContentControl();
        var versionHost = new ContentControl();

        Control PageRow(DocPage page)
        {
            var target = page;
            var selected = page.Id == active;
            var row = new Border
            {
                Padding = new Thickness(9, 6, 9, 6),
                CornerRadius = new CornerRadius(9),
                Background = selected ? AppKit.Tint(ArcTokens.Accent, 0.11) : null,
                Child = AppKit.Row(8,
                    new ArcIcon
                    {
                        Icon = "file",
                        Size = 14,
                        Foreground = selected ? AppKit.Brush(ArcTokens.Accent) : AppKit.Brush(ArcTokens.TextMuted)
                    },
                    new TextBlock
                    {
                        Text = page.Title,
                        Classes = { "arc-body-sm" },
                        Foreground = selected ? AppKit.Brush(ArcTokens.Foreground) : AppKit.Brush(ArcTokens.TextSecondary)
                    })
            };
            return AppKit.Pressable(row, () =>
            {
                active = target.Id;
                heading = 0;
                Build();
                AppKit.Say(status, $"{target.Title} opened. Content is sample documentation.");
            });
        }

        void BuildTree()
        {
            var matches = Pages
                .Where(page => string.IsNullOrWhiteSpace(filter)
                    || page.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || page.Summary.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || page.Group.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var panel = new StackPanel { Spacing = 3 };
            panel.Children.Add(AppKit.Search("Filter pages", text =>
            {
                filter = text;
                BuildTree();
                AppKit.Say(status, $"{Pages.Count(page => string.IsNullOrWhiteSpace(filter)
                    || page.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || page.Summary.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || page.Group.Contains(filter, StringComparison.OrdinalIgnoreCase))} pages match.");
            }, 176, filter));

            if (matches.Count == 0)
            {
                panel.Children.Add(new Border
                {
                    Padding = new Thickness(9, 12, 9, 12),
                    Child = AppKit.Stack(4,
                        AppKit.Caption("No pages match"),
                        AppKit.Link("Clear the filter", () =>
                        {
                            filter = string.Empty;
                            Build();
                        }))
                });
                treeHost.Content = panel;
                return;
            }

            foreach (var group in matches.GroupBy(page => page.Group))
            {
                panel.Children.Add(new Border
                {
                    Margin = new Thickness(9, 10, 9, 3),
                    Child = new TextBlock { Text = group.Key, Classes = { "arc-caption", "arc-muted" } }
                });
                foreach (var page in group)
                {
                    panel.Children.Add(PageRow(page));
                }
            }

            treeHost.Content = panel;
        }

        void BuildVersion()
        {
            var button = new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(8, 5, 8, 5),
                Background = versionOpen ? AppKit.Brush(ArcTokens.SurfaceMuted) : null,
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    ColumnSpacing = 7,
                    Children =
                    {
                        new ArcIcon { Icon = "layers", Size = 14, Foreground = AppKit.Brush(ArcTokens.TextSecondary) },
                        AppKit.Place(new TextBlock { Text = $"Version {version}", Classes = { "arc-caption" } }, 1),
                        AppKit.Place(new ArcIcon { Icon = "chevrons-up-down", Size = 12, Foreground = AppKit.Brush(ArcTokens.TextMuted) }, 2)
                    }
                }
            };
            AppKit.Pressable(button, () =>
            {
                versionOpen = !versionOpen;
                Build();
            });
            versionHost.Content = button;

            if (!versionOpen)
            {
                return;
            }

            var menu = new StackPanel { Spacing = 2 };
            foreach (var option in new[] { "1.4", "1.3", "1.2" })
            {
                var value = option;
                var item = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8, 6, 8, 6),
                    Background = option == version ? AppKit.Tint(ArcTokens.Accent, 0.1) : null,
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            new TextBlock { Text = value, Classes = { "arc-body-sm" } },
                            AppKit.Place(option == version
                                ? (Control)new ArcIcon { Icon = "check", Size = 13, Foreground = AppKit.Brush(ArcTokens.Accent) }
                                : new Border { Width = 0 }, 1)
                        }
                    }
                };
                menu.Children.Add(AppKit.Pressable(item, () =>
                {
                    version = value;
                    versionOpen = false;
                    Build();
                    AppKit.Say(status, $"Showing documentation for version {value}.");
                }));
            }

            var floating = AppKit.Floating(menu, 5, 12, 156);
            floating.HorizontalAlignment = HorizontalAlignment.Stretch;
            floating.Margin = new Thickness(0, 34, 0, 0);
            var layer = new Panel { Children = { button, floating } };
            versionHost.Content = layer;
        }

        void BuildArticle()
        {
            var page = Pages.First(entry => entry.Id == active);
            var article = new StackPanel { Spacing = 9 };
            article.Children.Add(AppKit.Row(6, AppKit.Caption(page.Group), AppKit.Pill(version == "1.4" ? "Current" : "Older", ArcBadgeTone.Neutral)));
            article.Children.Add(new TextBlock { Text = page.Title, Classes = { "arc-title-sm" } });
            article.Children.Add(AppKit.Body(page.Summary, 420));
            article.Children.Add(AppKit.Divider());

            article.Children.Add(HeadingBlock(0, "Install the package"));
            article.Children.Add(AppKit.Body("Add the package once, then import the components you use. The CLI command writes the source into your project so you own it.", 500));
            article.Children.Add(AppKit.Tile(AppKit.Stack(6,
                AppKit.Mono("pnpm add @uiarc/react"),
                AppKit.Divider(),
                AppKit.Mono("import { Button } from \"@/components/arc/button\";")), 12, 12, hairline: true));

            article.Children.Add(HeadingBlock(1, "Render the first component"));
            article.Children.Add(AppKit.Body("Components are unstyled by default and take their colour from the nearest theme root, so a single provider covers the page.", 500));
            article.Children.Add(AppKit.Row(8,
                AppKit.Btn("Primary", ArcButtonVariant.Primary, ArcButtonSize.Sm, () => AppKit.Say(status, "Sample button pressed."), height: 32),
                AppKit.Btn("Secondary", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => AppKit.Say(status, "Sample button pressed."), height: 32),
                AppKit.Pill("Live sample", ArcBadgeTone.Accent)));

            article.Children.Add(HeadingBlock(2, "Theming"));
            article.Children.Add(AppKit.Body("Semantic tokens drive every surface; override the palette once and both schemes follow.", 500));


            articleHost.Content = article;
        }

        Control HeadingBlock(int index, string text)
        {
            var selected = heading == index;
            var block = new Border
            {
                Padding = new Thickness(8, 4, 8, 4),
                CornerRadius = new CornerRadius(8),
                Background = selected ? AppKit.Tint(ArcTokens.Accent, 0.07) : null,
                Child = new TextBlock
                {
                    Text = text,
                    Classes = { "arc-body-sm", "arc-medium" },
                    Foreground = selected ? AppKit.Brush(ArcTokens.Accent) : AppKit.Brush(ArcTokens.Foreground)
                }
            };
            AppKit.Pressable(block, () =>
            {
                heading = index;
                Build();
                AppKit.Say(status, $"Outline moved to {text}.");
            });
            return block;
        }

        void BuildOutline()
        {
            var panel = new StackPanel { Spacing = 2 };
            panel.Children.Add(new TextBlock { Text = "On this page", Classes = { "arc-caption", "arc-muted" }, Margin = new Thickness(8, 0, 0, 5) });
            for (var index = 0; index < Outline.Length; index++)
            {
                var target = index;
                var selected = heading == index;
                var row = new Border
                {
                    Padding = new Thickness(8, 5, 8, 5),
                    CornerRadius = new CornerRadius(7),
                    Background = selected ? AppKit.Tint(ArcTokens.Accent, 0.08) : null,
                    Child = AppKit.Row(7,
                        new Border
                        {
                            Width = 2,
                            Height = selected ? 14 : 10,
                            CornerRadius = new CornerRadius(1),
                            Background = selected ? AppKit.Brush(ArcTokens.Accent) : AppKit.Brush(ArcTokens.Border)
                        },
                        new TextBlock
                        {
                            Text = Outline[index],
                            Classes = { "arc-caption" },
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 148,
                            Foreground = selected ? AppKit.Brush(ArcTokens.Foreground) : AppKit.Brush(ArcTokens.TextMuted)
                        })
                };
                panel.Children.Add(AppKit.Pressable(row, () =>
                {
                    heading = target;
                    Build();
                }));
            }

            panel.Children.Add(AppKit.Divider(8));
            panel.Children.Add(AppKit.Stack(6,
                AppKit.Caption("Was this page helpful?"),
                AppKit.Row(6,
                    AppKit.Btn("Yes", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => AppKit.Say(status, "Thanks, noted."), icon: "thumbs-up", height: 28),
                    AppKit.Btn("No", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () => AppKit.Say(status, "Thanks, noted."), height: 28))));

            outlineHost.Content = panel;
        }

        void Build()
        {
            BuildTree();
            BuildVersion();
            BuildArticle();
            BuildOutline();
        }

        Build();

        var nav = new Border
        {
            Background = AppKit.Brush(ArcTokens.SurfaceMuted),
            BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(12),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*"),
                Children =
                {
                    AppKit.Stack(10,
                        AppKit.Row(6,
                            AppKit.Mark("file", 26, 13),
                            new TextBlock { Text = "Arc Docs", Classes = { "arc-body-sm", "arc-medium" } }),
                        versionHost),
                    AppKit.Place(treeHost, 0, 1)
                }
            }
        };

        var articleColumn = new Border
        {
            Padding = new Thickness(20, 18, 18, 18),
            Child = articleHost
        };

        var outlineColumn = new Border
        {
            Width = 196,
            Padding = new Thickness(12, 18, 12, 18),
            Background = AppKit.Brush(ArcTokens.Surface),
            Child = outlineHost
        };

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("208,*,196") };
        Grid.SetColumn(articleColumn, 1);
        Grid.SetColumn(outlineColumn, 2);
        layout.Children.Add(nav);
        layout.Children.Add(articleColumn);
        layout.Children.Add(outlineColumn);

        var screen = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children = { layout, AppKit.Place(new Border { Padding = new Thickness(20, 0, 20, 8), Child = status }, 0, 1) }
        };

        AppKit.Shortcut(screen, Key.OemOpenBrackets, () =>
        {
            AppKit.Say(status, "The rail shortcut is available in the reference; this layout keeps its tree open at gallery width.");
        });
        AppKit.Shortcut(screen, Key.Escape, () =>
        {
            if (!string.IsNullOrEmpty(filter))
            {
                filter = string.Empty;
                Build();
                AppKit.Say(status, "Filter cleared.");
            }
        });

        return AppKit.Screen(screen, 570);
    }
}
