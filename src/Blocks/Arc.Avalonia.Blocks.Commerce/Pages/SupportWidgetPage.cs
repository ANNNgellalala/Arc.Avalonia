using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
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
/// Support widget: an in-app help launcher that opens into article search, an article reader and a chat with
/// the team. The stage behind it is decoration that stands in for the host app; articles, the team and replies
/// are sample data, and sending appends a message locally.
/// </summary>
public sealed class SupportWidgetPage : ArcItemPage
{
    public override string ItemSlug => "support-widget";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "An in-app help launcher with article search, an article reader and chat.",
        "SaaS products that want one help entry point mounted near the app root."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "A full page support chat; use support-conversation."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The open panel is a dialog whose label switches between Help and the conversation; Escape closes it and returns focus to the launcher.",
        "Arrow keys move from the search into the article list and between articles, and Enter opens the highlighted one."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The launcher grows into the panel and views slide in the direction of travel.",
        "Reduced motion replaces the scale and slide with plain fades."
    };

    public override string? ImplementationNotes =>
        "The panel is positioned inside the specimen stage rather than fixed to the gallery window, so the demo stays inside its shot. Articles and replies are sample data; switching views rebuilds the panel content and agent replies are simulated with a local timer.";

    private sealed record Article(string Title, string Category, string Body);

    private static readonly Article[] Articles =
    {
        new("Fix a failed payment", "Billing", "Open Billing, then Invoices. A failed charge shows a Retry button next to it, and the card on file can be replaced without starting a new subscription."),
        new("Invite a teammate", "Team", "Settings, then Members. Invitations expire after seven days; resend one from the pending list rather than creating a second invite."),
        new("Connect Slack", "Integrations", "Integrations, then Slack. Pick a channel for release notes and a second one for billing alerts if you want them separate.")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var view = "home";
        var query = string.Empty;
        var unread = 1;
        var messages = new List<(string Author, string Text, bool Mine)>
        {
            ("Maya", "Hi, you are through to the support team. What can we look at?", false),
            ("You", "My card was declined but the plan still shows as active.", true),
            ("Maya", "Thanks. I can see the retry succeeded two minutes later, so you are all set.", false)
        };
        var status = Ck.Status();

        var panelHost = new ContentControl();
        var panel = new Border
        {
            Width = 360,
            Height = 404,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 12),
            Background = Ck.Brush(ArcTokens.Surface),
            BorderBrush = Ck.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            BoxShadow = ArcResources.Shadows(ArcTokens.ShadowFloating),
            ClipToBounds = true,
            Child = panelHost
        };

        var launcher = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Width = 48,
            Height = 48,
            MinHeight = 48,
            CornerRadius = new CornerRadius(24),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsVisible = false,
            Content = new ArcIcon { Icon = "message-circle", Size = 20, Foreground = Ck.Brush(ArcTokens.AccentForeground) }
        };
        AutomationSetName(launcher, "Open help, one unread reply");

        void Close()
        {
            panel.IsVisible = false;
            launcher.IsVisible = true;
            Ck.Say(status, "Help closed.");
        }

        launcher.Click += (_, _) =>
        {
            panel.IsVisible = true;
            launcher.IsVisible = false;
            unread = 0;
            Render();
            Ck.Say(status, "Help opened.");
        };

        Control Header(string title)
        {
            var back = Ck.IconBtn("chevron-left", "Back to help", () =>
            {
                view = "home";
                Render();
            }, 28);
            var close = Ck.IconBtn("x", "Close help", Close, 28);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 6 };
            row.Children.Add(view == "home" ? Ck.Disc("message-circle", 28, 14, accent: true) : back);
            row.Children.Add(Place(Ck.Text(title, "arc-body-sm arc-medium"), 1));
            row.Children.Add(Place(close, 2));
            return row;
        }

        void RenderHome()
        {
            var matches = Articles
                .Where(article => string.IsNullOrWhiteSpace(query) ||
                                  article.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var list = new StackPanel { Spacing = 4 };
            foreach (var article in matches)
            {
                var target = article;
                list.Children.Add(new ArcButton
                {
                    Variant = ArcButtonVariant.Ghost,
                    Padding = new Thickness(8),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(10),
                    Content = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        ColumnSpacing = 8,
                        Children =
                        {
                            Ck.Disc("file", 24, 11),
                            Place(new StackPanel
                            {
                                Spacing = 0,
                                Children = { Ck.Text(article.Title, "arc-body-sm"), Ck.Caption(article.Category) }
                            }, 1),
                            Place(new ArcIcon { Icon = "chevron-right", Size = 13 }, 2)
                        }
                    }
                }.With(button => button.Click += (_, _) =>
                {
                    view = article.Title;
                    Render();
                }));
            }

            panelHost.Content = new StackPanel
            {
                Spacing = 10,
                Margin = new Thickness(14),
                Children =
                {
                    Header("Help"),
                    Ck.Search("Search help", text =>
                    {
                        query = text;
                        Render();
                    }),
                    Ck.Caption("Popular articles"),
                    list,
                    Ck.Divider(),
                    Ck.Btn("Chat with the team", ArcButtonVariant.Secondary, ArcButtonSize.Md, () =>
                    {
                        view = "chat";
                        Render();
                    }, icon: "message-circle"),
                    Ck.Caption("Usually replies in a few minutes")
                }
            };
        }

        void RenderArticle(Article article)
        {
            panelHost.Content = new StackPanel
            {
                Spacing = 10,
                Margin = new Thickness(14),
                Children =
                {
                    Header(article.Category),
                    Ck.Text(article.Title, "arc-body arc-medium"),
                    Ck.Caption(article.Category + " · 2 min read"),
                    Ck.Divider(),
                    Ck.Body(article.Body, 300),
                    Ck.Row(8,
                        Ck.Btn("Yes", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => Ck.Say(status, "Thanks. Vote recorded (sample)."), height: 30),
                        Ck.Btn("No", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () => Ck.Say(status, "Thanks. We will look at this article."), height: 30),
                        Ck.Caption("Was this helpful?"))
                }
            };
        }

        void RenderChat()
        {
            var thread = new StackPanel { Spacing = 6 };
            foreach (var message in messages)
            {
                var bubble = new Border
                {
                    Background = message.Mine ? Ck.Tint(ArcTokens.Accent, 0.12) : Ck.Brush(ArcTokens.SurfaceMuted),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(9, 6, 9, 6),
                    MaxWidth = 250,
                    HorizontalAlignment = message.Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    Child = Ck.Text(message.Text, "arc-caption")
                };
                thread.Children.Add(bubble);
            }

            var composer = Ck.Input("Write a message", null, null, null);
            composer.HorizontalAlignment = HorizontalAlignment.Stretch;

            void SendMessage()
            {
                var text = (composer.Value ?? string.Empty).Trim();
                if (text.Length == 0)
                {
                    return;
                }

                messages.Add(("You", text, true));
                composer.Value = string.Empty;
                Render();
                Ck.Say(status, "Message sent to the support team.");
            }

            panelHost.Content = new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(14),
                Children =
                {
                    Header("Support"),
                    Ck.Row(8,
                        Ck.Person("Maya Chen", ArcAvatarSize.Xs),
                        Ck.Text("Maya Chen", "arc-caption"),
                        Ck.Pill("Online", ArcBadgeTone.Success, true)),
                    thread,
                    Ck.Caption("Maya is typing…"),
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        ColumnSpacing = 6,
                        Children =
                        {
                            composer,
                            Place(Ck.Btn("Send", ArcButtonVariant.Primary, ArcButtonSize.Sm, SendMessage, icon: "send", height: 36), 1)
                        }
                    }
                }
            };
        }

        void Render()
        {
            if (view == "home")
            {
                RenderHome();
                return;
            }

            if (view == "chat")
            {
                RenderChat();
                return;
            }

            var article = Articles.FirstOrDefault(item => item.Title == view) ?? Articles[0];
            RenderArticle(article);
        }

        Render();

        // The host app is decoration: a header and two placeholder cards behind the dock.
        var appBackground = new Border
        {
            Background = Ck.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            Ck.Row(8, Ck.Mark("layers", 26, 13), Ck.Text("Acme workspace", "arc-body-sm arc-medium")),
                            Place(Ck.Row(8,
                                Ck.Caption("Search"),
                                Ck.Person("Ana Ribeiro", ArcAvatarSize.Xs)), 1)
                        }
                    },
                    Ck.Row(8,
                        Ck.Pill("Overview", ArcBadgeTone.Neutral),
                        Ck.Caption("Projects"),
                        Ck.Caption("Reports")),
                    new StackPanel
                    {
                        Spacing = 10,
                        Children =
                        {
                            new Grid
                            {
                                ColumnDefinitions = new ColumnDefinitions("*,*"),
                                ColumnSpacing = 12,
                                Children =
                                {
                                    Placeholder("Revenue", "$48,210", "12% above plan"),
                                    Place(Ck.Card(new StackPanel
                                    {
                                        Spacing = 6,
                                        Children =
                                        {
                                            Ck.Caption("Open invoices"),
                                            Ck.Text("12", "arc-subtitle"),
                                            Ck.Caption("3 need attention")
                                        }
                                    }, 14), 1)
                                }
                            },
                            Placeholder("Activity", "248 events", "Across 14 projects this week")
                        }
                    }
                }
            }
        };

        var stage = new Panel
        {
            Width = 1000,
            Height = 520,
            Children = { appBackground, panel, launcher }
        };

        return stage;
    }

    private static Control Placeholder(string label, string value, string note)
    {
        return Ck.Card(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Ck.Caption(label),
                Ck.Text(value, "arc-subtitle"),
                Ck.Caption(note),
                new Border
                {
                    Height = 42,
                    CornerRadius = new CornerRadius(10),
                    Background = Ck.Brush(ArcTokens.SurfaceMuted)
                }
            }
        }, 14);
    }

    private static void AutomationSetName(Control control, string name) =>
        AutomationProperties.SetName(control, name);

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
