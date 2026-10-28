using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Mk = Arc.Avalonia.Blocks.Marketing.MarketingKit;

namespace Arc.Avalonia.Blocks.Marketing.Pages;

/// <summary>
/// Site footer: link columns beside a newsletter, a one-row minimal layout, and a closing variant with a
/// large fading mark. The newsletter validates in place and confirms without a toast while the form keeps its
/// width. Approximations: the fading mark is a clipped type block at low opacity rather than a mask image, and
/// the variant switcher stands in for the reference's container-query reflow.
/// </summary>
public sealed class SiteFooterPage : ArcItemPage
{
    public override string ItemSlug => "site-footer";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The closing section of a marketing, docs, or product site.",
        "Footers that need a newsletter signup and a status link.",
        "Minimal one-row footers for app shells and docs."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The footer is a landmark and every link group carries its own label.",
        "The newsletter field is labelled and its validation message is tied to the field.",
        "The status tone is paired with a written label, never colour alone."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The subscribe action morphs into its confirmed state without the form changing width.",
        "Reduced motion swaps states without travel."
    };

    public override string? ImplementationNotes =>
        "Sample links, columns and the newsletter are declared in the page. The subscribe handler is local and simulated: it validates the address, then morphs the row into a confirmation. The large mark uses a low-opacity display text clipped by the footer surface, which is the port's stand-in for the reference's masked logo image.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var newsletterHost = new ContentControl();
        var email = Mk.Input("you@company.com", width: 226, leadingIcon: "mail");

        var formHost = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };
        var subscribeButton = Mk.Btn("Subscribe");
        formHost.Children.Add(Mk.Row(8, email, subscribeButton));
        formHost.Children.Add(Mk.Caption("One email a month. Unsubscribe anytime."));

        var confirmedAddress = Mk.Caption(string.Empty);
        var useAnother = Mk.Btn("Use another address", ArcButtonVariant.Ghost, ArcButtonSize.Sm);
        var confirmedHost = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            IsVisible = false,
            Children =
            {
                new Border
                {
                    Background = Mk.Tint(ArcTokens.Success, 0.10),
                    BorderBrush = Mk.Tint(ArcTokens.Success, 0.28),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(14),
                    Child = Mk.Row(10,
                        new ArcIcon { Icon = "circle-check", Size = 16, Foreground = Mk.Brush(ArcTokens.Success) },
                        Mk.Stack(2,
                            Mk.Text("You're on the list", "arc-body-sm arc-medium"),
                            confirmedAddress))
                },
                useAnother
            }
        };

        newsletterHost.Content = Mk.Stack(12, formHost, confirmedHost);

        subscribeButton.Click += (_, _) => Subscribe();
        useAnother.Click += (_, _) =>
        {
            confirmedHost.IsVisible = false;
            formHost.IsVisible = true;
            email.Focus();
        };

        void Subscribe()
        {
            var address = (email.Value ?? string.Empty).Trim();
            if (address.Length < 3 || !address.Contains('@') || !address.Contains('.'))
            {
                email.Error = "Enter a valid email address.";
                return;
            }

            email.Error = null;
            confirmedAddress.Text = address;
            formHost.IsVisible = false;
            confirmedHost.IsVisible = true;
        }

        var brandBlock = Mk.Stack(8,
            Mk.Logo("Acme", "layers"),
            Mk.Body("Scheduling for small clinics, without the noise.", 250));

        var newsletterBlock = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { Mk.Label("Get the changelog"), newsletterHost }
        };

        var topRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        topRow.Children.Add(brandBlock);
        Grid.SetColumn(newsletterBlock, 1);
        topRow.Children.Add(newsletterBlock);

        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), IsVisible = true };
        var groups = new[]
        {
            ("Product", new[] { "Overview", "Pricing", "Changelog", "Roadmap" }),
            ("Company", new[] { "About", "Careers", "Press" }),
            ("Resources", new[] { "Docs", "Guides", "Community" }),
            ("Legal", new[] { "Privacy", "Terms", "Security" })
        };
        for (var index = 0; index < groups.Length; index++)
        {
            var column = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };
            column.Children.Add(Mk.Caption(groups[index].Item1));
            foreach (var link in groups[index].Item2)
            {
                column.Children.Add(Mk.Link(link));
            }

            Grid.SetColumn(column, index);
            columns.Children.Add(column);
        }

        var status = Mk.Row(6,
            new Border
            {
                Width = 7,
                Height = 7,
                CornerRadius = new CornerRadius(4),
                Background = Mk.Brush(ArcTokens.Success),
                VerticalAlignment = VerticalAlignment.Center
            },
            Mk.Caption("All systems normal"));

        var socials = Mk.Row(2,
            Mk.IconBtn("globe", "Website"),
            Mk.IconBtn("at-sign", "Social"),
            Mk.IconBtn("message-circle", "Community"));

        var bottom = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        bottom.Children.Add(Mk.Row(14,
            Mk.Caption("© 2026 Acme Labs"),
            status,
            Mk.Link("Privacy"),
            Mk.Link("Terms")));
        Grid.SetColumn(socials, 2);
        bottom.Children.Add(socials);

        // The closing variant's large mark fades toward the bottom edge: a clipped display face at low
        // opacity, which is the port's stand-in for the reference's masked logo image.
        var mark = new TextBlock
        {
            Text = "acme",
            Classes = { "arc-display" },
            FontSize = 72,
            Opacity = 0.06,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, -24)
        };

        var markBand = new Border { Height = 44, Child = mark, IsHitTestVisible = false };

        var full = new StackPanel { Orientation = Orientation.Vertical, Spacing = 16 };
        full.Children.Add(topRow);
        full.Children.Add(Mk.Divider());
        full.Children.Add(columns);
        full.Children.Add(Mk.Divider());
        full.Children.Add(bottom);
        full.Children.Add(markBand);

        var fullHost = new Panel();
        fullHost.Children.Add(new Border { Padding = new Thickness(28, 26, 28, 10), Child = full });

        var minimalSocials = Mk.Row(2,
            Mk.IconBtn("globe", "Website"),
            Mk.IconBtn("at-sign", "Social"),
            Mk.IconBtn("message-circle", "Community"));

        var minimalRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        minimalRow.Children.Add(Mk.Row(16,
            Mk.Logo("Acme", "layers", 22, 14),
            Mk.Caption("© 2026 Acme Labs"),
            Mk.Caption("Privacy"),
            Mk.Caption("Terms")));
        Grid.SetColumn(minimalSocials, 1);
        minimalRow.Children.Add(minimalSocials);

        var minimal = new Border
        {
            Padding = new Thickness(28, 16, 28, 16),
            IsVisible = false,
            Child = minimalRow
        };

        var body = new Panel();
        body.Children.Add(fullHost);
        body.Children.Add(minimal);

        var minimalSwitch = new ArcSwitch { Label = "Minimal layout" };
        minimalSwitch.CheckedChanged += (_, _) =>
        {
            fullHost.IsVisible = !minimalSwitch.Checked;
            minimal.IsVisible = minimalSwitch.Checked;
        };

        var columnSwitch = new ArcSwitch { Label = "Link columns", Checked = true };
        columnSwitch.CheckedChanged += (_, _) => columns.IsVisible = columnSwitch.Checked;

        return Mk.Stack(14,
            Mk.Row(20, minimalSwitch, columnSwitch),
            Mk.Frame("acme.com", body));
    }
}
