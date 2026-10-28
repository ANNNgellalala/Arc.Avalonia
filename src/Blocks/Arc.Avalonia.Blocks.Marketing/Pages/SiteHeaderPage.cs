using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Arc.Avalonia.Blocks.Marketing.Controls;
using Mk = Arc.Avalonia.Blocks.Marketing.MarketingKit;

namespace Arc.Avalonia.Blocks.Marketing.Pages;

/// <summary>
/// Site header: a sticky marketing bar that turns solid past the scroll threshold, glides its current link,
/// opens mega menu panels with a short hover intent, and folds into a sheet on a narrow container. The
/// specimen shows the header over a little page context so both states read, with switches for the scroll
/// state and the narrow container. Approximations: the header sits in a fixed-height specimen rather than
/// sticking to a live scroller, and the solid state is a switch rather than a scroll listener.
/// </summary>
public sealed class SiteHeaderPage : ArcItemPage
{
    public override string ItemSlug => "site-header";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The top bar of a marketing site, landing page, or docs site.",
        "Product sites with grouped destinations that deserve a mega menu with a feature card.",
        "Pages that need one clear sign-up action in the header."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Left and right arrows move between top level items; down opens a panel and moves into it.",
        "Escape closes the panel or the folded sheet and returns focus to its trigger.",
        "The nav landmark is labelled and the current item is marked as the current page."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Panels open after a short hover intent delay and morph between sections on the smooth spring.",
        "The bar turns solid past the scroll threshold; reduced motion drops panel travel and uses a fade."
    };

    public override string? ImplementationNotes =>
        "The mega menu is the shared ArcMegaMenu control from the Actions project, so panel morph, hover intent, safe triangle and the folded sheet are the real component. The specimen supplies the scroll state and the narrow container with switches instead of wiring a scroll listener, and the page beneath the header is sample context. No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var menu = new ArcMegaMenu
        {
            Brand = "Acme",
            BrandIcon = "layers",
            Label = "Main",
            Current = "product",
            Sections = new List<ArcMegaMenuSection>
            {
                new()
                {
                    Value = "product",
                    Label = "Product",
                    Groups = new List<ArcMegaMenuGroup>
                    {
                        new()
                        {
                            Title = "Platform",
                            Links = new List<ArcMegaMenuLink>
                            {
                                Link("Automations", "Run work when events land", "zap"),
                                Link("Workflows", "Chain steps with conditions", "move-3d"),
                                Link("Data", "One source for every team", "database"),
                                Link("Permissions", "Scoped access by role", "shield-check")
                            }
                        },
                        new()
                        {
                            Title = "Build",
                            Links = new List<ArcMegaMenuLink>
                            {
                                Link("API", "Typed endpoints and webhooks", "code"),
                                Link("SDKs", "Libraries for six runtimes", "package"),
                                Link("Changelog", "What shipped this week", "activity", "New")
                            }
                        }
                    },
                    Feature = new ArcMegaMenuFeature
                    {
                        Title = "Arc 3.0",
                        Description = "The release that connects specs, tasks and releases.",
                        Cta = "Read the announcement",
                        Icon = "sparkles"
                    },
                    Footer = "Trusted by 4,000 product teams."
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
                                Link("Product", "Plans, specs and bets", "layers"),
                                Link("Engineering", "Roadmap to release", "code"),
                                Link("Design", "Critique in one place", "palette"),
                                Link("Support", "Close the loop faster", "headphones")
                            }
                        }
                    },
                    Footer = "Built for teams of 5 to 5,000."
                },
                new() { Value = "docs", Label = "Docs" },
                new() { Value = "pricing", Label = "Pricing" }
            },
            Actions = new List<ArcMegaMenuAction>
            {
                new() { Label = "Sign in", Variant = ArcMegaMenuActionVariant.Quiet },
                new() { Label = "Start free", Variant = ArcMegaMenuActionVariant.Primary }
            }
        };

        var bar = new Border
        {
            Child = menu,
            Padding = new Thickness(6, 4, 6, 4),
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(0, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Top
        };

        var pageContext = new Border
        {
            Background = Mk.Brush(ArcTokens.SurfaceMuted),
            Padding = new Thickness(32, 92, 32, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 12,
                Children =
                {
                    Mk.Text("Plan, build and ship in one place.", "arc-subtitle", 430),
                    Mk.Body("Arc keeps specs, tasks and releases on one surface, so the whole team sees the same version of the work.", 440),
                    Mk.Row(10,
                        Mk.Btn("Start free"),
                        Mk.Btn("Book a demo", ArcButtonVariant.Secondary)),
                    Mk.Row(14,
                        Mk.Meta("shield-check", "SOC 2 Type II"),
                        Mk.Meta("globe", "EU and US regions"),
                        Mk.Meta("users", "4,000 teams"))
                }
            }
        };

        var stage = new Grid { Height = 292 };
        stage.Children.Add(pageContext);

        var solid = false;
        void ApplyBar()
        {
            bar.Background = solid ? Mk.Brush(ArcTokens.Surface) : Mk.Tint(ArcTokens.Surface, 0);
            bar.BorderThickness = new Thickness(0, 0, 0, solid ? 1 : 0);
            bar.BorderBrush = Mk.Brush(ArcTokens.BorderSubtle);
        }

        ApplyBar();
        stage.Children.Add(bar);

        var scrolled = new ArcSwitch { Label = "Scrolled", Checked = false };
        scrolled.CheckedChanged += (_, _) =>
        {
            solid = scrolled.Checked;
            ApplyBar();
        };

        var narrow = new ArcSwitch { Label = "Narrow container", Checked = false };
        narrow.CheckedChanged += (_, _) => menu.ForceNarrow = narrow.Checked;

        return Mk.Stack(14,
            Mk.Row(20, scrolled, narrow),
            Mk.Frame("acme.com", stage));
    }

    private static ArcMegaMenuLink Link(string label, string description, string icon, string? badge = null) => new()
    {
        Label = label,
        Description = description,
        Icon = icon,
        Badge = badge
    };
}

