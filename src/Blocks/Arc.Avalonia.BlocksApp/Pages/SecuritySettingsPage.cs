using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksApp.Pages;

/// <summary>
/// Security settings: two factor setup walked along a three-step rail (scan, verify, backup codes), a
/// password change with a strength meter, and active sessions that can be revoked one at a time. The demo
/// code is 246810 and every action is local.
/// </summary>
public sealed class SecuritySettingsPage : ArcItemPage
{
    public override string ItemSlug => "security-settings";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The security tab of account settings with two factor setup, password change and sessions.",
        "Guided TOTP setup that walks through scan, verify and backup codes."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use settings-page for general account and workspace preferences.",
        "Use api-keys for developer tokens instead of account security."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Password errors are announced and tied to the field, and the live region reports saves, verification and session changes.",
        "The 2FA rail is a labelled step list; the code row takes digits and a paste, and focus returns to the neighbouring revoke button after a session is removed."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Two factor steps advance along a rail with a gliding marker, and sections expand with a height spring.",
        "Reduced motion uses instant transitions and skips the height animation."
    };

    public override string? ImplementationNotes =>
        "The setup key, QR block, backup codes and sessions are sample data, and verification compares against 246810. No secret is generated and no session is revoked on a server; the QR is a deterministic block pattern rather than an encoded secret.";

    private const string DemoCode = "246810";

    private static readonly string[] BackupCodes =
    {
        "k4mt-9x2p", "q7hd-3w8n", "z8rl-6v1c", "b2sn-5j7d", "h5wy-4k9r", "m3tc-8p2s", "v9xj-1d6h", "c6rg-7n4w"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var step = 1;
        var code = string.Empty;
        var verified = step == 2;
        var twoFactorOn = true;
        var sessions = new List<(string Device, string Place, string When)>
        {
            ("MacBook Pro · Chrome", "Lisbon, Portugal", "Active now"),
            ("iPhone 16 · Safari", "Lisbon, Portugal", "2 hours ago"),
            ("Windows desktop · Edge", "Berlin, Germany", "3 days ago")
        };
        var passwordError = string.Empty;
        var twoFactorHost = new ContentControl();
        var sessionHost = new ContentControl();

        Control QrBlock()
        {
            // A deterministic block pattern standing in for a QR code; it encodes nothing.
            var grid = new UniformGrid { Rows = 11, Columns = 11, Width = 96, Height = 96 };
            var seed = 0;
            for (var index = 0; index < 121; index++)
            {
                seed = (seed * 31 + 17) % 97;
                var filled = seed % 3 != 0 || index % 11 is 0 or 10 || index / 11 is 0 or 10;
                grid.Children.Add(new Border
                {
                    Background = filled ? AppKit.Brush(ArcTokens.Foreground) : Brushes.Transparent,
                    Margin = new Thickness(1)
                });
            }

            return new Border
            {
                Width = 112,
                Height = 112,
                Background = AppKit.Brush(ArcTokens.Surface),
                BorderBrush = AppKit.Brush(ArcTokens.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8),
                Child = grid
            };
        }

        void BuildTwoFactor()
        {
            var rail = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var labels = new[] { "Scan", "Verify", "Backup codes" };
            for (var index = 0; index < labels.Length; index++)
            {
                var done = index < step;
                var active = index == step;
                rail.Children.Add(new Border
                {
                    Padding = new Thickness(9, 5, 9, 5),
                    CornerRadius = new CornerRadius(999),
                    Background = active
                        ? AppKit.Tint(ArcTokens.Accent, 0.13)
                        : done ? AppKit.Tint(ArcTokens.Success, 0.1) : AppKit.Brush(ArcTokens.SurfaceMuted),
                    Child = AppKit.Row(5,
                        done
                            ? (Control)new ArcIcon { Icon = "check", Size = 11, Foreground = AppKit.Brush(ArcTokens.Success) }
                            : new TextBlock
                            {
                                Text = (index + 1).ToString(),
                                Classes = { "arc-caption" },
                                Foreground = active ? AppKit.Brush(ArcTokens.Accent) : AppKit.Brush(ArcTokens.TextMuted)
                            },
                        new TextBlock
                        {
                            Text = labels[index],
                            Classes = { "arc-caption" },
                            Foreground = active ? AppKit.Brush(ArcTokens.Accent) : AppKit.Brush(ArcTokens.TextMuted)
                        })
                });
            }

            Control body;
            if (step == 0)
            {
                body = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        AppKit.Body("Scan this with an authenticator app, or enter the setup key by hand.", 380),
                        AppKit.Row(14,
                            QrBlock(),
                            AppKit.Stack(8,
                                AppKit.Caption("Setup key"),
                                AppKit.Tile(AppKit.Row(8,
                                    AppKit.Mono("JBSW Y3DP EHPK 3PXP"),
                                    AppKit.IconBtn("copy", "Copy the setup key", () => AppKit.Say(status, "Setup key copied. Clipboard writes are simulated."), 24)), 8, 10, hairline: true),
                                AppKit.Caption("Time based, 30 second period, six digits."))),
                        AppKit.Btn("I have scanned it", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
                        {
                            step = 1;
                            Build();
                            AppKit.Say(status, "Step 2: enter the code from the app.");
                        }, trailingIcon: "arrow-right", height: 32)
                    }
                };
            }
            else if (step == 1)
            {
                var field = new OtpField(6, slotSize: 40, gap: 6, label: "Authentication code");
                field.SetDescription("Demo code 246810.");
                field.Completed += (_, value) =>
                {
                    if (value == DemoCode)
                    {
                        step = 2;
                        verified = true;
                        twoFactorOn = true;
                        Build();
                        AppKit.Say(status, "Two factor verified. Backup codes are ready.");
                    }
                };

                body = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        field,
                        AppKit.Row(8,
                            AppKit.Btn("Verify", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
                            {
                                if (field.Value == DemoCode)
                                {
                                    step = 2;
                                    verified = true;
                                    twoFactorOn = true;
                                    Build();
                                    AppKit.Say(status, "Two factor verified. Backup codes are ready.");
                                }
                                else
                                {
                                    field.Error = "That code is not right. Use 246810 in this preview.";
                                    AppKit.Say(status, "Verification failed.");
                                }
                            }, height: 34),
                            AppKit.Link("Back to the setup key", () =>
                            {
                                step = 0;
                                Build();
                            }))
                    }
                };
            }
            else
            {
                var codes = new UniformGrid { Columns = 2, Rows = 4 };
                foreach (var backup in BackupCodes)
                {
                    codes.Children.Add(new Border
                    {
                        Background = AppKit.Brush(ArcTokens.SurfaceMuted),
                        CornerRadius = new CornerRadius(7),
                        Padding = new Thickness(8, 5, 8, 5),
                        Margin = new Thickness(0, 0, 6, 6),
                        Child = AppKit.Mono(backup)
                    });
                }

                body = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        AppKit.Row(7,
                            new ArcIcon { Icon = "circle-check", Size = 14, Foreground = AppKit.Brush(ArcTokens.Success) },
                            new TextBlock { Text = "Two factor is on", Classes = { "arc-body-sm", "arc-medium" } }),
                        AppKit.Body("Save these codes somewhere safe. Each one signs you in once if you lose the authenticator.", 380),
                        codes,
                        AppKit.Row(6,
                            AppKit.Btn("Copy all", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => AppKit.Say(status, "Backup codes copied. Clipboard writes are simulated."), icon: "copy", height: 30),
                            AppKit.Btn("Download", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () => AppKit.Say(status, "Download is simulated."), icon: "download", height: 30),
                            AppKit.Link("Generate new codes", () => AppKit.Say(status, "New codes would invalidate the old set.")))
                    }
                };
            }

            twoFactorHost.Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            new StackPanel
                            {
                                Spacing = 1,
                                Children =
                                {
                                    new TextBlock { Text = "Two factor authentication", Classes = { "arc-body-sm", "arc-medium" } },
                                    AppKit.Caption("Authenticator app · recommended")
                                }
                            },
                            AppKit.Place(twoFactorOn
                                ? AppKit.Pill("On", ArcBadgeTone.Success)
                                : AppKit.Pill("Off", ArcBadgeTone.Neutral), 1)
                        }
                    },
                    rail,
                    AppKit.Divider(),
                    body
                }
            };
        }

        void BuildSessions()
        {
            var panel = new StackPanel { Spacing = 8 };
            foreach (var session in sessions.ToList())
            {
                var target = session;
                panel.Children.Add(new Border
                {
                    Padding = new Thickness(0, 8, 0, 8),
                    BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        ColumnSpacing = 9,
                        Children =
                        {
                            AppKit.Disc(target.Device.Contains("iPhone") ? "phone" : "cpu", 28, 13),
                            AppKit.Place(new StackPanel
                            {
                                Spacing = 1,
                                Children =
                                {
                                    new TextBlock { Text = target.Device, Classes = { "arc-body-sm" } },
                                    AppKit.Caption($"{target.Place} · {target.When}")
                                }
                            }, 1),
                            AppKit.Place(AppKit.Btn("Revoke", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () =>
                            {
                                sessions.Remove(target);
                                Build();
                                AppKit.Say(status, $"{target.Device} revoked. Focus moves to the next row.");
                            }, height: 28), 2)
                        }
                    }
                });
            }

            if (sessions.Count == 0)
            {
                panel.Children.Add(AppKit.Empty("shield-check", "No other sessions", "Only this device is signed in."));
            }

            panel.Children.Add(AppKit.Btn("Sign out everywhere else", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
            {
                sessions.Clear();
                Build();
                AppKit.Say(status, "Every other session signed out. This is simulated.");
            }, icon: "log-out", height: 32));

            sessionHost.Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "Active sessions", Classes = { "arc-body-sm", "arc-medium" } },
                    AppKit.Caption("Devices signed in to your account right now."),
                    panel
                }
            };
        }

        void Build()
        {
            BuildTwoFactor();
            BuildSessions();
        }

        Build();

        var passwordCard = AppKit.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Change password", Classes = { "arc-body-sm", "arc-medium" } },
                AppKit.Columns("*,*",
                    AppKit.Field("Current password", new ArcPasswordField { Placeholder = "Current password" }),
                    AppKit.Field("New password", new ArcPasswordStrength { Placeholder = "New password" })),
                string.IsNullOrEmpty(passwordError) ? AppKit.Caption("At least 12 characters is recommended.") : AppKit.FieldError(passwordError),
                AppKit.Btn("Update password", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
                {
                    passwordError = string.Empty;
                    Build();
                    AppKit.Say(status, "Password updated. The request is simulated.");
                }, height: 34)
            }
        }, 14);

        var left = new StackPanel
        {
            Spacing = 12,
            Children = { AppKit.Card(twoFactorHost, 14), passwordCard, status }
        };

        var right = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                AppKit.Card(sessionHost, 14),
                AppKit.Tile(AppKit.Stack(6,
                    AppKit.Meta("info", "Recovery"),
                    AppKit.Caption("If you lose every device, support can verify your identity with a recovery code.")), 12, 12, hairline: true)
            }
        };

        var columns = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,360"),
            ColumnSpacing = 14
        };
        columns.Children.Add(left);
        Grid.SetColumn(right, 1);
        columns.Children.Add(right);

        return AppKit.Screen(new Border
        {
            Padding = new Thickness(18, 14, 18, 12),
            Child = new Border { Child = columns }
        }, 570);
    }
}
