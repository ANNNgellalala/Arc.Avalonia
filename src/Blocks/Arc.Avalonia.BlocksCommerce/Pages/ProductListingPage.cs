using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Ck = Arc.Avalonia.BlocksCommerce.CommerceKit;

namespace Arc.Avalonia.BlocksCommerce.Pages;

/// <summary>
/// Product listing: a collection page with a filter rail, a sort control and a product grid whose cards carry a
/// quick add size row. Filtering and sorting are in memory over sample products; the bag count is local state.
/// Photography is a token tile with the product glyph (no external images), and the hover photo swap is the
/// alternate tile crossfading in the same fixed frame.
/// </summary>
public sealed class ProductListingPage : ArcItemPage
{
    public override string ItemSlug => "product-listing";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A shop's collection or category page where colour and size are the main filters.",
        "Catalogs where quick add should save a trip to the product detail page."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "One product with many options; use product-detail instead."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Swatches, size chips and quick add are real buttons with pressed state and an accessible name.",
        "An addition is written to a polite live region with the product name and size; sold out is text, not colour."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The alternate photo crossfades for 160 ms inside the same frame, with no zoom.",
        "Filtering and sorting rebuild the grid without travel; reduced motion keeps the crossfade only."
    };

    public override string? ImplementationNotes =>
        "Sample products, colours and sizes live in the page. The grid shows four of eight products at gallery width because the filter rail plus a two row grid exceeds the 1400 px page budget; filtering and sorting still act on the whole catalog. Photos are token tiles with the product glyph, and the reference's dual price range is one maximum-price slider (ArcSlider has a single handle).";

    private sealed record Product(
        string Name,
        string Detail,
        double Price,
        double? CompareAt,
        string[] Colors,
        string[] Sizes,
        bool InStock,
        string? Badge,
        string Icon,
        string HoverIcon,
        int Rank,
        int Added);

    private sealed record ColorOption(string Id, string Name, string Token, double Alpha);

    private static readonly ColorOption[] ColorOptions =
    {
        new("ink", "Ink", ArcTokens.Foreground, 1),
        new("clay", "Clay", ArcTokens.Accent, 1),
        new("sand", "Sand", ArcTokens.Foreground, 0.28),
        new("mist", "Mist", ArcTokens.SurfaceMuted, 1)
    };

    private static readonly Product[] Products =
    {
        new("Halo table lamp", "Brushed brass, dimmable", 189, 220, new[] { "clay", "ink" }, new[] { "S", "M", "L" }, true,
            "New", "circle-dot", "zap", 1, 1),
        new("Linen throw", "Stone washed, 130 × 180", 84, null, new[] { "sand", "mist" }, new[] { "S", "M" }, true,
            null, "layers", "wind", 2, 2),
        new("Arc floor mirror", "Full length, oak frame", 320, null, new[] { "ink", "sand" }, new[] { "L" }, false,
            null, "scan", "maximize", 3, 3),
        new("Stoneware mug", "Set of two, matte glaze", 38, 46, new[] { "clay", "mist" }, new[] { "S", "M" }, true,
            "Sale", "circle", "droplet", 4, 4),
        new("Oak side table", "Solid oak, 46 cm", 245, null, new[] { "sand", "ink" }, new[] { "M", "L" }, true,
            null, "box", "columns-3", 5, 5),
        new("Woven basket", "Hand woven seagrass", 62, null, new[] { "sand" }, new[] { "S", "M", "L" }, true,
            null, "archive", "shopping-cart", 6, 6),
        new("Glass carafe", "Mouth blown, 1 litre", 54, null, new[] { "mist" }, new[] { "S", "M" }, true,
            null, "droplet", "circle", 7, 7),
        new("Wool cushion", "Undyed, 45 × 45", 72, 88, new[] { "sand", "ink" }, new[] { "S", "M", "L" }, true,
            "Sale", "layers", "circle", 8, 8)
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        string? color = null;
        string? size = null;
        var maxPrice = 340.0;
        var inStockOnly = false;
        var sort = "featured";
        var bag = 2;

        var bagText = new TextBlock
        {
            Text = bag.ToString(),
            Classes = { "arc-body-sm", "arc-medium" },
            VerticalAlignment = VerticalAlignment.Center
        };
        var bagButton = Ck.Btn("Bag", ArcButtonVariant.Secondary, ArcButtonSize.Sm, null, "shopping-cart", height: 34);
        ((StackPanel)bagButton.Content!).Children.Add(new Border
        {
            Background = Ck.Tint(ArcTokens.Accent, 0.14),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(7, 1, 7, 1),
            Child = bagText
        });

        var status = Ck.Status("Free returns within 30 days.");
        var countText = Ck.Caption(string.Empty);
        var gridHost = new ContentControl();
        var swatchHost = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var sizeHost = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var clearHolder = new ContentControl();
        var priceReadout = Ck.Caption(string.Empty);

        var priceSlider = new ArcSlider
        {
            Value = maxPrice,
            Min = 20,
            Max = 340,
            Step = 10,
            LargeStep = 40,
            Label = "Price",
            ShowValue = false
        };
        priceSlider.ValueChanged += (_, _) =>
        {
            maxPrice = Math.Round(priceSlider.Value / 10) * 10;
            priceReadout.Text = $"Up to {Ck.Money(maxPrice)}";
            BuildResults();
        };

        Control Card(Product product)
        {
            var added = false;
            var sizeRowOpen = false;
            var quickHost = new ContentControl();
            var productName = product.Name;

            void Add(string chosenSize)
            {
                added = true;
                bag++;
                bagText.Text = bag.ToString();
                Ck.Say(status, $"{productName}, size {chosenSize}, added to the bag. {bag} items.");
                BuildQuick();
            }

            void BuildQuick()
            {
                if (!product.InStock)
                {
                    quickHost.Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 6,
                        Children =
                        {
                            Ck.Btn("Sold out", ArcButtonVariant.Secondary, ArcButtonSize.Sm, null, height: 30),
                            Ck.Link("Notify", () => Ck.Say(status, $"We will email you when {productName} is back."))
                        }
                    };
                    return;
                }

                if (added)
                {
                    quickHost.Content = Ck.Link("Added to bag · add another", () =>
                    {
                        added = false;
                        sizeRowOpen = true;
                        BuildQuick();
                    }, "check");
                    return;
                }

                if (product.Sizes.Length <= 1)
                {
                    quickHost.Content = Ck.Btn("Add to bag", ArcButtonVariant.Secondary, ArcButtonSize.Sm,
                        () => Add(product.Sizes[0]), icon: "plus", height: 30);
                    return;
                }

                if (!sizeRowOpen)
                {
                    quickHost.Content = Ck.Btn("Quick add", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
                    {
                        sizeRowOpen = true;
                        BuildQuick();
                    }, icon: "plus", height: 30);
                    return;
                }

                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                row.Children.Add(Ck.Caption("Size"));
                foreach (var option in product.Sizes)
                {
                    row.Children.Add(new ArcButton
                    {
                        Variant = ArcButtonVariant.Secondary,
                        Size = ArcButtonSize.Sm,
                        Content = option,
                        Height = 28,
                        MinHeight = 28,
                        Width = 26,
                        Padding = new Thickness(0),
                        CornerRadius = new CornerRadius(8),
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center
                    }.With(button => button.Click += (_, _) => Add(option)));
                }

                quickHost.Content = row;
            }

            var swatches = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center
            };
            foreach (var id in product.Colors)
            {
                var option = ColorOptions.First(item => item.Id == id);
                swatches.Children.Add(new Border
                {
                    Width = 9,
                    Height = 9,
                    CornerRadius = new CornerRadius(5),
                    Background = Ck.Tint(option.Token, option.Alpha),
                    BorderBrush = Ck.Brush(ArcTokens.Border),
                    BorderThickness = new Thickness(1)
                });
            }

            var priceRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            priceRow.Children.Add(Ck.Text(Ck.Money(product.Price), "arc-body-sm arc-medium"));
            if (product.CompareAt is { } was)
            {
                var strike = Ck.Text(Ck.Money(was), "arc-caption");
                strike.TextDecorations = TextDecorations.Strikethrough;
                priceRow.Children.Add(strike);
            }

            if (product.Badge is { } badge)
            {
                priceRow.Children.Add(Ck.Pill(badge, badge == "Sale" ? ArcBadgeTone.Accent : ArcBadgeTone.Neutral));
            }

            var body = new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    Ck.HoverPhoto(product.Icon, product.HoverIcon, 188),
                    Ck.Text(product.Name, "arc-body-sm arc-medium", 160),
                    Ck.Caption(product.Detail),
                    priceRow,
                    Ck.Row(8, product.InStock ? Ck.Caption("In stock") : Ck.Caption("Sold out"), swatches),
                    quickHost
                }
            };

            BuildQuick();
            return new Border
            {
                Background = Ck.Brush(ArcTokens.SurfaceRaised),
                BorderBrush = Ck.Brush(ArcTokens.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(9),
                Child = body
            };
        }

        void BuildResults()
        {
            var matches = Products.Where(product =>
                (color is null || product.Colors.Contains(color)) &&
                (size is null || product.Sizes.Contains(size)) &&
                product.Price <= maxPrice &&
                (!inStockOnly || product.InStock)).ToList();

            matches = sort switch
            {
                "newest" => matches.OrderByDescending(product => product.Added).ToList(),
                "price-asc" => matches.OrderBy(product => product.Price).ToList(),
                "price-desc" => matches.OrderByDescending(product => product.Price).ToList(),
                _ => matches.OrderBy(product => product.Rank).ToList()
            };

            var visible = matches.Take(4).ToList();
            gridHost.Content = visible.Count == 0
                ? Ck.Card(new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        Ck.Disc("search", 34, 16),
                        Ck.Text("No products match those filters", "arc-body-sm arc-medium"),
                        Ck.Link("Clear filters", () => { color = null; size = null; inStockOnly = false; maxPrice = 340; priceSlider.Value = 340; BuildFilters(); BuildResults(); })
                    }
                }, 18)
                : Ck.GridOf(4, 12, visible.Select(Card).Cast<Control>().ToList());

            countText.Text = matches.Count == Products.Length
                ? $"Showing {visible.Count} of {matches.Count} products"
                : $"{matches.Count} of {Products.Length} products match";
        }

        void BuildFilters()
        {
            swatchHost.Children.Clear();
            foreach (var option in ColorOptions)
            {
                var isSelected = color == option.Id;
                swatchHost.Children.Add(Ck.Swatch(Ck.Tint(option.Token, option.Alpha), isSelected, () =>
                {
                    color = isSelected ? null : option.Id;
                    BuildFilters();
                    BuildResults();
                }, option.Name));
            }

            sizeHost.Children.Clear();
            foreach (var option in new[] { "S", "M", "L" })
            {
                var isSelected = size == option;
                sizeHost.Children.Add(Ck.Chip(option, isSelected, () =>
                {
                    size = isSelected ? null : option;
                    BuildFilters();
                    BuildResults();
                }));
            }

            clearHolder.Content = color is null && size is null && !inStockOnly && Math.Abs(maxPrice - 340) < 0.01
                ? null
                : Ck.Link("Clear all filters", () =>
                {
                    color = null;
                    size = null;
                    inStockOnly = false;
                    maxPrice = 340;
                    priceSlider.Value = maxPrice;
                    BuildFilters();
                    BuildResults();
                });
            priceReadout.Text = $"Up to {Ck.Money(maxPrice)}";
        }

        var inStockSwitch = Ck.Toggle("In stock only", inStockOnly, value =>
        {
            inStockOnly = value;
            BuildResults();
            BuildFilters();
        });

        var sidebar = new StackPanel
        {
            Spacing = 14,
            Width = 200,
            Children =
            {
                new StackPanel
                {
                    Spacing = 6,
                    Children = { priceSlider, priceReadout }
                },
                Ck.Divider(),
                new StackPanel { Spacing = 8, Children = { Ck.Label("Colour"), swatchHost } },
                Ck.Divider(),
                new StackPanel { Spacing = 8, Children = { Ck.Label("Size"), sizeHost } },
                inStockSwitch,
                clearHolder
            }
        };

        var sortControl = Ck.Segmented(
            new[] { ("featured", "Featured"), ("newest", "Newest"), ("price-asc", "Price ↑") },
            sort,
            value =>
            {
                sort = value;
                BuildResults();
            });

        BuildFilters();
        BuildResults();

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new StackPanel
        {
            Spacing = 3,
            Children =
            {
                Ck.Heading("Home goods", 460, "arc-subtitle"),
                Ck.Caption("Lighting, textiles and small furniture. Free returns within 30 days.")
            }
        });
        bagButton.HorizontalAlignment = HorizontalAlignment.Right;
        bagButton.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(bagButton, 1);
        header.Children.Add(bagButton);

        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 14 };
        toolbar.Children.Add(countText);
        sortControl.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(sortControl, 1);
        toolbar.Children.Add(sortControl);

        var body = Ck.Columns("222,22,*");
        body.Children.Add(sidebar);
        var results = new StackPanel { Spacing = 12, Children = { toolbar, gridHost, status } };
        Grid.SetColumn(results, 2);
        body.Children.Add(results);

        return Ck.Specimen(new StackPanel { Spacing = 16, Children = { header, Ck.Divider(), body } });
    }
}
