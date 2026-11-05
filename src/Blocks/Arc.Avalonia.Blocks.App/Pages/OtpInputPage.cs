using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App.Pages;

/// <summary>
/// OTP input: a six digit verification card with a drawn slot row, paste fill, arrow-key movement and an
/// inline error and success state. The code is checked locally; no message is sent.
/// </summary>
public sealed class OtpInputPage : ArcItemPage
{
    public override string ItemSlug => "otp-input";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-filled", "variant-six-slots", "variant-verify-action", "variant-guidance"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Verification codes from email or SMS.",
        "Two-factor authentication code entry."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use password-field for secrets people type from memory.",
        "Use input for any other text entry."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The slots sit in a labelled group, each named with its position, and helper and error copy are tied to the row.",
        "Digits fill and advance, Arrow keys move, Backspace walks back, Ctrl/Cmd+V spreads a pasted code across the slots, and errors are announced."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "A filled character rises into its slot and the error nudge shakes the row once; the focus ring moves between slots.",
        "Reduced motion removes the rise, the shake and the glide."
    };

    public override string? ImplementationNotes =>
        "The reference uses six DOM inputs, which lets the platform offer SMS autofill. The port draws the slots and reads keys itself so the focus ring and filled state stay exact, so one-time-code autofill is not available; verification is a local comparison against 482913.";

    private const string DemoCode = "482913";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var field = new OtpField(6, slotSize: 46, gap: 8, label: "Verification code") { Value = DemoCode };
        var feedback = new ContentControl();
        var verifyButton = new ContentControl();

        void BuildFeedback(string? error = null, bool success = false)
        {
            if (success)
            {
                feedback.Content = AppKit.Row(7,
                    new ArcIcon { Icon = "circle-check", Size = 15, Foreground = AppKit.Brush(ArcTokens.Success) },
                    new TextBlock { Text = "Code verified. Continue to your workspace.", Classes = { "arc-body-sm" } });
                return;
            }

            if (error is not null)
            {
                field.Error = error;
                feedback.Content = AppKit.Row(7,
                    new ArcIcon { Icon = "circle-alert", Size = 15, Foreground = AppKit.Brush(ArcTokens.Danger) },
                    new TextBlock { Text = error, Classes = { "arc-body-sm" }, Foreground = AppKit.Brush(ArcTokens.Danger) });
                return;
            }

            feedback.Content = AppKit.Caption("The code expires in 9 minutes. Paste it straight from the email.");
        }

        void Verify()
        {
            if (field.Value == DemoCode)
            {
                BuildFeedback(success: true);
                AppKit.Say(status, "Code verified. The account step is simulated.");
            }
            else
            {
                BuildFeedback("That code is not right. Check the email and try again.");
                AppKit.Say(status, "Verification failed: the code did not match.");
            }
        }

        field.Completed += (_, value) =>
        {
            if (value == DemoCode)
            {
                BuildFeedback(success: true);
                AppKit.Say(status, "Six digits entered and verified.");
            }
        };
        field.ValueChanged += (_, _) =>
        {
            if (field.Value.Length < 6)
            {
                field.Error = null;
                BuildFeedback();
            }
        };

        BuildFeedback();

        var verify = AppKit.Btn("Verify code", ArcButtonVariant.Primary, ArcButtonSize.Md, Verify, height: 42, stretch: true);
        var resend = AppKit.Btn("Resend code", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () =>
        {
            field.Value = string.Empty;
            BuildFeedback();
            AppKit.Say(status, "A new code would be sent. The countdown restarts in the full app.");
        }, icon: "refresh-cw", height: 30);

        var card = AppKit.Card(new StackPanel
        {
            Spacing = 14,
            Children =
            {
                AppKit.Row(9,
                    AppKit.Disc("shield-check", 34, 16, true),
                    new StackPanel
                    {
                        Spacing = 0,
                        Children =
                        {
                            new TextBlock { Text = "Verify your email", Classes = { "arc-body", "arc-medium" } },
                            AppKit.Caption("We sent a code to maya@northwind.co")
                        }
                    }),
                AppKit.Field("Verification code", field),
                feedback,
                verify,
                AppKit.Row(8,
                    resend,
                    AppKit.Place(AppKit.Caption("Resend in 00:24"), 0)),
                AppKit.Divider(),
                AppKit.Link("Use a different email", () => AppKit.Say(status, "The email step is simulated."), icon: "arrow-left")
            }
        }, 18, 18);

        var right = AppKit.Stack(10,
            new TextBlock { Text = "How the code works", Classes = { "arc-label" } },
            AppKit.Tile(AppKit.Stack(6,
                AppKit.Meta("clock", "Expires in 10 minutes"),
                AppKit.Caption("Each code can be used once. A new code invalidates the old one.")), 12, 12, hairline: true),
            AppKit.Tile(AppKit.Stack(6,
                AppKit.Meta("lock", "Six digits, numbers only"),
                AppKit.Caption("Letters and spaces are dropped, so a pasted code still lands cleanly.")), 12, 12, hairline: true),
            AppKit.Tile(AppKit.Stack(6,
                AppKit.Meta("key-round", "Demo code 482913"),
                AppKit.Caption("Any other six digits show the error state so you can see both paths.")), 12, 12, hairline: true),
            status);

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("420,340"),
            ColumnSpacing = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(card);
        Grid.SetColumn(right, 1);
        row.Children.Add(right);

        return AppKit.Screen(new Border { Padding = new Thickness(24), Child = row }, 400);
    }
}
