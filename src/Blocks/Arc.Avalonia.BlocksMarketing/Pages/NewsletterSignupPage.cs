using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Animation;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Mk = Arc.Avalonia.BlocksMarketing.MarketingKit;

namespace Arc.Avalonia.BlocksMarketing.Pages;

/// <summary>
/// Newsletter signup: the form is framed by a stack of past issues, and subscribing drops the next issue,
/// addressed to the new reader, onto the front of the stack. The send is simulated; the new issue card is
/// created locally from the address and slides in unless reduced motion asks it to appear.
/// </summary>
public sealed class NewsletterSignupPage : ArcItemPage
{
    public override string ItemSlug => "newsletter-signup";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A blog or changelog that ships a regular issue.",
        "Closing sections where the archive is the proof of value.",
        "Launch pages that want a low-commitment action beside the main one."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The email field is labelled and its error is tied to the field, not shown as a toast.",
        "The subscribe action confirms in place and the confirmation is announced politely.",
        "The issue list is ordinary content, so the newest issue is readable without interacting."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The new issue drops onto the front of the stack while the older cards move down one slot.",
        "Reduced motion inserts the card without travel."
    };

    public override string? ImplementationNotes =>
        "Subscribing is simulated: the address is validated locally, then a new issue card is built with the address on it. Past issues are sample data. The stack is a vertical list rather than the reference's overlapping card stack, which keeps the text readable at gallery width.";

    private sealed record Issue(string Number, string Title, string Meta);

    private static readonly Issue[] Issues =
    {
        new("11", "The cost of a rewrite", "6 min · Engineering"),
        new("10", "Small teams, long horizons", "5 min · Product"),
        new("9", "What we learned from 300 interviews", "8 min · Research")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 };

        foreach (var issue in Issues)
        {
            stack.Children.Add(IssueCard(issue.Number, issue.Title, issue.Meta, false, null));
        }

        var status = Mk.Caption("Join 12,400 readers. One issue every Thursday.");
        var email = Mk.Input("you@company.com", width: 240, leadingIcon: "mail");
        var subscribe = Mk.Btn("Subscribe");
        var form = Mk.Row(8, email, subscribe);

        subscribe.Click += (_, _) =>
        {
            var address = (email.Value ?? string.Empty).Trim();
            if (address.Length < 3 || !address.Contains('@') || !address.Contains('.'))
            {
                email.Error = "Enter a valid email address.";
                return;
            }

            email.Error = null;
            var newest = IssueCard("12", "The next issue, written for you", "Arrives Thursday · 6 min", true, address);
            stack.Children.Insert(0, newest);
            status.Text = $"Issue 12 will land in {address} on Thursday.";
            form.IsVisible = false;

            if (!ArcThemeManager.ReducedMotion)
            {
                newest.Opacity = 0;
                newest.RenderTransform = new TranslateTransform(0, -10);
                newest.Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = Visual.OpacityProperty,
                        Duration = Arc.Avalonia.Motion.ArcMotion.Standard,
                        Easing = Arc.Avalonia.Motion.ArcMotion.EaseEnter
                    },
                    new TransformOperationsTransition
                    {
                        Property = Visual.RenderTransformProperty,
                        Duration = Arc.Avalonia.Motion.ArcMotion.SpringSmoothDuration,
                        Easing = Arc.Avalonia.Motion.ArcMotion.SpringSmooth
                    }
                };

                Dispatcher.UIThread.Post(() =>
                {
                    newest.Opacity = 1;
                    newest.RenderTransform = new TranslateTransform(0, 0);
                }, DispatcherPriority.Render);
            }
        };

        var left = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 14,
            Children =
            {
                Mk.Stack(4,
                    Mk.Heading("The weekly build log", 380),
                    Mk.Body("One considered issue about planning, shipping and the tools in between. Written by the team, never syndicated.", 380)),
                form,
                status,
                ArchiveLink()
            }
        };

        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("1*,1*") };
        root.Children.Add(left);
        var stackHost = new Border
        {
            Child = stack,
            Margin = new Thickness(28, 0, 0, 0)
        };
        Grid.SetColumn(stackHost, 1);
        root.Children.Add(stackHost);

        return Mk.Surface(new Border { Padding = new Thickness(26, 26, 26, 24), Child = root });
    }

    private static Control ArchiveLink()
    {
        var link = Mk.Btn("Read the archive", ArcButtonVariant.Ghost, ArcButtonSize.Sm, trailingIcon: "arrow-right");
        link.HorizontalAlignment = HorizontalAlignment.Left;
        return link;
    }

    private static Control IssueCard(string number, string title, string meta, bool fresh, string? address)
    {
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        top.Children.Add(Mk.Text($"Issue {number}", "arc-caption arc-medium"));
        var badge = fresh ? Mk.Pill("For you", ArcBadgeTone.Accent) : null;
        if (badge is not null)
        {
            Grid.SetColumn(badge, 2);
            top.Children.Add(badge);
        }

        var content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            Children =
            {
                top,
                Mk.Text(title, "arc-body-sm arc-medium", 320)
            }
        };

        if (!string.IsNullOrWhiteSpace(address))
        {
            content.Children.Add(Mk.Caption($"Sent to {address}"));
        }
        else
        {
            content.Children.Add(Mk.Caption(meta));
        }

        return new Border
        {
            Background = fresh ? Mk.Tint(ArcTokens.Accent, 0.07) : Mk.Brush(ArcTokens.SurfaceMuted),
            BorderBrush = fresh ? Mk.Tint(ArcTokens.Accent, 0.3) : Mk.Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14, 12, 14, 12),
            Child = content
        };
    }
}



