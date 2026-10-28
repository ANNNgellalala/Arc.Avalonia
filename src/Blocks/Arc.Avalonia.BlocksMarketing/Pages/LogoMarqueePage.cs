using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;
using Arc.Avalonia.BlocksMarketing.Controls;
using Mk = Arc.Avalonia.BlocksMarketing.MarketingKit;

namespace Arc.Avalonia.BlocksMarketing.Pages;

/// <summary>
/// Logo marquee: a quiet, continuously moving row of brand marks with a pause control. Two copies of the row
/// travel on a render transform so the seam never shows; the loop stops under reduced motion, where the pause
/// button reports the still frame instead of pretending to move. Marks are sample wordmarks, not endorsements.
/// </summary>
public sealed class LogoMarqueePage : ArcItemPage
{
    public override string ItemSlug => "logo-marquee";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A social proof band under the hero, when the names matter more than the logos.",
        "Pages that want quiet motion without asking for attention.",
        "Closing sections that repeat the customer list."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The pause control is a real button with a label that describes the next action, and it is disabled when motion is off.",
        "The row is decorative; the same names are also written in the surrounding copy, so nothing is only available by watching it move.",
        "The row never steals focus while it moves."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The row loops on a constant linear speed with no easing at the seam.",
        "Pausing stops it where it is; reduced motion holds the first frame and disables the control."
    };

    public override string? ImplementationNotes =>
        "The marquee is a local control: two identical rows translated by one timer, with the seam wrapped by the measured copy width. The web original loops with CSS animation; the desktop port drives it from the same render transform so nothing re-lays-out. Brand marks are sample wordmarks with a Lucide glyph instead of customer logos.";

    private static readonly (string Name, string Icon)[] Brands =
    {
        ("Northwind", "cloud"),
        ("Cobalt", "box"),
        ("Fieldkit", "briefcase"),
        ("Meridian", "compass"),
        ("Halcyon", "wind"),
        ("Parcel", "package"),
        ("Quanta", "cpu"),
        ("Orbit", "globe")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var marquee = new ArcMarquee(Brands.Length, index => Mark(Brands[index].Name, Brands[index].Icon),
            speed: 22, gap: 46, height: 56);

        var pause = Mk.Btn("Pause", ArcButtonVariant.Secondary, ArcButtonSize.Sm, icon: "pause");
        var state = Mk.Caption("Moving");
        pause.IsEnabled = !marquee.IsStill;

        pause.Click += (_, _) =>
        {
            marquee.TogglePause();
            var moving = marquee.IsMoving;
            pause.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                Children =
                {
                    new ArcIcon { Icon = moving ? "pause" : "play", Size = 14 },
                    new TextBlock { Text = moving ? "Pause" : "Play" }
                }
            };
            state.Text = moving ? "Moving" : "Paused";
        };

        if (marquee.IsStill)
        {
            pause.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                Children =
                {
                    new ArcIcon { Icon = "play", Size = 14 },
                    new TextBlock { Text = "Play" }
                }
            };
            state.Text = "Holds still under reduced motion";
        }

        var band = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
            Children =
            {
                Mk.Divider(),
                new Border { Padding = new Thickness(0, 14, 0, 14), Child = marquee },
                Mk.Divider()
            }
        };

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto") };
        footer.Children.Add(Mk.Caption("Sample customer marks"));
        var middle = Mk.Caption("4,000 teams · 38 countries");
        Grid.SetColumn(middle, 2);
        middle.VerticalAlignment = VerticalAlignment.Center;
        middle.Margin = new Thickness(0, 0, 12, 0);
        footer.Children.Add(middle);
        Grid.SetColumn(pause, 3);
        footer.Children.Add(pause);

        var stateRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { state }
        };

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(Mk.Stack(3,
            Mk.Caption("Trusted by product teams at"),
            Mk.Text("Quiet software for teams that ship", "arc-heading", 460)));
        Grid.SetColumn(stateRow, 1);
        head.Children.Add(stateRow);

        return Mk.Surface(new Border
        {
            Padding = new Thickness(26, 22, 26, 20),
            Child = Mk.Stack(14,
                head,
                band,
                footer)
        });
    }

    private static Control Mark(string name, string icon)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Opacity = 0.62,
            Children =
            {
                new ArcIcon { Icon = icon, Size = 18, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock
                {
                    Text = name,
                    FontSize = 16,
                    FontWeight = FontWeight.Medium,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };
    }
}

