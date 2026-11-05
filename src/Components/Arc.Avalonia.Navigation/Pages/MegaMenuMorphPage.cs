using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Pages;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Navigation.Controls;

namespace Arc.Avalonia.Navigation.Pages;

/// <summary>
/// Mega menu's completeness preview: a wide header with the shared menu pinned open on Products (Current
/// marks Developers) and a forced-narrow header with the accordion sheet open. Both are hosted in fixed
/// stages because the panel lives in the header's own overlay canvas. Declared state keys match
/// Docs/mega-menu-morph.json so the audit can compare the two.
/// </summary>
public sealed class MegaMenuMorphPage : ArcItemPage
{
    public override string ItemSlug => "mega-menu-morph";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-wide", "variant-narrow", "state-open", "state-current"
    };

    public override bool DemoOnCanvas => false;

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Labelled("宽屏页眉", "Products 菜单展开，Developers 是当前分区。", Wide()));
        panel.Children.Add(Labelled("折叠页眉", "ForceNarrow 下分区折进手风琴式抽屉，一次只开一段。", Narrow()));
        return panel;
    }

    private static Control Wide()
    {
        var stage = ActionDemos.Stage(860, 380, mockPage: false);

        var header = new ArcMegaMenu
        {
            Brand = "Tally",
            BrandIcon = "layers",
            Label = "Main",
            Current = "developers",
            IsOpen = true,
            ActiveSection = "products",
            CollapseBelow = 420,
            Sections = Sections(),
            Actions = Actions(),
            Margin = new Thickness(12)
        };

        stage.Children.Add(header);
        return stage;
    }

    private static Control Narrow()
    {
        var stage = ActionDemos.Stage(430, 360, mockPage: false);

        var header = new ArcMegaMenu
        {
            Brand = "Tally",
            BrandIcon = "layers",
            Label = "Folded main",
            Current = "products",
            ForceNarrow = true,
            IsOpen = true,
            ActiveSection = "products",
            Sections = Sections(),
            Actions = Actions(),
            Margin = new Thickness(10)
        };

        stage.Children.Add(header);
        return stage;
    }

    private static IList<ArcMegaMenuAction> Actions() => new List<ArcMegaMenuAction>
    {
        new() { Label = "Sign in", Variant = ArcMegaMenuActionVariant.Quiet },
        new() { Label = "Start now", Variant = ArcMegaMenuActionVariant.Primary }
    };

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
                        new() { Label = "Billing", Description = "Subscriptions and invoices", Icon = "receipt" }
                    }
                },
                new()
                {
                    Title = "Platforms",
                    Links = new List<ArcMegaMenuLink>
                    {
                        new() { Label = "Connect", Description = "Payments for platforms", Icon = "layers" },
                        new() { Label = "Tax", Description = "Calculate and file tax", Icon = "file" }
                    }
                }
            },
            Feature = new ArcMegaMenuFeature
            {
                Title = "Tally 2.0",
                Description = "One dashboard for every currency.",
                Cta = "Read more",
                Icon = "zap"
            }
        },
        new()
        {
            Value = "developers",
            Label = "Developers",
            Groups = new List<ArcMegaMenuGroup>
            {
                new()
                {
                    Title = "Build",
                    Links = new List<ArcMegaMenuLink>
                    {
                        new() { Label = "Quickstart", Description = "Live in ten minutes", Icon = "zap" },
                        new() { Label = "API reference", Description = "Every endpoint on one page", Icon = "code" }
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

    private static Control Labelled(string label, string note, Control content)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        panel.Children.Add(content);
        panel.Children.Add(DemoUi.Caption(note));
        return panel;
    }
}
