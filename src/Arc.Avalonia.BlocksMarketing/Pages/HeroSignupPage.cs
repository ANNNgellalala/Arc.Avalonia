using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Mk = Arc.Avalonia.BlocksMarketing.MarketingKit;

namespace Arc.Avalonia.BlocksMarketing.Pages;

/// <summary>
/// Hero signup: a waitlist hero on a soft drifting wash. The email field validates as you type, the button
/// only enables on a valid address, and submitting grows the form into a confirmation with your place in line.
/// The queue position is sample data; nothing is sent anywhere.
/// </summary>
public sealed class HeroSignupPage : ArcItemPage
{
    public override string ItemSlug => "hero-signup";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A launch or waitlist page where the email is the only action.",
        "Products that are not generally available yet and want a real queue.",
        "Campaign pages that need one field and a reason to trust it."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The email field is labelled and Enter submits from the field.",
        "The validation message is announced in place and tied to the field.",
        "The confirmation is a polite status update, and the place in line is written as text."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The submit button grows into the confirmation while the form column keeps its width.",
        "The wash drifts on a slow loop; reduced motion holds a still frame and swaps the states without travel."
    };

    public override string? ImplementationNotes =>
        "Validation is local (an at sign and a dot), the confirmation swaps in place, and the queue position is a fixed sample number so the page renders the same frame every time. The wash is the port's gradient-brush approximation of the reference's mesh, and the drifting loop is disabled under reduced motion.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var email = Mk.Input("you@company.com", width: 300, leadingIcon: "mail");
        var join = Mk.Btn("Join the waitlist", ArcButtonVariant.Primary, ArcButtonSize.Lg);

        var form = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                Mk.Row(8, email, join),
                Mk.Caption("No spam. One email when your invite is ready.")
            }
        };

        var confirmation = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                Mk.Row(8,
                    Mk.Disc("circle-check", 34, 16, accent: true),
                    Mk.Text("You're in. You're #1,284 in line.", "arc-body-lg arc-medium")),
                Mk.Caption("We'll email you the moment an invite opens."),
                Mk.Row(8,
                    Mk.AvatarGroup(new[] { "Maya Chen", "Jonas Weber", "Sofia Ahlberg", "Ravi Patel" }),
                    Mk.Caption("1,283 people ahead of you"))
            }
        };

        email.ValueChanged += (_, _) =>
        {
            var address = (email.Value ?? string.Empty).Trim();
            var valid = address.Length > 3 && address.Contains('@') && address.Contains('.');
            join.IsEnabled = valid || address.Length == 0;
            email.Error = address.Length > 3 && !valid ? "Enter a full email address, including the domain." : null;
        };

        join.Click += (_, _) =>
        {
            var address = (email.Value ?? string.Empty).Trim();
            if (address.Length < 4 || !address.Contains('@') || !address.Contains('.'))
            {
                email.Error = "Enter a full email address, including the domain.";
                return;
            }

            form.IsVisible = false;
            confirmation.IsVisible = true;
        };

        var title = Mk.Text("Be first when the doors open.", "arc-title-sm", 560);
        title.TextAlignment = TextAlignment.Center;
        title.HorizontalAlignment = HorizontalAlignment.Center;

        var body = Mk.Body("Arc opens in small groups through the spring. Join the waitlist and we will send one email when your group is up.", 460);
        body.TextAlignment = TextAlignment.Center;
        body.HorizontalAlignment = HorizontalAlignment.Center;

        var formHost = new Panel { Children = { form, confirmation } };

        var content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 18,
            Margin = new Thickness(32, 34, 32, 0),
            Children =
            {
                Mk.Pill("Waitlist open", ArcBadgeTone.Accent, dot: true),
                title,
                body,
                formHost
            }
        };

        content.Children[0].HorizontalAlignment = HorizontalAlignment.Center;

        var stage = new Grid { Height = 330 };
        stage.Children.Add(content);

        return Mk.Wash(stage, radius: 26, drift: true, intensity: 0.85);
    }
}


