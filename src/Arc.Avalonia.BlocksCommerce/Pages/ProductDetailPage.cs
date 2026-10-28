using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Ck = Arc.Avalonia.BlocksCommerce.CommerceKit;

namespace Arc.Avalonia.BlocksCommerce.Pages;

/// <summary>
/// Product detail: a gallery with thumbnails, finish and size variants with per-finish stock, a quantity
/// stepper, add to bag that confirms in place, shipping promises, a reviews summary and a details accordion.
/// Stock, prices and reviews are sample data; the add to bag request is a short local timer, so the button
/// still shows the real pending and confirmed states.
/// </summary>
public sealed class ProductDetailPage : ArcItemPage
{
    public override string ItemSlug => "product-detail";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A product page with a few variants, a gallery and a reviews summary.",
        "Stores that want stock-aware variants with a restock alert instead of a dead buy button."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Browsing many products; use product-listing."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Gallery arrows and thumbnails are labelled buttons; the counter is announced.",
        "Finish and size are single-select groups; a sold-out size says so in text, and add to bag announces its result."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Changing finish crossfades the photo in place and keeps the current index.",
        "Add to bag morphs through Adding to Added to bag without changing the button width."
    };

    public override string? ImplementationNotes =>
        "Sample product, finishes, sizes, shipping and reviews live in the page. The gallery is a composed photo frame with previous, next and thumbnail buttons rather than the reference's dragged track with momentum. Photos are token tiles with the product glyph, and add to bag is a local timer.";

    private sealed record Finish(string Id, string Name, string Token, double Alpha, string[] Icons);

    private sealed record SizeOption(string Id, string Name, double Price, string[] SoldOutIn);

    private static readonly Finish[] Finishes =
    {
        new("brass", "Brushed brass", ArcTokens.Accent, 1, new[] { "circle-dot", "zap", "sun", "layers" }),
        new("ink", "Ink", ArcTokens.Foreground, 1, new[] { "circle-dot", "moon", "layers", "circle" }),
        new("bone", "Bone", ArcTokens.Foreground, 0.2, new[] { "circle-dot", "circle", "box", "sun" })
    };

    private static readonly SizeOption[] Sizes =
    {
        new("small", "Small", 189, Array.Empty<string>()),
        new("medium", "Medium", 219, Array.Empty<string>()),
        new("large", "Large", 249, new[] { "ink" })
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var finish = Finishes[0];
        var size = Sizes[0];
        var quantity = 1;
        var index = 0;
        var saved = false;
        var status = Ck.Status();

        var photoHost = new ContentControl();
        var counter = Ck.Caption("1 / 4");
        var thumbs = new List<Border>();
        var thumbRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        void BuildGallery()
        {
            var photo = Ck.Photo(finish.Icons[index], 186, 378, 38);
            photoHost.Content = new Border
            {
                Child = photo,
                CornerRadius = new CornerRadius(14),
                ClipToBounds = true
            };
            counter.Text = $"{index + 1} / 4";

            thumbRow.Children.Clear();
            thumbs.Clear();
            for (var slot = 0; slot < 4; slot++)
            {
                var target = slot;
                var thumb = new Border
                {
                    Width = 58,
                    Height = 40,
                    CornerRadius = new CornerRadius(10),
                    ClipToBounds = true,
                    BorderBrush = slot == index ? Ck.Brush(ArcTokens.Accent) : Ck.Brush(ArcTokens.Border),
                    BorderThickness = new Thickness(slot == index ? 2 : 1),
                    Child = Ck.Photo(finish.Icons[slot], 36, 52, 14)
                };
                AutomationProperties.SetName(thumb, $"{finish.Name} photo {slot + 1}");
                thumb.PointerPressed += (_, _) =>
                {
                    index = target;
                    BuildGallery();
                };
                thumbs.Add(thumb);
                thumbRow.Children.Add(thumb);
            }
        }

        void Step(int delta)
        {
            index = (index + delta + 4) % 4;
            BuildGallery();
        }

        BuildGallery();

        var prev = Ck.IconBtn("chevron-left", "Previous photo", () => Step(-1), 30);
        var next = Ck.IconBtn("chevron-right", "Next photo", () => Step(1), 30);
        var gallery = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                photoHost,
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children = { thumbRow, Place(Ck.Row(6, counter, prev, next), 1) }
                }
            }
        };

        var finishName = Ck.Caption(finish.Name);
        var swatchHost = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var sizeHost = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var priceText = Ck.Text(Ck.Money(size.Price), "arc-heading");
        var stockNote = Ck.Caption(string.Empty);
        var notifyHolder = new ContentControl();

        void BuildSizes()
        {
            sizeHost.Children.Clear();
            foreach (var option in Sizes)
            {
                var soldOut = option.SoldOutIn.Contains(finish.Id);
                var selected = option.Id == size.Id;
                var button = new ArcButton
                {
                    Variant = selected ? ArcButtonVariant.Secondary : ArcButtonVariant.Ghost,
                    Size = ArcButtonSize.Sm,
                    Height = 48,
                    MinHeight = 48,
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(12, 0, 12, 0)
                };
                if (selected)
                {
                    button.BorderBrush = Ck.Brush(ArcTokens.Accent);
                    button.BorderThickness = new Thickness(1.5);
                    button.Background = Ck.Brush(ArcTokens.ControlOnSubtle);
                }
                else
                {
                    button.BorderBrush = Ck.Brush(ArcTokens.BorderSubtle);
                    button.BorderThickness = new Thickness(1);
                }

                button.Content = new StackPanel
                {
                    Spacing = 0,
                    Children =
                    {
                        Ck.Text(option.Name + (soldOut ? " · sold out" : string.Empty), "arc-caption"),
                        Ck.Text(Ck.Money(option.Price), "arc-body-sm arc-medium")
                    }
                };

                var choice = option;
                button.Click += (_, _) =>
                {
                    size = choice;
                    priceText.Text = Ck.Money(choice.Price);
                    BuildSizes();
                    BuildStockNote();
                };
                sizeHost.Children.Add(button);
            }
        }

        void BuildStockNote()
        {
            var soldOut = size.SoldOutIn.Contains(finish.Id);
            stockNote.Text = soldOut
                ? $"{size.Name} is sold out in {finish.Name.ToLowerInvariant()}."
                : $"In stock in {finish.Name.ToLowerInvariant()}, ships in 2 days.";
            stockNote.Foreground = soldOut ? Ck.Brush(ArcTokens.TextSecondary) : Ck.Brush(ArcTokens.Success);
            notifyHolder.Content = soldOut
                ? Ck.Link("Notify me when it is back",
                    () => Ck.Say(status, $"We will email you when {size.Name} is back in {finish.Name}."), "bell")
                : null;
        }

        void BuildFinishes()
        {
            swatchHost.Children.Clear();
            foreach (var option in Finishes)
            {
                var selected = option.Id == finish.Id;
                swatchHost.Children.Add(Ck.Swatch(Ck.Tint(option.Token, option.Alpha), selected, () =>
                {
                    finish = option;
                    finishName.Text = option.Name;
                    BuildFinishes();
                    BuildGallery();
                    BuildStockNote();
                }, option.Name));
            }
        }

        BuildFinishes();
        BuildSizes();
        BuildStockNote();

        var addLabel = new TextBlock
        {
            Text = "Add to bag",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Ck.Brush(ArcTokens.AccentForeground)
        };
        var addIcon = new ArcIcon
        {
            Icon = "plus",
            Size = 15,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Ck.Brush(ArcTokens.AccentForeground)
        };
        var addButton = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Md,
            Height = 44,
            MinHeight = 44,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { addIcon, addLabel }
            }
        };
        addButton.Click += (_, _) =>
        {
            if (size.SoldOutIn.Contains(finish.Id))
            {
                Ck.Say(status, $"{size.Name} in {finish.Name} is sold out. Use Notify me to get an email.");
                return;
            }

            addButton.IsEnabled = false;
            addButton.Loading = true;
            addLabel.Text = "Adding";

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                addButton.Loading = false;
                addButton.IsEnabled = true;
                addIcon.Icon = "check";
                addLabel.Text = "Added to bag";
                Ck.Say(status, $"{quantity} × Halo table lamp, {finish.Name}, size {size.Name}, added to the bag.");
            };
            timer.Start();
        };

        var reviewBars = new StackPanel { Spacing = 4 };
        var distribution = new[] { 92, 24, 7, 2 };
        for (var slot = 0; slot < distribution.Length; slot++)
        {
            var stars = 5 - slot;
            var bar = Ck.Progress(distribution[slot], 126, ArcProgressTone.Accent);
            bar.Width = 210;
            reviewBars.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"),
                ColumnSpacing = 6,
                Children =
                {
                    Ck.Caption($"{stars}★"),
                    Place(bar, 1),
                    Place(Ck.Caption(distribution[slot].ToString()), 2)
                }
            });
        }

        var starRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1 };
        for (var slot = 0; slot < 5; slot++)
        {
            starRow.Children.Add(new ArcIcon { Icon = "star", Size = 13, Foreground = Ck.Brush(ArcTokens.Accent) });
        }

        var reviews = Ck.Card(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Ck.Row(10,
                    Ck.Text("4.8", "arc-heading"),
                    starRow,
                    Ck.Caption("126 reviews")),
                reviewBars
            }
        }, 12);

        var accordion = new ArcAccordion
        {
            DefaultOpen = -1,
            Label = "Product details",
            Items = new List<ArcAccordionItem>
            {
                new()
                {
                    Title = "Materials and dimensions",
                    Content = Ck.Body("Brushed metal stem, hand-blown glass shade, solid oak base. 46 cm tall, 28 cm wide, 2.1 kg.")
                },
                new()
                {
                    Title = "Care",
                    Content = Ck.Body("Wipe the base with a dry cloth and dust the shade with a soft brush. No solvents.")
                },
                new()
                {
                    Title = "Shipping and returns",
                    Content = Ck.Body("Free delivery over $75, 30 day returns and a two year warranty on the fitting.")
                }
            }
        };

        var leftColumn = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                gallery,
                Ck.Divider(),
                reviews,
                Ck.RowWrap(16,
                    Ck.Meta("package", "Free delivery over $75"),
                    Ck.Meta("refresh-cw", "30 day returns"),
                    Ck.Link("Read all 126 reviews",
                        () => Ck.Say(status, "The reference opens the full review list; this specimen keeps the summary.")))
            }
        };

        var info = new StackPanel
        {
            Spacing = 5,
            Children =
            {
                Ck.Heading("Halo table lamp", 420, "arc-subtitle"),
                Ck.Row(10,
                    priceText,
                    Strike(Ck.Money(220)),
                    Ck.Meta("star", "4.8 (126)")),
                Ck.Body("A dimmable lamp with a hand-blown shade and a brushed stem. Warm light, no glare, and a small footprint.", 450),
                Ck.Row(8, Ck.Label("Finish"), finishName),
                swatchHost,
                Ck.Divider(),
                Ck.Label("Size"),
                sizeHost,
                stockNote,
                notifyHolder,
                Ck.Row(10,
                    Ck.Qty(quantity, value => quantity = value, min: 1, max: 9),
                    addButton),
                Ck.Divider(),
                accordion,
                status
            }
        };

        var wishlist = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Compact = true,
            Content = new ArcIcon { Icon = "heart", Size = 17 }
        };
        AutomationProperties.SetName(wishlist, "Save to wishlist");
        wishlist.Click += (_, _) =>
        {
            saved = !saved;
            wishlist.Foreground = saved ? Ck.Brush(ArcTokens.Accent) : Ck.Brush(ArcTokens.TextSecondary);
            Ck.Say(status, saved ? "Saved to your wishlist." : "Removed from your wishlist.");
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(Ck.Row(8,
            Ck.Link("Lighting", () => Ck.Say(status, "Breadcrumbs route in a real store; this specimen stays on the product.")),
            Ck.Caption("/"),
            Ck.Caption("Table lamps")));
        var headerRight = Ck.Row(10, Ck.Pill("In stock", ArcBadgeTone.Success, true), wishlist);
        headerRight.HorizontalAlignment = HorizontalAlignment.Right;
        header.Children.Add(Place(headerRight, 1));

        var main = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("398,28,*"),
            Children = { leftColumn, Place(info, 2) }
        };

        return Ck.Specimen(new StackPanel
        {
            Spacing = 12,
            Children = { header, Ck.Divider(), main }
        }, 16);
    }

    private static TextBlock Strike(string text)
    {
        var block = Ck.Text(text, "arc-caption");
        block.TextDecorations = TextDecorations.Strikethrough;
        block.VerticalAlignment = VerticalAlignment.Center;
        return block;
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}