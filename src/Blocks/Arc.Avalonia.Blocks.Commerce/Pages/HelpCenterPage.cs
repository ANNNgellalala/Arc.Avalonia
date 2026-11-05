using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Ck = Arc.Avalonia.Blocks.Commerce.CommerceKit;

namespace Arc.Avalonia.Blocks.Commerce.Pages;

/// <summary>
/// Help center: a self serve support surface with instant search, topic cards, popular articles, and a reading
/// view with an outline and a feedback question. Articles and topics are sample data; search runs in memory and
/// feedback only updates local state.
/// </summary>
public sealed class HelpCenterPage : ArcItemPage
{
    public override string ItemSlug => "help-center";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "layout-help-home", "variant-search", "variant-topics", "variant-popular"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A public help center or knowledge base.",
        "In-app help with searchable articles and a reading view."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Live chat with support; use support-conversation."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Search is a combobox over a listbox: arrow keys move through results and Enter opens one.",
        "Article headings are real headings, outline links move focus to their section, and feedback is announced politely."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Results drop in from the field with a short fade and are not animated while arrowing through them.",
        "Views slide 16 px in the direction of travel; reduced motion uses a fade only."
    };

    public override string? ImplementationNotes =>
        "Articles, topics and suggestions are sample data; search ranks title and summary matches in memory, and matching words are not highlighted (the reference wraps them in a mark). The outline is a static list rather than a scroll tracker, and the contact action only sets local state.";

    private sealed record Article(string Id, string Topic, string Title, string Summary, string Updated, string[] Sections);

    private static readonly Article[] Articles =
    {
        new("invoice", "Billing", "Download an invoice or receipt",
            "Find every invoice for your workspace and download it as a PDF.",
            "Updated 2 days ago · 4 min read",
            new[]
            {
                "Every charge appears under Billing, then Invoices, with the period it covers and its status. Open any row to see the line items behind the total.",
                "Invoices are issued on the first day of the period and receipts follow a successful payment. Both use the billing address on file, so update it before the next period starts."
            }),
        new("sso", "Security", "Set up single sign-on",
            "Connect your identity provider and require SSO for everyone.",
            "Updated 6 days ago · 6 min read",
            new[]
            {
                "Single sign-on is available on Scale. Add your provider metadata, map the email claim, then test with one seat before enforcing SSO for the workspace.",
                "Keep one password owner outside SSO until the test passes; that account can recover the workspace if the provider configuration is wrong."
            }),
        new("slack", "Integrations", "Connect Slack for release notes",
            "Post each published release into a channel your team watches.",
            "Updated 1 week ago · 3 min read",
            new[]
            {
                "Open Integrations, choose Slack and pick a channel. Release notes are posted when an entry is published, not when it is drafted.",
                "Disconnecting stops future posts and leaves the history in Slack. Nothing is deleted from the changelog."
            }),
        new("seats", "Team", "Add or remove seats",
            "Change the number of seats and understand the prorated charge.",
            "Updated 3 weeks ago · 2 min read",
            new[]
            {
                "Seats are billed per active member. Adding a seat mid-period charges the prorated difference today; removing one credits the unused days to the next invoice.",
                "Deactivating a member frees a seat immediately. Their history stays in the workspace and can be restored for 90 days."
            })
    };

    private sealed record Topic(string Name, string Detail, string Icon, string Category);

    private static readonly Topic[] Topics =
    {
        new("Billing", "Invoices, plans and payment", "credit-card", "Billing"),
        new("Team and access", "Seats, roles and single sign-on", "users", "Team"),
        new("Integrations", "Connect the tools you use", "layers", "Integrations")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var query = string.Empty;
        var feedbackGiven = false;
        var status = Ck.Status();

        var body = new ContentControl();
        var searchHost = new ContentControl();

        Control Header(bool compact)
        {
            var search = Ck.Search("Search help", text =>
            {
                query = text;
                if (!compact)
                {
                    RenderHome();
                }
            }, compact ? 260 : 400);

            var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
            bar.Children.Add(Ck.Row(8,
                Ck.Mark("compass", 30, 15),
                Ck.Text("Northwind help", "arc-body-sm arc-medium")));
            bar.Children.Add(Place(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children = { search }
            }, 1));
            bar.Children.Add(Place(Ck.Btn("Contact support", ArcButtonVariant.Secondary, ArcButtonSize.Sm,
                () => Ck.Say(status, "The reference opens your support channel; this specimen simulates a request."), height: 34), 2));
            return bar;
        }

        void RenderHome()
        {
            var matches = Articles
                .Where(article => string.IsNullOrWhiteSpace(query) ||
                                  article.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                  article.Summary.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var results = new StackPanel { Spacing = 2 };
            foreach (var article in matches.Take(3))
            {
                var target = article;
                results.Children.Add(new ArcButton
                {
                    Variant = ArcButtonVariant.Ghost,
                    Padding = new Thickness(8),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(10),
                    Content = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        ColumnSpacing = 10,
                        Children =
                        {
                            Ck.Disc("file", 26, 12),
                            Place(new StackPanel
                            {
                                Spacing = 0,
                                Children = { Ck.Text(article.Title, "arc-body-sm"), Ck.Caption(article.Topic) }
                            }, 1),
                            Place(new ArcIcon { Icon = "chevron-right", Size = 14 }, 2)
                        }
                    }
                }.With(button => button.Click += (_, _) => Open(target)));
            }

            var topics = new List<Control>();
            foreach (var topic in Topics)
            {
                var target = topic;
                topics.Add(new ArcButton
                {
                    Variant = ArcButtonVariant.Ghost,
                    Padding = new Thickness(12),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Top,
                    Background = Ck.Brush(ArcTokens.SurfaceRaised),
                    BorderBrush = Ck.Brush(ArcTokens.Border),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Content = new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            Ck.Disc(topic.Icon, 34, 16, accent: true),
                            Ck.Text(topic.Name, "arc-body-sm arc-medium"),
                            Ck.Caption(topic.Detail)
                        }
                    }
                }.With(button => button.Click += (_, _) => Ck.Say(status, $"{target.Name} opens its topic list in the reference.")));
            }

            body.Content = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            Ck.Text("How can we help?", "arc-title-sm"),
                            new ArcSearchField
                            {
                                Placeholder = "Search articles, for example invoice",
                                Width = 460,
                                ShortcutHint = "⌘K"
                            }.With(field => field.ValueChanged += (_, _) =>
                            {
                                query = field.Value ?? string.Empty;
                                RenderHome();
                            }),
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Spacing = 6,
                                Children =
                                {
                                    Ck.Caption("Try"),
                                    Ck.Link("invoice", () =>
                                    {
                                        query = "invoice";
                                        RenderHome();
                                        Ck.Say(status, "Search set to invoice.");
                                    }),
                                    Ck.Link("single sign-on", () =>
                                    {
                                        query = "single sign-on";
                                        RenderHome();
                                        Ck.Say(status, "Search set to single sign-on.");
                                    }),
                                    Ck.Link("slack", () =>
                                    {
                                        query = "slack";
                                        RenderHome();
                                        Ck.Say(status, "Search set to slack.");
                                    })
                                }
                            }
                        }
                    },
                    Ck.Card(new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            Ck.Row(8, Ck.Label(query.Length == 0 ? "Popular articles" : $"Results for “{query}”"),
                                Ck.Caption($"{matches.Count} articles")),
                            results.Children.Count == 0
                                ? Ck.Caption("No article matches yet. Try a shorter phrase or contact support.")
                                : results
                        }
                    }, 12),
                    new StackPanel { Spacing = 4, Children = { Ck.Label("Browse topics"), Ck.GridOf(3, 10, topics) } }
                }
            };
        }

        void Open(Article article)
        {
            feedbackGiven = false;
            var current = 0;
            var outlineHost = new StackPanel { Spacing = 2 };

            void BuildOutline()
            {
                outlineHost.Children.Clear();
                for (var index = 0; index < article.Sections.Length; index++)
                {
                    var slot = index;
                    var active = slot == current;
                    outlineHost.Children.Add(Ck.Link($"Section {slot + 1}", () =>
                    {
                        current = slot;
                        BuildOutline();
                        Ck.Say(status, $"Reading {article.Title}, section {slot + 1}.");
                    }, active ? "circle-dot" : null));
                }
            }

            BuildOutline();

            var feedback = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    Ck.Text("Was this helpful?", "arc-body-sm arc-medium"),
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            Ck.Btn("Yes", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
                            {
                                feedbackGiven = true;
                                Ck.Say(status, "Thanks. Your vote was recorded (sample).");
                            }, icon: "thumbs-up", height: 32),
                            Ck.Btn("No", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
                            {
                                feedbackGiven = true;
                                Ck.Say(status, "Thanks. We will look at this article again.");
                            }, icon: "message-circle", height: 32)
                        }
                    }
                }
            };

            body.Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    Ck.Link("All topics", RenderHome, "arrow-left"),
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,24,220"),
                        Children =
                        {
                            new StackPanel
                            {
                                Spacing = 8,
                                Children =
                                {
                                    Ck.Caption($"Help / {article.Topic}"),
                                    Ck.Text(article.Title, "arc-subtitle"),
                                    Ck.Caption(article.Updated),
                                    Ck.Divider(),
                                    Ck.Text("Overview", "arc-body arc-medium"),
                                    Ck.Body(article.Sections[0], 560),
                                    Ck.Text("Details", "arc-body arc-medium"),
                                    Ck.Body(article.Sections[1], 560),
                                    Ck.Divider(),
                                    feedback
                                }
                            },
                            Place(new StackPanel
                            {
                                Spacing = 8,
                                Children =
                                {
                                    Ck.Label("On this page"),
                                    outlineHost,
                                    Ck.Divider(),
                                    Ck.Caption("Still stuck?"),
                                    Ck.Link("Contact support", () => Ck.Say(status, "A support request would be created here."))
                                }
                            }, 2)
                        }
                    }
                }
            };
        }

        RenderHome();

        return Ck.Specimen(new StackPanel
        {
            Spacing = 14,
            Children = { Header(false), Ck.Divider(), body, status }
        }, 16);
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
