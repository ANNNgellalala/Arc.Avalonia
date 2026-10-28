using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// The mega menu morph: a site header whose sections share one floating menu, plus the folded accordion
/// sheet the header falls back to. Both are hosted in the page, so the open panel is visible in a still.
/// </summary>
public sealed class MegaMenuPage : ArcPage
{
    public override string Title => "Mega menu";

    public override string Group => "Navigation";

    public override string? Description =>
        "A marketing header where four sections share one floating panel: the surface morphs to the next " +
        "menu, the caret glides under its trigger and the folded bar turns the sections into an accordion " +
        "sheet. The panel is hosted in the header's own overlay canvas, not a popup window.";

    public override int Order => 206;

    public override Control Build(ArcDemoContext context)
    {
        var status = new TextBlock
        {
            Text = "Products menu open. Hover another section or choose a link.",
            Classes = { "arc-caption" },
            Margin = new Thickness(2, 10, 0, 0)
        };

        var header = BuildHeader(status);

        var page = new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Mega menu morph",
                    "Use it as the top navigation of a marketing or product site with three to six sections, " +
                    "where each section holds a few destinations worth describing. Keep the menus different " +
                    "in shape on purpose: a wide menu with two groups beside a narrow single list is what " +
                    "makes the morph read.",
                    new StackPanel
                    {
                        Children =
                        {
                            ActionDemos.Stage(1000, 392, mockPage: false).WithChildren(HeaderPage(), header),
                            status
                        }
                    }),
                DemoUi.Section(
                    "Folded header",
                    "Below the natural width of the row the header folds: the primary action stays in the bar, " +
                    "quiet actions move to the bottom of the sheet, and one accordion section opens at a time. " +
                    "ForceNarrow pins this layout for the demo.",
                    BuildNarrow())
            }
        };

        return page;
    }

    private static Control HeaderPage()
    {
        var panel = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(32, 296, 32, 20)
        };

        var cards = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 0) };
        foreach (var (title, copy) in new[]
                 {
                     ("46 currencies", "Local pricing without a second processor."),
                     ("Same day payouts", "Sellers get paid while the order is fresh."),
                     ("Usage billing", "Metered plans and invoices in one place.")
                 })
        {
            var card = new Border
            {
                Width = 290,
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 12, 0),
                CornerRadius = new CornerRadius(18),
                Background = DemoUi.Brush(ArcTokens.Surface),
                BorderBrush = DemoUi.Brush(ArcTokens.BorderSubtle),
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock { Text = title, Classes = { "arc-body-sm", "arc-medium" } },
                        new TextBlock
                        {
                            Text = copy,
                            Classes = { "arc-caption" },
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            };
            cards.Children.Add(card);
        }

        panel.Children.Add(cards);
        return panel;
    }

    private static ArcMegaMenu BuildHeader(TextBlock status)
    {
        var header = new ArcMegaMenu
        {
            Brand = "Tally",
            BrandIcon = "layers",
            Label = "Main",
            Current = "developers",
            OpenDelay = 90,
            CloseDelay = 240,
            IsOpen = true,
            ActiveSection = "products",
            Sections = Sections(),
            Actions = new List<ArcMegaMenuAction>
            {
                new() { Label = "Sign in", Variant = ArcMegaMenuActionVariant.Quiet },
                new() { Label = "Start now", Variant = ArcMegaMenuActionVariant.Primary }
            },
            Margin = new Thickness(12)
        };

        header.SectionChanged += (_, value) =>
        {
            var label = value is null ? "All menus closed" : $"{TitleCase(value)} menu open";
            status.Text = $"{label}. Hover another section or choose a link.";
        };

        header.ItemInvoked += (_, args) =>
        {
            var destination = args.Destination;
            status.Text = destination.Section is null
                ? $"Chose {destination.Label}."
                : $"Chose {destination.Label} in {TitleCase(destination.Section)}.";
        };

        return header;
    }

    private static string TitleCase(string? value) =>
        string.IsNullOrEmpty(value)
            ? "Section"
            : char.ToUpperInvariant(value[0]) + value[1..];

    private static IList<ArcMegaMenuSection> Sections() => new List<ArcMegaMenuSection>
    {
        new()
        {
            Value = "products",
            Label = "Products",
            Groups = new List<ArcMegaMenuGroup>
            {
                new()
                {
                    Title = "Payments",
                    Links = new List<ArcMegaMenuLink>
                    {
                        new() { Label = "Checkout", Description = "Hosted pages in 46 currencies", Icon = "credit-card" },
                        new() { Label = "Billing", Description = "Subscriptions and invoices", Icon = "receipt" },
                        new() { Label = "Payouts", Description = "Send money to any seller", Icon = "send" }
                    }
                },
                new()
                {
                    Title = "Platform",
                    Links = new List<ArcMegaMenuLink>
                    {
                        new() { Label = "Connect", Description = "Payments for platforms", Icon = "layers" },
                        new() { Label = "Tax", Description = "Calculate and file tax", Icon = "file" },
                        new() { Label = "Radar", Description = "Fraud checks on charges", Icon = "shield-check" }
                    }
                }
            },
            Feature = new ArcMegaMenuFeature
            {
                Title = "Instant payouts in the EU",
                Description = "Sellers now get paid in seconds, not days.",
                Cta = "Read the announcement",
                Icon = "zap"
            }
        },
        new()
        {
            Value = "solutions",
            Label = "Solutions",
            Groups = new List<ArcMegaMenuGroup>
            {
                new()
                {
                    Title = "By team",
                    Links = new List<ArcMegaMenuLink>
                    {
                        new() { Label = "Startups", Description = "Launch without finance", Icon = "trending-up" },
                        new() { Label = "Enterprise", Description = "Controls and audit trails", Icon = "shield" },
                        new() { Label = "Marketplaces", Description = "Split payments between sellers", Icon = "users" }
                    }
                }
            },
            Footer = "All systems normal"
        },
        new()
        {
            Value = "developers",
            Label = "Developers",
            Groups = new List<ArcMegaMenuGroup>
            {
                new()
                {
                    Title = "Documentation",
                    Links = new List<ArcMegaMenuLink>
                    {
                        new() { Label = "Quickstart", Description = "Live in ten minutes", Icon = "zap" },
                        new() { Label = "API reference", Description = "Every endpoint on one page", Icon = "code" },
                        new() { Label = "Changelog", Description = "What shipped this week", Icon = "list", Badge = "New" }
                    }
                }
            },
            Feature = new ArcMegaMenuFeature
            {
                Title = "Build on the API",
                Description = "Typed SDKs for six languages, with webhooks.",
                Cta = "Read the docs",
                Icon = "code"
            }
        },
        new()
        {
            Value = "pricing",
            Label = "Pricing",
            Href = "/pricing"
        }
    };

    private static Control BuildNarrow()
    {
        var header = new ArcMegaMenu
        {
            Brand = "Tally",
            BrandIcon = "layers",
            Label = "Folded main",
            Current = "products",
            ForceNarrow = true,
            IsOpen = true,
            ActiveSection = "products",
            Sections = NarrowSections(),
            Actions = new List<ArcMegaMenuAction>
            {
                new() { Label = "Sign in", Variant = ArcMegaMenuActionVariant.Quiet },
                new() { Label = "Start now", Variant = ArcMegaMenuActionVariant.Primary }
            },
            Margin = new Thickness(12)
        };

        var stage = ActionDemos.Stage(460, 390, mockPage: false);
        stage.Children.Add(new StackPanel
        {
            Margin = new Thickness(24, 24, 24, 24),
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "The folded sheet opens one section at a time and scrolls inside itself.",
                    Classes = { "arc-caption" },
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 360
                }
            }
        });
        stage.Children.Add(header);
        return stage;
    }

    private static IList<ArcMegaMenuSection> NarrowSections() => new List<ArcMegaMenuSection>
    {
        new()
        {
            Value = "products",
            Label = "Products",
            Groups = new List<ArcMegaMenuGroup>
            {
                new()
                {
                    Title = "Payments",
                    Links = new List<ArcMegaMenuLink>
                    {
                        new() { Label = "Checkout", Description = "Hosted pages in 46 currencies", Icon = "credit-card" },
                        new() { Label = "Billing", Description = "Subscriptions and invoices", Icon = "receipt" }
                    }
                }
            }
        },
        new()
        {
            Value = "solutions",
            Label = "Solutions",
            Groups = new List<ArcMegaMenuGroup>
            {
                new()
                {
                    Title = "By team",
                    Links = new List<ArcMegaMenuLink>
                    {
                        new() { Label = "Startups", Description = "Launch without finance", Icon = "trending-up" },
                        new() { Label = "Enterprise", Description = "Controls and audit trails", Icon = "shield" }
                    }
                }
            }
        },
        new()
        {
            Value = "pricing",
            Label = "Pricing",
            Href = "/pricing"
        }
    };
}

/// <summary>Small panel helper so a demo stage can hold background content and an overlay control.</summary>
internal static class ArcDemoStageExtensions
{
    public static Panel WithChildren(this Panel panel, params Control[] children)
    {
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }
}




