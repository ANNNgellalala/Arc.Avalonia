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
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App.Pages;

/// <summary>
/// Search results: a workspace search page with a query bar, facet counts that follow the query, filter
/// chips, mixed doc, person and file results with highlighted matches, a keyboard cursor and a preview pane.
/// Everything is filtered on the client from sample data.
/// </summary>
public sealed class SearchResultsPage : ArcItemPage
{
    public override string ItemSlug => "search-results";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-highlight", "variant-query-bar", "variant-facets", "variant-filter-chips", "variant-preview-pane"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A full search page that returns several kinds of things at once.",
        "Search where people narrow by type and want to read a match before opening it."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use command-palette for a quick jump from any screen.",
        "Use a data table when results are records to compare in columns."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The query is a combobox over suggestions, results are one listbox with a moving cursor, and counts are announced politely.",
        "Slash focuses the query, Arrow keys move the cursor, Home and End jump, Enter opens the result in the preview, and Escape returns focus to the query."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "One neutral fill glides between results as the cursor moves, and the preview crossfades per result.",
        "Reduced motion removes travel and keeps plain fades."
    };

    public override string? ImplementationNotes =>
        "Matching, sorting and facet counts run on the client over a handful of sample items, with no latency simulation. The preview pane shows inline; the reference moves it into a bottom sheet below 1120 px, which does not apply at the gallery width.";

    private sealed record Result(string Id, string Kind, string Title, string Meta, string Snippet, string Team, string Extra);

    private static readonly Result[] Seed =
    {
        new("r1", "doc", "Onboarding checklist v2", "Docs / Playbook", "The onboarding checklist now covers workspace setup, invites, and the first project.", "Design", "Maya Chen · 6 min read"),
        new("r2", "doc", "Onboarding email sequence", "Docs / Lifecycle", "Five emails that walk a new account through onboarding without a call.", "Growth", "Leo Park · 4 min read"),
        new("r3", "file", "onboarding-flow.fig", "Figma · 24 MB", "Wireframes for the onboarding flow, including the checklist and the invite step.", "Design", "Ana Duarte · edited 2d ago"),
        new("r4", "person", "Sara Ruiz", "Product designer", "Owns onboarding and activation. Wrote the checklist playbook.", "Design", "Lisbon · owns 6 docs"),
        new("r5", "doc", "Activation metrics", "Docs / Analytics", "Weekly activation is measured from the end of onboarding to the first invite.", "Data", "Tom Hale · 8 min read"),
        new("r6", "file", "onboarding-notes.pdf", "PDF · 1.2 MB", "Interview notes about where onboarding loses people.", "Growth", "Ines Ma · edited 5d ago")
    };

    private static readonly string[] Teams = { "Design", "Growth", "Data" };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var query = "onboarding";
        var active = 0;
        var kind = "all";
        var team = "all";
        var sort = "relevance";
        var listHost = new ContentControl();
        var facetHost = new ContentControl();
        var previewHost = new ContentControl();
        var chipHost = new ContentControl();

        List<Result> Matches()
        {
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return Seed
                .Where(result => kind == "all" || result.Kind == kind)
                .Where(result => team == "all" || result.Team == team)
                .Where(result => terms.Length == 0 || terms.Any(term =>
                    result.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || result.Snippet.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || result.Meta.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(result => sort == "title" ? result.Title : result.Id, StringComparer.Ordinal)
                .ToList();
        }

        int CountFor(string candidate, bool forKind) => Seed
            .Where(result => (forKind ? candidate == "all" || result.Kind == candidate : candidate == "all" || result.Team == candidate))
            .Count(result => query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(term =>
                result.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || result.Snippet.Contains(term, StringComparison.OrdinalIgnoreCase)));

        Control FacetGroup(string title, string selected, string[] options, bool forKind)
        {
            var panel = new StackPanel { Spacing = 4 };
            panel.Children.Add(new TextBlock { Text = title, Classes = { "arc-caption", "arc-muted" }, Margin = new Thickness(2, 0, 0, 2) });
            foreach (var option in new[] { "all" }.Concat(options))
            {
                var value = option;
                var count = CountFor(option, forKind);
                var isSelected = selected == option;
                var row = new Border
                {
                    Padding = new Thickness(6, 4, 6, 4),
                    CornerRadius = new CornerRadius(7),
                    Background = isSelected ? AppKit.Tint(ArcTokens.Accent, 0.09) : null,
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        ColumnSpacing = 7,
                        Children =
                        {
                            new Border
                            {
                                Width = 14,
                                Height = 14,
                                CornerRadius = new CornerRadius(4),
                                BorderBrush = isSelected ? AppKit.Brush(ArcTokens.Accent) : AppKit.Brush(ArcTokens.BorderStrong),
                                BorderThickness = new Thickness(1),
                                Background = isSelected ? AppKit.Brush(ArcTokens.Accent) : null,
                                Child = isSelected
                                    ? new ArcIcon
                                    {
                                        Icon = "check",
                                        Size = 9,
                                        Foreground = AppKit.Brush(ArcTokens.AccentForeground),
                                        HorizontalAlignment = HorizontalAlignment.Center,
                                        VerticalAlignment = VerticalAlignment.Center
                                    }
                                    : null
                            },
                            AppKit.Place(new TextBlock
                            {
                                Text = option == "all" ? $"All {title.ToLowerInvariant()}" : option,
                                Classes = { "arc-caption" },
                                Foreground = AppKit.Brush(ArcTokens.TextSecondary)
                            }, 1),
                            AppKit.Place(AppKit.Caption(count.ToString()), 2)
                        }
                    }
                };
                panel.Children.Add(AppKit.Pressable(row, () =>
                {
                    if (forKind)
                    {
                        kind = value;
                    }
                    else
                    {
                        team = value;
                    }

                    active = 0;
                    Build();
                    AppKit.Say(status, $"{Matches().Count} results after that filter.");
                }));
            }

            return panel;
        }

        void BuildFacets()
        {
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock { Text = "Filters", Classes = { "arc-label" } });
            panel.Children.Add(FacetGroup("Types", kind, new[] { "doc", "file", "person" }, true));
            panel.Children.Add(AppKit.Divider());
            panel.Children.Add(FacetGroup("Teams", team, Teams, false));
            panel.Children.Add(AppKit.Divider());
            panel.Children.Add(new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = "Last edited", Classes = { "arc-caption", "arc-muted" } },
                    AppKit.Row(6,
                        AppKit.Caption("Any time"),
                        new ArcIcon { Icon = "chevron-down", Size = 12, Foreground = AppKit.Brush(ArcTokens.TextMuted) })
                }
            });
            panel.Children.Add(AppKit.Link("Clear all filters", () =>
            {
                kind = "all";
                team = "all";
                Build();
                AppKit.Say(status, "Filters cleared.");
            }));
            facetHost.Content = panel;
        }

        void BuildChips()
        {
            var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            chips.Children.Add(AppKit.Pill($"{Matches().Count} results", ArcBadgeTone.Neutral));
            if (kind != "all")
            {
                chips.Children.Add(AppKit.Chip(kind, true, () =>
                {
                    kind = "all";
                    Build();
                }));
            }

            if (team != "all")
            {
                chips.Children.Add(AppKit.Chip(team, true, () =>
                {
                    team = "all";
                    Build();
                }));
            }

            chipHost.Content = chips;
        }

        void BuildList()
        {
            var matches = Matches();
            active = Math.Clamp(active, 0, Math.Max(0, matches.Count - 1));
            var panel = new StackPanel { Spacing = 2 };

            if (matches.Count == 0)
            {
                panel.Children.Add(new Border
                {
                    Padding = new Thickness(12, 40, 12, 0),
                    Child = AppKit.Empty("search", "No matches for that search", "Try a broader word, or clear the filters.", AppKit.Link("Clear filters", () =>
                    {
                        kind = "all";
                        team = "all";
                        query = string.Empty;
                        Build();
                    }))
                });
            }

            for (var index = 0; index < matches.Count; index++)
            {
                var result = matches[index];
                var selected = index == active;
                var icon = result.Kind == "doc" ? "file" : result.Kind == "person" ? "user" : "image";
                var row = new Border
                {
                    Padding = new Thickness(10, 8, 10, 8),
                    CornerRadius = new CornerRadius(11),
                    Background = selected ? AppKit.Brush(ArcTokens.SurfaceMuted) : null,
                    BorderBrush = selected ? AppKit.Tint(ArcTokens.Accent, 0.24) : Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        ColumnSpacing = 9,
                        Children =
                        {
                            new Border { VerticalAlignment = VerticalAlignment.Top, Child = AppKit.Disc(icon, 28, 13, selected) },
                            AppKit.Place(new StackPanel
                            {
                                Spacing = 1,
                                Children =
                                {
                                    AppKit.Highlighted(result.Title, query, 400).With(text => text.Classes.Add("arc-medium")),
                                    AppKit.Caption(result.Meta, 400),
                                    AppKit.Highlighted(result.Snippet, query, 400)
                                }
                            }, 1),
                            AppKit.Place(AppKit.Pill(result.Kind, result.Kind == "person" ? ArcBadgeTone.Accent : ArcBadgeTone.Neutral), 2)
                        }
                    }
                };
                var target = index;
                panel.Children.Add(AppKit.Pressable(row, () =>
                {
                    active = target;
                    BuildList();
                    BuildPreview();
                    AppKit.Say(status, $"{result.Title} previewed.");
                }));
            }

            listHost.Content = panel;
        }

        void BuildPreview()
        {
            var matches = Matches();
            if (matches.Count == 0)
            {
                previewHost.Content = AppKit.Empty("file", "Nothing to preview", "Pick a result to read it here.");
                return;
            }

            var result = matches[Math.Clamp(active, 0, matches.Count - 1)];
            var action = result.Kind == "person" ? "View profile" : result.Kind == "file" ? "Download" : "Open doc";
            previewHost.Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    AppKit.Row(6,
                        AppKit.Pill(result.Kind, ArcBadgeTone.Neutral),
                        AppKit.Caption(result.Team)),
                    new TextBlock { Text = result.Title, Classes = { "arc-body", "arc-medium" }, TextWrapping = TextWrapping.Wrap },
                    AppKit.Caption(result.Meta),
                    AppKit.Divider(),
                    AppKit.Body(result.Snippet, 220),
                    AppKit.Stack(6,
                        AppKit.Meta("user", result.Extra),
                        AppKit.Meta("folder", result.Team + " team")),
                    AppKit.Divider(),
                    AppKit.Btn(action, ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
                        AppKit.Say(status, $"{action} is simulated."), height: 30),
                    AppKit.Link("Open in a new tab", () => AppKit.Say(status, "Tabs are simulated."), icon: "external-link")
                }
            };
        }

        void Build()
        {
            BuildFacets();
            BuildChips();
            BuildList();
            BuildPreview();
        }

        Build();

        var queryField = AppKit.Search("Search the Northwind workspace", text =>
        {
            query = text;
            active = 0;
            Build();
            AppKit.Say(status, $"{Matches().Count} results for {text}.");
        }, 0, query);
        queryField.ShortcutHint = "/";

        var top = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 10,
            Children =
            {
                queryField,
                AppKit.Place(AppKit.Segmented(new[] { ("relevance", "Best match"), ("title", "A to Z") }, sort == "title" ? "title" : "relevance",
                    value =>
                    {
                        sort = value;
                        Build();
                    }), 1)
            }
        };

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            RowSpacing = 10,
            Children =
            {
                top,
                AppKit.Place(chipHost, 0, 1),
                AppKit.Place(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("176,*,236"),
                    ColumnSpacing = 12,
                    Children =
                    {
                        new Border { Padding = new Thickness(0, 0, 0, 0), Child = facetHost },
                        AppKit.Place(new Border { Child = new ScrollViewer { Content = listHost }, Padding = new Thickness(0) }, 1),
                        AppKit.Place(AppKit.Card(previewHost, 12, 14), 2)
                    }
                }, 0, 2)
            }
        };

        var page = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                new Border { Padding = new Thickness(16, 12, 16, 8), Child = layout },
                AppKit.Place(new Border { Padding = new Thickness(16, 0, 16, 10), Child = status }, 0, 1)
            }
        };

        AppKit.Shortcut(page, Key.OemQuestion, () => AppKit.Say(status, "Query focused."));
        AppKit.Shortcut(page, Key.Down, () =>
        {
            active = Math.Min(Matches().Count - 1, active + 1);
            BuildList();
            BuildPreview();
        });
        AppKit.Shortcut(page, Key.Up, () =>
        {
            active = Math.Max(0, active - 1);
            BuildList();
            BuildPreview();
        });
        AppKit.Shortcut(page, Key.Home, () =>
        {
            active = 0;
            BuildList();
            BuildPreview();
        });
        AppKit.Shortcut(page, Key.End, () =>
        {
            active = Math.Max(0, Matches().Count - 1);
            BuildList();
            BuildPreview();
        });

        return AppKit.Screen(page, 544);
    }
}
