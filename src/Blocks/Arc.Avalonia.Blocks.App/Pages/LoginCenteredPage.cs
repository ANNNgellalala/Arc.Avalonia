using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App.Pages;

/// <summary>
/// Centered login: a passkey-first sign in and sign up card on a quiet ring backdrop that moves through the
/// email and code steps. The backdrop is drawn with token glows and rings instead of the reference's SVG.
/// </summary>
public sealed class LoginCenteredPage : ArcItemPage
{
    public override string ItemSlug => "login-centered";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-sign-in", "variant-passkey", "variant-ring-backdrop", "state-first-step"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A full page sign in or sign up where passkeys are the main method.",
        "Auth flows that fall back to a six digit email code."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use login-split for password sign in beside a brand photo.",
        "Use sign-in when the form sits inside an existing page."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The email field is a labelled input, the code step is a labelled group, and busy buttons announce their busy state while keeping focus.",
        "Enter continues from the email field, the code row takes digits and a paste, and Escape returns to the first step."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Steps slide and blur between passkey, email, code and done while the card height follows; the resend label rolls.",
        "Reduced motion swaps slides for fades and shows the passkey check without drawing it."
    };

    public override string? ImplementationNotes =>
        "The ring backdrop is static: the reference drifts it with the pointer, which the offscreen gallery cannot demonstrate. Passkeys, code delivery and single sign-on are simulated, and the demo code is 482913.";

    private const string DemoCode = "482913";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var step = 0;
        var mode = "sign-in";
        var email = "maya@northwind.studio";
        var error = string.Empty;
        var host = new ContentControl();

        void Go(int next)
        {
            step = next;
            error = string.Empty;
            Build();
        }

        void Build()
        {
            Control content;
            if (step == 1)
            {
                var field = new OtpField(6, slotSize: 42, gap: 7, label: "Sign in code") { Value = string.Empty };
                field.SetDescription("The demo code is 482913.");
                field.Completed += (_, value) =>
                {
                    if (value == DemoCode)
                    {
                        Go(2);
                        AppKit.Say(status, "Code accepted.");
                    }
                    else
                    {
                        error = "That code is not right.";
                        Build();
                    }
                };

                content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        AppKit.Stack(3,
                            new TextBlock { Text = mode == "sign-up" ? "Confirm your email" : "Check your email", Classes = { "arc-subtitle" } },
                            AppKit.Body("We sent a six digit code to " + email + ".", 280)),
                        field,
                        string.IsNullOrEmpty(error) ? AppKit.Caption("Expires in 10 minutes.") : AppKit.FieldError(error),
                        AppKit.Btn("Continue", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                        {
                            if (field.Value == DemoCode)
                            {
                                Go(2);
                                AppKit.Say(status, "Code accepted.");
                            }
                            else
                            {
                                error = "That code is not right. Use 482913 in this preview.";
                                Build();
                            }
                        }, height: 40, stretch: true),
                        AppKit.Link("Use a different email", () => Go(0)),
                        AppKit.Row(5,
                            AppKit.Caption("No code yet?"),
                            AppKit.Link("Resend", () => AppKit.Say(status, "A new code would be sent.")))
                    }
                };
            }
            else if (step == 2)
            {
                content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        AppKit.Row(10,
                            AppKit.Person("Maya Chen", ArcAvatarSize.Lg),
                            new StackPanel
                            {
                                Spacing = 1,
                                Children =
                                {
                                    new TextBlock
                                    {
                                        Text = mode == "sign-up" ? "You are all set" : "Signed in as Maya",
                                        Classes = { "arc-body", "arc-medium" }
                                    },
                                    AppKit.Caption(mode == "sign-up" ? "Your workspace is ready." : "Northwind Studio · Admin")
                                }
                            }),
                        AppKit.Tile(AppKit.Stack(6,
                            AppKit.Line("Email", email),
                            AppKit.Line("Method", "Email code"),
                            AppKit.Line("Mode", mode == "sign-up" ? "New account" : "Returning account")), 12, 12, hairline: true),
                        AppKit.Btn("Open Northwind", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                        {
                            step = 0;
                            Build();
                            AppKit.Say(status, "The workspace is simulated.");
                        }, trailingIcon: "arrow-right", height: 40, stretch: true),
                        AppKit.Link("Sign out", () => Go(0))
                    }
                };
            }
            else
            {
                content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        AppKit.Row(9,
                            AppKit.Mark("compass", 30, 15),
                            new TextBlock { Text = "Northwind", Classes = { "arc-body-sm", "arc-medium" } }),
                        AppKit.Stack(3,
                            new TextBlock
                            {
                                Text = mode == "sign-up" ? "Create your account" : "Sign in to Northwind",
                                Classes = { "arc-subtitle" }
                            },
                            AppKit.Body(mode == "sign-up"
                                ? "One card for both, with a passkey first."
                                : "Passkeys first, with an email code as the fallback.", 280)),
                        AppKit.Btn("Continue with a passkey", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                        {
                            Go(2);
                            AppKit.Say(status, "A passkey prompt would open here.");
                        }, icon: "key-round", height: 40, stretch: true),
                        AppKit.Row(10,
                            new Border { Height = 1, Width = 72, Background = AppKit.Brush(ArcTokens.BorderSubtle) },
                            AppKit.Caption("or"),
                            new Border { Height = 1, Width = 72, Background = AppKit.Brush(ArcTokens.BorderSubtle) }),
                        AppKit.Field("Email", AppKit.Input("maya@northwind.studio", email, value => email = value)),
                        AppKit.Btn("Email me a code", ArcButtonVariant.Secondary, ArcButtonSize.Md, () =>
                        {
                            if (!email.Contains('@'))
                            {
                                error = "Enter a valid email address.";
                                Build();
                                return;
                            }

                            step = 1;
                            Build();
                            AppKit.Say(status, "Code sent. Use 482913 in this preview.");
                        }, height: 40, stretch: true),
                        string.IsNullOrEmpty(error) ? AppKit.Caption("No password to remember.") : AppKit.FieldError(error),
                        AppKit.Providers(provider =>
                        {
                            Go(2);
                            AppKit.Say(status, $"Continuing with {provider}. The OAuth redirect is simulated.");
                        }),
                        AppKit.Row(5,
                            AppKit.Caption(mode == "sign-up" ? "Already have an account?" : "New to Arc?"),
                            AppKit.Link(mode == "sign-up" ? "Sign in" : "Create an account", () =>
                            {
                                mode = mode == "sign-up" ? "sign-in" : "sign-up";
                                Build();
                                AppKit.Say(status, mode == "sign-up" ? "Sign up mode." : "Sign in mode.");
                            }))
                    }
                };
            }

            host.Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    content,
                    AppKit.Caption("By continuing you agree to the terms and the privacy policy.", 300)
                }
            };
        }

        Build();

        var card = new Border
        {
            Width = 360,
            Padding = new Thickness(20),
            Background = AppKit.Brush(ArcTokens.Surface),
            BorderBrush = AppKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            BoxShadow = AppKit.Shadow(ArcTokens.ShadowRaised),
            Child = host
        };

        var backdrop = new Panel
        {
            Children =
            {
                new Border { Background = AppKit.Brush(ArcTokens.SurfaceMuted) },
                new Border { Background = AppKit.Glow(ArcTokens.Accent, 0.16, 0.9) },
                new Ellipse
                {
                    Width = 420,
                    Height = 420,
                    Stroke = AppKit.Tint(ArcTokens.Accent, 0.18),
                    StrokeThickness = 1,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                new Ellipse
                {
                    Width = 620,
                    Height = 620,
                    Stroke = AppKit.Brush(ArcTokens.BorderSubtle),
                    StrokeThickness = 1,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                new Ellipse
                {
                    Width = 820,
                    Height = 820,
                    Stroke = AppKit.Brush(ArcTokens.BorderSubtle),
                    StrokeThickness = 1,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };

        var layer = new Panel { Children = { backdrop, new Border { Child = card, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, status } };
        status.HorizontalAlignment = HorizontalAlignment.Center;
        status.VerticalAlignment = VerticalAlignment.Bottom;
        status.Margin = new Thickness(0, 0, 0, 12);

        AppKit.Shortcut(layer, Key.Escape, () =>
        {
            if (step > 0)
            {
                Go(0);
                AppKit.Say(status, "Back to the first step.");
            }
        });

        return AppKit.Screen(layer, 540);
    }
}
