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
/// Sign in: a compact card that moves from email to a six digit code to the signed-in account, with
/// single sign-on and a passkey entry point. Codes and passkeys are simulated.
/// </summary>
public sealed class SignInPage : ArcItemPage
{
    public override string ItemSlug => "sign-in";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The entry screen of an app with email code sign in.",
        "Sign in cards that also offer passkeys and single sign-on."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use login-centered, login-split or login-immersive for full-page auth layouts.",
        "Use signup-form to create an account instead."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Single sign-on is a labelled group of buttons with visible names, and step changes and errors are announced politely.",
        "Enter advances from the email field, the code row accepts digits, arrows and a paste, and Escape returns to the email step."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The card morphs its height between steps and slides content in the direction of travel.",
        "Reduced motion swaps the slide for a plain fade."
    };

    public override string? ImplementationNotes =>
        "Codes are checked against the documented demo code 123456; passkeys and single sign-on never leave the preview. The port keeps one card width across steps rather than measuring each step, so the height change is the only morph.";

    private const string DemoCode = "123456";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var step = 0;
        var email = "maya@northwind.co";
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
                var field = new OtpField(6, slotSize: 40, gap: 6, label: "Sign in code");
                field.SetDescription("The demo code is 123456.");
                field.Completed += (_, value) =>
                {
                    if (value == DemoCode)
                    {
                        Go(2);
                        AppKit.Say(status, "Code accepted. Signing you in.");
                    }
                    else
                    {
                        error = "That code is not right. Try 123456 in this preview.";
                        Build();
                        AppKit.Say(status, "The code did not match.");
                    }
                };

                content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        AppKit.Link("Back to email", () => Go(0), icon: "arrow-left"),
                        AppKit.Stack(3,
                            new TextBlock { Text = "Check your email", Classes = { "arc-subtitle" } },
                            AppKit.Body("We sent a six digit code to " + email + ".", 300)),
                        field,
                        string.IsNullOrEmpty(error) ? AppKit.Caption("Codes expire after 10 minutes.") : AppKit.FieldError(error),
                        AppKit.Btn("Verify and sign in", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                        {
                            if (field.Value == DemoCode)
                            {
                                Go(2);
                                AppKit.Say(status, "Code accepted.");
                            }
                            else
                            {
                                error = "That code is not right. Try 123456 in this preview.";
                                Build();
                            }
                        }, height: 40, stretch: true),
                        AppKit.Row(6,
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
                                    new TextBlock { Text = "Welcome back, Maya", Classes = { "arc-body", "arc-medium" } },
                                    AppKit.Caption("maya@northwind.co · Northwind Studio")
                                }
                            }),
                        AppKit.Tile(new StackPanel
                        {
                            Spacing = 6,
                            Children =
                            {
                                AppKit.Line("Workspace", "Northwind Studio"),
                                AppKit.Line("Role", "Admin"),
                                AppKit.Line("Last sign in", "2 days ago · Lisbon")
                            }
                        }, 12, 12, hairline: true),
                        AppKit.Btn("Continue to the workspace", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                        {
                            step = 0;
                            Build();
                            AppKit.Say(status, "The workspace is simulated here.");
                        }, trailingIcon: "arrow-right", height: 40, stretch: true),
                        AppKit.Link("Not you? Use another account", () => Go(0))
                    }
                };
            }
            else
            {
                var emailField = AppKit.Input("maya@northwind.co", email, value => email = value);
                content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        AppKit.Stack(3,
                            new TextBlock { Text = "Sign in to Northwind", Classes = { "arc-subtitle" } },
                            AppKit.Body("Use a passkey, a code by email, or your work account.", 300)),
                        AppKit.Providers(provider =>
                        {
                            Go(2);
                            AppKit.Say(status, $"Continuing with {provider}. The OAuth redirect is simulated.");
                        }),
                        AppKit.Row(10,
                            new Border { Height = 1, Width = 84, Background = AppKit.Brush(ArcTokens.BorderSubtle) },
                            AppKit.Caption("or use email"),
                            new Border { Height = 1, Width = 84, Background = AppKit.Brush(ArcTokens.BorderSubtle) }),
                        AppKit.Field("Work email", emailField),
                        AppKit.Btn("Email me a code", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                        {
                            if (!email.Contains('@'))
                            {
                                error = "Enter a valid email address.";
                                Build();
                                return;
                            }

                            step = 1;
                            Build();
                            AppKit.Say(status, "Code sent to " + email + ". Use 123456.");
                        }, height: 40, stretch: true),
                        string.IsNullOrEmpty(error) ? AppKit.Caption("We never ask for a password.") : AppKit.FieldError(error),
                        AppKit.Btn("Sign in with a passkey", ArcButtonVariant.Ghost, ArcButtonSize.Md, () =>
                        {
                            Go(2);
                            AppKit.Say(status, "A passkey prompt would open here.");
                        }, icon: "key-round", height: 36, stretch: true),
                        AppKit.Row(5,
                            AppKit.Caption("New to Northwind?"),
                            AppKit.Link("Create an account", () => AppKit.Say(status, "Sign up is its own block.")))
                    }
                };
            }

            host.Content = content;
        }

        Build();

        var card = new Border
        {
            Width = 380,
            Padding = new Thickness(20),
            Background = AppKit.Brush(ArcTokens.Surface),
            BorderBrush = AppKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            BoxShadow = AppKit.Shadow(ArcTokens.ShadowRaised),
            Child = host
        };

        var stage = new Panel
        {
            Children =
            {
                new Border { Background = AppKit.Brush(ArcTokens.SurfaceMuted) },
                new Border { Background = AppKit.Glow(ArcTokens.Accent, 0.12) },
                new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = card
                }
            }
        };

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                stage,
                AppKit.Place(new Border
                {
                    Padding = new Thickness(20, 0, 20, 10),
                    Child = status
                }, 0, 1)
            }
        };

        AppKit.Shortcut(layout, Key.Escape, () =>
        {
            if (step > 0)
            {
                Go(0);
                AppKit.Say(status, "Back to the email step.");
            }
        });

        return AppKit.Screen(layout, 508);
    }
}
