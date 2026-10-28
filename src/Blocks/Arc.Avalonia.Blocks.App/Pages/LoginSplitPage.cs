using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App.Pages;

/// <summary>
/// Login split: an email-first sign in that morphs into the password step beside a brand panel. The photo is
/// a token gradient with the quote laid over it; the demo password is arc-demo.
/// </summary>
public sealed class LoginSplitPage : ArcItemPage
{
    public override string ItemSlug => "login-split";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Password-based sign in with an email-first step beside a brand photo.",
        "Sign in pages that show the recognised account before asking for a password."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use login-centered for passkey-first auth.",
        "Use login-immersive for magic link auth instead."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Fields use work email and current password autofill hints; errors are announced and tied to the field.",
        "Enter continues from the email step and submits the password step, Tab reaches the links, and Escape returns to the email step."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Continue morphs the email form into the password step with the recognised account avatar, and labels roll between states.",
        "Reduced motion uses fades and skips the label roll."
    };

    public override string? ImplementationNotes =>
        "The sample accounts are Maya Chen and Leo Park; the demo password is arc-demo. The brand photo is a token gradient because the port ships no photography, and the split stays side by side rather than stacking, since the gallery renders one fixed width.";

    private static readonly (string Name, string Email)[] Accounts =
    {
        ("Maya Chen", "maya@northwind.co"),
        ("Leo Park", "leo@northwind.co")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var step = 0;
        var email = Accounts[0].Email;
        var password = string.Empty;
        var error = string.Empty;
        var account = Accounts[0];
        var host = new ContentControl();

        void Go(int next)
        {
            step = next;
            error = string.Empty;
            Build();
        }

        void Build()
        {
            if (step == 1)
            {
                var recognised = Accounts.FirstOrDefault(entry => string.Equals(entry.Email, email, StringComparison.OrdinalIgnoreCase));
                account = recognised.Email is null ? Accounts[0] : recognised;
                var passwordField = new ArcPasswordField
                {
                    Placeholder = "Enter your password",
                    Value = password,
                    CapsLockHint = true,
                    Width = 320
                };
                passwordField.ValueChanged += (_, _) => password = passwordField.Value ?? string.Empty;

                host.Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        AppKit.Link("Use another account", () => Go(0), icon: "arrow-left"),
                        AppKit.Row(10,
                            AppKit.Person(account.Name, ArcAvatarSize.Md),
                            new StackPanel
                            {
                                Spacing = 1,
                                Children =
                                {
                                    new TextBlock { Text = account.Name, Classes = { "arc-body", "arc-medium" } },
                                    AppKit.Caption(account.Email)
                                }
                            }),
                        AppKit.Field("Password", passwordField),
                        string.IsNullOrEmpty(error)
                            ? AppKit.Caption("Demo password: arc-demo", 300)
                            : AppKit.FieldError(error),
                        AppKit.Btn("Sign in", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                        {
                            if (password != "arc-demo")
                            {
                                error = "That password does not match. Use arc-demo in this preview.";
                                Build();
                                AppKit.Say(status, "Sign in failed: the password did not match.");
                                return;
                            }

                            Go(2);
                            AppKit.Say(status, "Signed in as " + account.Name + ". The session is simulated.");
                        }, height: 42, stretch: true),
                        AppKit.Row(8,
                            AppKit.Check("Keep me signed in", true),
                            AppKit.Link("Forgot password?", () => AppKit.Say(status, "A reset email would be sent."))),
                        AppKit.Divider(),
                        AppKit.Providers(provider =>
                        {
                            Go(2);
                            AppKit.Say(status, $"Continuing with {provider}. The OAuth redirect is simulated.");
                        })
                    }
                };
                return;
            }

            if (step == 2)
            {
                host.Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        AppKit.Row(10,
                            AppKit.Person(account.Name, ArcAvatarSize.Lg),
                            new StackPanel
                            {
                                Spacing = 1,
                                Children =
                                {
                                    new TextBlock { Text = "Welcome back, " + account.Name.Split(' ')[0], Classes = { "arc-body", "arc-medium" } },
                                    AppKit.Caption("Northwind Studio · signed in with a password")
                                }
                            }),
                        AppKit.Tile(new StackPanel
                        {
                            Spacing = 6,
                            Children =
                            {
                                AppKit.Line("Email", account.Email),
                                AppKit.Line("Session", "Expires in 7 days"),
                                AppKit.Line("This device", "Windows · Lisbon")
                            }
                        }, 12, 12, hairline: true),
                        AppKit.Btn("Open the workspace", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                        {
                            step = 0;
                            password = string.Empty;
                            Build();
                            AppKit.Say(status, "The workspace is simulated.");
                        }, trailingIcon: "arrow-right", height: 42, stretch: true),
                        AppKit.Link("Sign out", () => Go(0))
                    }
                };
                return;
            }

            host.Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    AppKit.Row(9,
                        AppKit.Mark("compass", 28, 14),
                        new TextBlock { Text = "Northwind", Classes = { "arc-body-sm", "arc-medium" } }),
                    AppKit.Stack(3,
                        new TextBlock { Text = "Sign in", Classes = { "arc-title-sm" } },
                        AppKit.Body("Use your work email to continue.", 300)),
                    AppKit.Field("Work email", AppKit.Input("maya@northwind.co", email, value => email = value)),
                    string.IsNullOrEmpty(error) ? AppKit.Caption("Try maya@northwind.co or leo@northwind.co.", 300) : AppKit.FieldError(error),
                    AppKit.Btn("Continue", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                    {
                        if (!email.Contains('@'))
                        {
                            error = "Enter a valid email address.";
                            Build();
                            AppKit.Say(status, "Enter a valid email address.");
                            return;
                        }

                        Go(1);
                        AppKit.Say(status, "Password step. The account was recognised.");
                    }, trailingIcon: "arrow-right", height: 42, stretch: true),
                    AppKit.Divider(),
                    AppKit.Providers(provider =>
                    {
                        Go(2);
                        AppKit.Say(status, $"Continuing with {provider}. The OAuth redirect is simulated.");
                    }),
                    AppKit.Row(5,
                        AppKit.Caption("New to Northwind?"),
                        AppKit.Link("Create an account", () => AppKit.Say(status, "Sign up is its own block.")))
                }
            };
        }

        Build();

        var form = new Border
        {
            Width = 460,
            Padding = new Thickness(26, 22, 26, 22),
            Background = AppKit.Brush(ArcTokens.Surface),
            Child = new StackPanel
            {
                Spacing = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { host, status }
            }
        };

        var brand = new Border
        {
            Background = AppKit.BrandWash(0.9, 0.5),
            Child = new Panel
            {
                Children =
                {
                    new Border { Background = AppKit.Glow(ArcTokens.GradientTo, 0.45, 0.85) },
                    new StackPanel
                    {
                        Spacing = 10,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(24, 0, 24, 24),
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "\"We moved the whole workspace in an afternoon. The sidebar alone saved us a week.\"",
                                Classes = { "arc-body" },
                                TextWrapping = TextWrapping.Wrap,
                                MaxWidth = 320,
                                Foreground = AppKit.Brush(ArcTokens.GradientForeground)
                            },
                            AppKit.Row(8,
                                AppKit.Person("Ana Duarte", ArcAvatarSize.Sm),
                                new StackPanel
                                {
                                    Spacing = 0,
                                    Children =
                                    {
                                        new TextBlock
                                        {
                                            Text = "Ana Duarte",
                                            Classes = { "arc-caption" },
                                            Foreground = AppKit.Brush(ArcTokens.GradientForeground)
                                        },
                                        new TextBlock
                                        {
                                            Text = "Head of design, Northwind",
                                            Classes = { "arc-caption" },
                                            Foreground = AppKit.Tint(ArcTokens.GradientForeground, 0.75)
                                        }
                                    }
                                })
                        }
                    }
                }
            }
        };

        var split = new Grid { ColumnDefinitions = new ColumnDefinitions("460,*") };
        split.Children.Add(form);
        Grid.SetColumn(brand, 1);
        split.Children.Add(brand);

        AppKit.Shortcut(split, Key.Escape, () =>
        {
            if (step > 0)
            {
                Go(0);
                AppKit.Say(status, "Back to the email step.");
            }
        });

        return AppKit.Screen(split, 500);
    }
}
