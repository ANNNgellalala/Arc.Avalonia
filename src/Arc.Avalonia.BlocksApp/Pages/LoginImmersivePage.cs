using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksApp.Pages;

/// <summary>
/// Immersive login: a magic link card over a dusk gradient wash. Sending folds the form into an envelope
/// state with a resend countdown. The reference uses dusk photography; the port uses a token gradient, since
/// it ships no photography.
/// </summary>
public sealed class LoginImmersivePage : ArcItemPage
{
    public override string ItemSlug => "login-immersive";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A magic link sign in page with a strong brand backdrop behind a glass card.",
        "Consumer or creative products where the sign in screen carries the brand mood."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use login-centered for passkey-first auth with an email code.",
        "Use login-split for password sign in with a quieter layout."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The email field is a labelled input and the provider buttons sit in a labelled group; busy actions stay focusable.",
        "Enter sends the link, Escape returns from the envelope to the form, and the sent state is announced in a polite status region."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "After sending, the form gives way to an envelope that folds shut while the resend countdown rolls.",
        "Reduced motion replaces the fold and the slide with fades."
    };

    public override string? ImplementationNotes =>
        "No mail is sent: the sent state and the countdown are local timers. The backdrop is a token gradient with a soft glow rather than the reference's photograph, and the glass card falls back to a solid surface at 92 percent opacity instead of a backdrop blur.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var email = "maya@northwind.co";
        var sent = false;
        var seconds = 24;
        var host = new ContentControl();

        void Build()
        {
            if (sent)
            {
                host.Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        AppKit.Disc("mail", 42, 20, true),
                        new TextBlock { Text = "Check your inbox", Classes = { "arc-subtitle" } },
                        AppKit.Body("A sign in link is on its way to " + email + ". It expires in 15 minutes.", 280),
                        AppKit.Tile(new StackPanel
                        {
                            Spacing = 6,
                            Children =
                            {
                                AppKit.Line("Sent to", email),
                                AppKit.Line("Expires", "In 15 minutes"),
                                AppKit.Line("One use", "Yes")
                            }
                        }, 12, 12, hairline: true),
                        AppKit.Btn("Open the mail app", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                            AppKit.Say(status, "Opening a mail client is simulated."),
                            icon: "external-link", height: 40, stretch: true),
                        AppKit.Row(6,
                            AppKit.Caption($"Resend in 00:{seconds:00}"),
                            AppKit.Link("Resend now", () =>
                            {
                                seconds = 30;
                                Build();
                                AppKit.Say(status, "A new link would be sent.");
                            })),
                        AppKit.Link("Use another email", () =>
                        {
                            sent = false;
                            Build();
                            AppKit.Say(status, "Back to the email form.");
                        }, icon: "arrow-left")
                    }
                };
                return;
            }

            host.Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    AppKit.Stack(3,
                        new TextBlock { Text = "Sign in with a link", Classes = { "arc-subtitle" } },
                        AppKit.Body("No password. We email you a link that signs you in once.", 280)),
                    AppKit.Field("Email", AppKit.Input("maya@northwind.co", email, value => email = value)),
                    AppKit.Btn("Email me a link", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                    {
                        sent = true;
                        seconds = 30;
                        Build();
                        AppKit.Say(status, "Magic link sent to " + email + ". Nothing left the machine.");
                    }, height: 40, stretch: true),
                    AppKit.Row(10,
                        new Border { Height = 1, Width = 76, Background = AppKit.Tint(ArcTokens.Foreground, 0.18) },
                        AppKit.Caption("or continue with"),
                        new Border { Height = 1, Width = 76, Background = AppKit.Tint(ArcTokens.Foreground, 0.18) }),
                    AppKit.Providers(provider =>
                    {
                        sent = true;
                        Build();
                        AppKit.Say(status, $"Continuing with {provider}. The OAuth redirect is simulated.");
                    }),
                    AppKit.Caption("By continuing you agree to the terms and the privacy policy.", 290)
                }
            };
        }

        Build();

        var glass = new Border
        {
            Width = 396,
            Padding = new Thickness(20),
            Background = AppKit.Tint(ArcTokens.Surface, 0.93),
            BorderBrush = AppKit.Tint(ArcTokens.Foreground, 0.14),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            BoxShadow = AppKit.Shadow(),
            Child = host
        };

        var backdrop = new Panel
        {
            Children =
            {
                new Border { Background = AppKit.BrandWash(0.92, 0.55) },
                new Border { Background = AppKit.Glow(ArcTokens.GradientTo, 0.5, 0.95) },
                new Border
                {
                    Width = 720,
                    Height = 720,
                    CornerRadius = new CornerRadius(360),
                    Background = AppKit.Tint(ArcTokens.Background, 0.1),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, -220, -320)
                }
            }
        };

        var topBar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(20, 16, 20, 0),
            Children =
            {
                AppKit.Row(8,
                    AppKit.Mark("compass", 28, 14),
                    new TextBlock
                    {
                        Text = "Northwind",
                        Classes = { "arc-body-sm", "arc-medium" },
                        Foreground = AppKit.Brush(ArcTokens.GradientForeground)
                    }),
                AppKit.Place(AppKit.Link("Back to northwind.co", () =>
                    AppKit.Say(status, "Navigation is simulated."), icon: "arrow-left"), 1)
            }
        };

        var cardLayer = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = glass
        };

        var footer = AppKit.Row(10,
            AppKit.Caption("Terms"),
            AppKit.Caption("Privacy"),
            AppKit.Caption("Status"),
            AppKit.Place(AppKit.Caption("Northwind Studio 2026"), 0));
        footer.HorizontalAlignment = HorizontalAlignment.Center;
        footer.Margin = new Thickness(0, 0, 0, 14);

        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"),
            Children =
            {
                topBar,
                AppKit.Place(cardLayer, 0, 1),
                AppKit.Place(status, 0, 2),
                AppKit.Place(footer, 0, 3)
            }
        };
        status.HorizontalAlignment = HorizontalAlignment.Center;
        status.Margin = new Thickness(0, 0, 0, 8);

        var layer = new Panel { Children = { backdrop, content } };

        AppKit.Shortcut(layer, Key.Escape, () =>
        {
            if (sent)
            {
                sent = false;
                Build();
                AppKit.Say(status, "Back to the email form.");
            }
        });

        return AppKit.Screen(layer, 500);
    }
}
