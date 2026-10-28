using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;
using Mk = Arc.Avalonia.BlocksMarketing.MarketingKit;

namespace Arc.Avalonia.BlocksMarketing.Pages;

/// <summary>
/// Blog grid: a featured post, a category filter, post cards, pagination and an in-place reader. Posts,
/// categories and page numbers are sample data in the page; opening a post swaps the index for the reader and
/// Back returns to the same filtered index position. No routing and no network.
/// </summary>
public sealed class BlogGridPage : ArcItemPage
{
    public override string ItemSlug => "blog-grid";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A marketing blog index with one post worth leading with.",
        "Teams that publish across a few categories and want the filter to stay in place.",
        "Indexes where people expect to read the post without losing their place."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each post card is a button, so Enter or Space opens the reader and Back returns to the index.",
        "Category chips expose the selected filter, and the page number is written out beside the control.",
        "The reader keeps one heading and the post body reads in normal document order."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The reader replaces the index in place; the index keeps its scroll position when you come back.",
        "Category changes rebuild the cards without travel, and reduced motion removes the swap fade."
    };

    public override string? ImplementationNotes =>
        "Sample posts and categories live in the page. The reader is an in-page swap rather than a route, pagination is a real ArcPagination control with local pages, and thumbnails are token washes with an icon instead of photography. No external fonts or images are loaded.";

    private sealed record Post(string Category, string Title, string Excerpt, string Author, string Date, string Read, string Icon, string Body, string More);

    private static readonly Post[] Posts =
    {
        new("Product", "The case for fewer, longer bets",
            "Why the roadmap works better with three live bets than twelve hopeful ones.",
            "Maya Chen", "12 Mar", "6 min", "target",
            "Most roadmaps are a list of wishes. The teams that ship consistently keep the list short enough to argue about: three bets, one owner each, and a written reason for every one of them.",
            "The rest of the list is not lost. It waits in a queue that the team reviews on the first Monday of each month, with no obligation to promote anything."),
        new("Engineering", "Cutting release time from hours to minutes",
            "How the platform team rebuilt the deploy path without a big rewrite.",
            "Jonas Weber", "9 Mar", "8 min", "code",
            "The old release took four hours because every service waited on the slowest migration. The new path runs the migration once, then fans out, which brought a typical release down to eleven minutes.",
            "The interesting part was not the tool. It was the decision to make the release boring enough that anyone could run it on a Friday."),
        new("Design", "A quieter way to show progress",
            "Progress bars that tell the truth about time instead of decorating it.",
            "Sofia Ahlberg", "4 Mar", "5 min", "activity",
            "A progress bar is a promise about time. When it moves smoothly toward a finish that keeps receding, people stop believing it. Honest progress is coarse, jumps in steps, and never fills to 99% early.",
            "We rebuilt the meter on three states, and the support questions about waiting went down by half."),
        new("Product", "What we changed after 300 interviews",
            "Six product decisions that survived contact with real users.",
            "Maya Chen", "26 Feb", "7 min", "message-circle",
            "Interviews are not a feature request queue. The useful signal is the workaround people invent, because that is where the product stops matching the job.",
            "Three of these changes touched the same screen, which is usually a sign the screen was doing too much."),
        new("Engineering", "Typed events, fewer surprises",
            "One schema for every event the product emits.",
            "Jonas Weber", "19 Feb", "6 min", "code",
            "Events drift. A field gets a new shape, a service keeps the old one, and six months later nobody can explain a chart. A single typed schema with an owner per event fixes most of that.",
            "The migration took two weeks and has paid for itself twice over."),
        new("Design", "The weight of one accent",
            "Using colour for the one thing that matters, and nothing else.",
            "Sofia Ahlberg", "11 Feb", "4 min", "palette",
            "An accent colour is a budget. Spend it on the current item, the primary action and the one series that carries the argument, and a dense screen stays readable.",
            "The rest is neutral, which is not the same as boring.")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var indexHost = new ContentControl();
        var readerHost = new ContentControl { IsVisible = false };
        var category = "All";

        var body = new Panel { Children = { indexHost, readerHost } };

        void Open(Post post)
        {
            indexHost.IsVisible = false;
            readerHost.IsVisible = true;
            readerHost.Content = Reader(post, Back);
        }

        void Back()
        {
            readerHost.IsVisible = false;
            indexHost.IsVisible = true;
        }

        void BuildIndex()
        {
            var matches = Posts.Where(post => category == "All" || post.Category == category).ToList();
            var featured = matches[0];
            var rest = matches.Skip(1).Take(3).ToList();

            var strip = new Border
            {
                Width = 300,
                Height = 108,
                CornerRadius = new CornerRadius(16),
                ClipToBounds = true,
                Child = new Panel
                {
                    Children =
                    {
                        new Arc.Avalonia.BlocksMarketing.Controls.ArcMeshWash(),
                        new Border
                        {
                            Padding = new Thickness(16),
                            Child = Mk.Disc(featured.Icon, 36, 17, accent: true)
                        }
                    }
                }
            };

            var featuredText = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 8,
                Children =
                {
                    Mk.Row(8,
                        Mk.Pill(featured.Category, ArcBadgeTone.Neutral),
                        Mk.Caption($"{featured.Date} · {featured.Read} read")),
                    Mk.Text(featured.Title, "arc-heading", 520),
                    Mk.Body(featured.Excerpt, 520),
                    Mk.Row(8,
                        Mk.Avatar(featured.Author, ArcAvatarSize.Xs),
                        Mk.Caption(featured.Author))
                }
            };

            var featuredGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            featuredGrid.Children.Add(strip);
            var featuredCopy = WithMargin(featuredText, 18, 0, 0, 0);
            Grid.SetColumn(featuredCopy, 1);
            featuredGrid.Children.Add(featuredCopy);

            var featuredButton = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top,
                Content = featuredGrid
            };
            featuredButton.Click += (_, _) => Open(featured);
            var featuredCard = Mk.Panel(new Border { Child = featuredButton }, padding: 14);

            var cards = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
            for (var index = 0; index < rest.Count; index++)
            {
                var post = rest[index];
                var card = Card(post, () => Open(post));
                if (index > 0)
                {
                    card.Margin = new Thickness(12, 0, 0, 0);
                }

                Grid.SetColumn(card, index);
                cards.Children.Add(card);
            }
            var pages = new ArcPagination { Page = 1, PageCount = 3, Label = "Blog pages" };
            var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            footer.Children.Add(Mk.Caption($"{matches.Count} posts in {category}"));
            pages.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(pages, 1);
            footer.Children.Add(pages);

            indexHost.Content = Mk.Stack(12, featuredCard, cards, footer);
        }

        BuildIndex();

        var chips = Mk.Chips(
            new[] { ("All", "All"), ("Product", "Product"), ("Engineering", "Engineering"), ("Design", "Design") },
            "All",
            value =>
            {
                category = value;
                BuildIndex();
            });

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        header.Children.Add(Mk.Stack(2,
            Mk.Heading("Writing from the team", 420),
            Mk.Caption("Notes on building calm software.")));

        return Mk.Surface(new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 12,
            Margin = new Thickness(24, 20, 24, 18),
            Children = { header, chips, body }
        });
    }

    private static Control Card(Post post, Action open)
    {
        var wash = new Border
        {
            Height = 56,
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Child = new Panel
            {
                Children =
                {
                    new Arc.Avalonia.BlocksMarketing.Controls.ArcMeshWash { Intensity = 0.7 },
                    new Border { Padding = new Thickness(12), Child = Mk.Disc(post.Icon, 28, 13) }
                }
            }
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Children =
            {
                wash,
                Mk.Row(8, Mk.Caption(post.Category), Mk.Caption($"· {post.Read}")),
                Mk.Text(post.Title, "arc-body arc-medium", 260),
                Mk.Caption($"{post.Author} · {post.Date}")
            }
        };

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
            Content = content
        };
        button.Click += (_, _) => open();
        return Mk.Panel(new Border { Child = button }, padding: 12);
    }

    private static Control Reader(Post post, Action back)
    {
        return Mk.Stack(12,
            Mk.Btn("All posts", ArcButtonVariant.Ghost, ArcButtonSize.Sm, back, icon: "arrow-left"),
            Mk.Row(8,
                Mk.Pill(post.Category, ArcBadgeTone.Neutral),
                Mk.Caption($"{post.Date} · {post.Read} read")),
            Mk.Text(post.Title, "arc-subtitle", 640),
            Mk.Row(8,
                Mk.Avatar(post.Author, ArcAvatarSize.Xs),
                Mk.Caption($"By {post.Author}")),
            Mk.Divider(),
            Mk.Text(post.Body, "arc-body-lg", 640),
            Mk.Text(post.More, "arc-body", 640));
    }

    private static Control WithMargin(Control control, double left, double top, double right, double bottom)
    {
        control.Margin = new Thickness(left, top, right, bottom);
        return control;
    }
}



