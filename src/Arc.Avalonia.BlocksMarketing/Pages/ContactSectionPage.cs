using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Mk = Arc.Avalonia.BlocksMarketing.MarketingKit;

namespace Arc.Avalonia.BlocksMarketing.Pages;

/// <summary>
/// Contact section: a validated form that morphs into a confirmation, support channels beside it, and office
/// cards with local times. Validation is local and the send is simulated; office times are fixed sample
/// strings so the specimen renders the same frame every time.
/// </summary>
public sealed class ContactSectionPage : ArcItemPage
{
    public override string ItemSlug => "contact-section";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A sales or support page where the form is the main action.",
        "Teams that want to show real channels next to the form, not after it.",
        "Companies with more than one office that want local times on the page."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Every field is labelled, and a failed submit puts the message next to the field that needs it.",
        "The confirmation replaces the form in place and focus moves into it.",
        "Office times are written out beside the city, never encoded in colour alone."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The form morphs into its confirmation while the column keeps its width.",
        "Reduced motion swaps the two states without travel."
    };

    public override string? ImplementationNotes =>
        "Validation and the send are simulated in the page: the address has to look like an email and the message needs a sentence, then the form is replaced by a confirmation. Office clocks are fixed sample times rather than a live timezone calculation, so screenshots stay deterministic. The copy action uses the shared clipboard control.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var name = new ArcInput { Placeholder = "Maya Chen", Label = "Name" };
        var email = new ArcInput { Placeholder = "maya@company.com", Label = "Work email", LeadingIcon = "mail" };
        var message = new ArcTextarea { Placeholder = "What are you trying to solve?", Label = "Message", Rows = 3 };

        var form = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };
        form.Children.Add(Mk.Stack(4,
            Mk.Heading("Talk to the team", 340),
            Mk.Body("Tell us what you are working on and we will reply within one business day.", 340)));

        var fieldRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        fieldRow.Children.Add(name);
        email.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(email, 1);
        fieldRow.Children.Add(email);
        form.Children.Add(fieldRow);
        form.Children.Add(message);

        var send = Mk.Btn("Send message");
        form.Children.Add(Mk.Row(12, send, Mk.Caption("One reply from a person, no sequence.")));

        var resend = Mk.Btn("Send another message", ArcButtonVariant.Secondary);

        var confirmation = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 12,
            IsVisible = false,
            Children =
            {
                Mk.Disc("circle-check", 44, 20, accent: true),
                Mk.Heading("Message sent", 320),
                Mk.Body("Thanks. A person from the team will reply to that address within one business day.", 360),
                resend
            }
        };

        var leftHost = new Panel { Children = { form, confirmation } };

        resend.Click += (_, _) => Reset();
        send.Click += (_, _) => Submit();

        void Submit()
        {
            var ok = true;
            if (string.IsNullOrWhiteSpace(name.Value))
            {
                name.Error = "Enter your name.";
                ok = false;
            }
            else
            {
                name.Error = null;
            }

            var address = (email.Value ?? string.Empty).Trim();
            if (address.Length < 3 || !address.Contains('@') || !address.Contains('.'))
            {
                email.Error = "Enter an email address we can reply to.";
                ok = false;
            }
            else
            {
                email.Error = null;
            }

            if ((message.Value ?? string.Empty).Trim().Length < 8)
            {
                message.Error = "A sentence or two is enough.";
                ok = false;
            }
            else
            {
                message.Error = null;
            }

            if (!ok)
            {
                return;
            }

            form.IsVisible = false;
            confirmation.IsVisible = true;
        }

        void Reset()
        {
            form.IsVisible = true;
            confirmation.IsVisible = false;
            name.Error = null;
            email.Error = null;
            message.Error = null;
            name.Focus();
        }

        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("1.1*,0.9*") };
        root.Children.Add(leftHost);
        var side = SideColumn();
        side.Margin = new Thickness(26, 0, 0, 0);
        Grid.SetColumn(side, 1);
        root.Children.Add(side);

        return Mk.Surface(new Border { Padding = new Thickness(26, 24, 26, 24), Child = root });
    }

    private static Control SideColumn()
    {
        var copyEmail = new ArcCopyButton
        {
            Value = "hello@acme.com",
            Label = "Copy email",
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            IconOnly = true,
            VerticalAlignment = VerticalAlignment.Top
        };

        var supportHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        supportHeader.Children.Add(Mk.Disc("message-circle", 30, 14));
        supportHeader.Children.Add(new Border
        {
            Margin = new Thickness(10, 0, 0, 0),
            Child = Mk.Stack(1,
                Mk.Text("Chat with support", "arc-body-sm arc-medium"),
                Mk.Caption("Weekdays, 9:00 to 18:00 CET"))
        });
        Grid.SetColumn((Control)supportHeader.Children[1], 1);
        Grid.SetColumn(copyEmail, 2);
        supportHeader.Children.Add(copyEmail);

        var emailRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        emailRow.Children.Add(Mk.Disc("mail", 30, 14));
        var emailText = Mk.Stack(1,
            Mk.Text("hello@acme.com", "arc-body-sm arc-medium"),
            Mk.Caption("Replies within one business day"));
        emailText.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(emailText, 1);
        emailRow.Children.Add(emailText);

        var support = Mk.Panel(Mk.Stack(12,
            Mk.Caption("Support"),
            supportHeader,
            Mk.Divider(),
            emailRow), padding: 16, muted: true);

        var offices = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        offices.Children.Add(Office("Lisbon", "Rua da Prata 80", "09:41"));
        var toronto = Office("Toronto", "388 King Street West", "04:41");
        toronto.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(toronto, 1);
        offices.Children.Add(toronto);

        return Mk.Stack(10, support, offices);
    }

    private static Control Office(string city, string address, string time)
    {
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        top.Children.Add(Mk.Text(city, "arc-body-sm arc-medium"));
        var local = Mk.Caption($"{time} local");
        Grid.SetColumn(local, 1);
        top.Children.Add(local);

        return Mk.Panel(new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            Children = { top, Mk.Caption(address) }
        }, padding: 14);
    }
}
