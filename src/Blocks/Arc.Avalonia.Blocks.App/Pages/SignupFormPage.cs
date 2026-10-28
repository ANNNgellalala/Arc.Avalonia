using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App.Pages;

/// <summary>
/// Sign up form: an account creation card with single sign-on, name, email, a live password strength meter,
/// a product updates opt-in, local validation, and a completion panel. No account is created.
/// </summary>
public sealed class SignupFormPage : ArcItemPage
{
    public override string ItemSlug => "signup-form";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The client side of an email and password sign-up.",
        "Sign-up pages that show password strength and a product updates opt-in."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use sign-in or login-split for returning users.",
        "Use otp-input for passwordless or code-based sign-up."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Invalid fields expose their message beside the field, and the strength meter reports its level as text, not colour alone.",
        "Tab moves through the providers and fields, Enter submits the form, the provider buttons keep focus while busy, and the success panel is announced."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The card height springs between the form and the completion panel, and the strength bars fill as you type.",
        "Reduced motion removes the height spring and fills the bars instantly."
    };

    public override string? ImplementationNotes =>
        "Validation is local and synchronous: the name needs two characters, the email a basic shape, and the password eight characters. Single sign-on buttons simulate the provider and show the completion panel; there is no OAuth redirect.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var name = "Maya Chen";
        var email = "maya@northwind.co";
        var password = "northwind-2026";
        var updates = true;
        var nameError = string.Empty;
        var emailError = string.Empty;
        var passwordError = string.Empty;
        var created = false;
        var busy = false;

        var formHost = new ContentControl();

        void Submit()
        {
            nameError = name.Trim().Length < 2 ? "Enter your full name." : string.Empty;
            emailError = !email.Contains('@') || !email.Contains('.') ? "Enter a valid email address." : string.Empty;
            passwordError = password.Length < 8 ? "Use at least 8 characters." : string.Empty;
            if (nameError.Length + emailError.Length + passwordError.Length > 0)
            {
                Build();
                AppKit.Say(status, "Check the highlighted fields.");
                return;
            }

            busy = true;
            Build();
            AppKit.Say(status, "Creating the account. The request is simulated.");

            global::Avalonia.Threading.DispatcherTimer.RunOnce(() =>
            {
                busy = false;
                created = true;
                Build();
                AppKit.Say(status, "Account created for " + email + ". No email was sent.");
            }, TimeSpan.FromMilliseconds(650));
        }

        Control FieldBlock(string label, Control control, string error, string? hint = null)
        {
            var panel = new StackPanel { Spacing = 5 };
            panel.Children.Add(AppKit.Caption(label));
            panel.Children.Add(control);
            if (!string.IsNullOrWhiteSpace(error))
            {
                panel.Children.Add(AppKit.FieldError(error));
            }
            else if (!string.IsNullOrWhiteSpace(hint))
            {
                panel.Children.Add(AppKit.Caption(hint!));
            }

            return panel;
        }

        void BuildForm()
        {
            if (created)
            {
                formHost.Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        AppKit.Row(10,
                            AppKit.Disc("circle-check", 40, 19, true),
                            new StackPanel
                            {
                                Spacing = 1,
                                Children =
                                {
                                    new TextBlock { Text = "Account created", Classes = { "arc-body", "arc-medium" } },
                                    AppKit.Caption("Welcome to Northwind, Maya.")
                                }
                            }),
                        AppKit.Body("We sent a confirmation link to " + email + ". Open it to finish setting up your workspace.", 320),
                        AppKit.Divider(),
                        AppKit.Stack(6,
                            AppKit.Line("Workspace", "Northwind Studio"),
                            AppKit.Line("Plan", "Team · 14 day trial"),
                            AppKit.Line("Product updates", updates ? "On" : "Off")),
                        AppKit.Btn("Open the workspace", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                        {
                            created = false;
                            Build();
                            AppKit.Say(status, "The workspace tour is simulated.");
                        }, trailingIcon: "arrow-right", height: 40, stretch: true),
                        AppKit.Link("Use a different email", () =>
                        {
                            created = false;
                            email = string.Empty;
                            Build();
                        })
                    }
                };
                return;
            }

            var strength = new ArcPasswordStrength
            {
                Value = password,
                Placeholder = "At least 8 characters"
            };
            strength.ValueChanged += (_, args) =>
            {
                password = args.Value;
                passwordError = string.Empty;
            };

            formHost.Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    AppKit.Stack(3,
                        new TextBlock { Text = "Create your account", Classes = { "arc-heading" } },
                        AppKit.Body("Start free for 14 days. No card needed.", 320)),
                    AppKit.Providers(provider =>
                    {
                        busy = true;
                        Build();
                        AppKit.Say(status, $"Continuing with {provider}. The OAuth redirect is simulated.");
                        global::Avalonia.Threading.DispatcherTimer.RunOnce(() =>
                        {
                            busy = false;
                            created = true;
                            Build();
                            AppKit.Say(status, $"Signed up with {provider}.");
                        }, TimeSpan.FromMilliseconds(700));
                    }),
                    AppKit.Row(10,
                        new Border { Height = 1, Width = 96, Background = AppKit.Brush(ArcTokens.BorderSubtle), VerticalAlignment = VerticalAlignment.Center },
                        AppKit.Caption("or use your email"),
                        new Border { Height = 1, Width = 96, Background = AppKit.Brush(ArcTokens.BorderSubtle), VerticalAlignment = VerticalAlignment.Center }),
                    AppKit.Columns("*,*",
                        FieldBlock("Full name", AppKit.Input("Maya Chen", name, value =>
                        {
                            name = value;
                            nameError = string.Empty;
                        }), nameError),
                        FieldBlock("Work email", AppKit.Input("maya@northwind.co", email, value =>
                        {
                            email = value;
                            emailError = string.Empty;
                        }), emailError)),
                    FieldBlock("Password", strength, passwordError, "Use a phrase you can remember."),
                    AppKit.Check("Email me product updates", updates, value => updates = value),
                    AppKit.Btn(busy ? "Creating account" : "Create account", ArcButtonVariant.Primary, ArcButtonSize.Md,
                        Submit, height: 42, stretch: true).With(button => button.Loading = busy)
                }
            };
        }

        void Build()
        {
            BuildForm();
        }

        Build();

        var card = AppKit.Card(formHost, 20, 20, width: 500);
        var aside = AppKit.Stack(10,
            new TextBlock { Text = "What you get", Classes = { "arc-label" } },
            AppKit.Tile(AppKit.Stack(7,
                AppKit.Meta("users", "Unlimited teammates"),
                AppKit.Caption("Invite as many people as you like on the trial.")), 12, 12, hairline: true),
            AppKit.Tile(AppKit.Stack(7,
                AppKit.Meta("shield-check", "SOC 2 controls"),
                AppKit.Caption("Sessions, two factor and audit history are built in.")), 12, 12, hairline: true),
            AppKit.Tile(AppKit.Stack(7,
                AppKit.Meta("credit-card", "No card for 14 days"),
                AppKit.Caption("Pick a plan only when the trial ends.")), 12, 12, hairline: true),
            status);

        var stage = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("500,280"),
            ColumnSpacing = 34,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top
        };
        stage.Children.Add(card);
        Grid.SetColumn(aside, 1);
        stage.Children.Add(aside);

        return AppKit.Screen(new Border { Padding = new Thickness(24, 18, 24, 18), Child = stage }, 590);
    }
}
