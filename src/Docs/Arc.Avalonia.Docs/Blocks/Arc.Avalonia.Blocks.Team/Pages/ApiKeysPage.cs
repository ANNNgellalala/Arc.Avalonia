using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Team.Pages;

/// <summary>
/// API keys: key management with scopes and environments, a secret shown once, per-key reveal and copy, and a
/// two step revoke. Create key unfolds into a short form; the rest of the surface stays visible. Keys and
/// secrets are simulated and never leave memory.
/// </summary>
public sealed class ApiKeysPage : ArcItemPage
{
    private sealed class ApiKey
    {
        public string Name { get; set; } = string.Empty;
        public string Environment { get; set; } = "Production";
        public string Secret { get; set; } = string.Empty;
        public string Scopes { get; set; } = "Read";
        public string LastUsed { get; set; } = "Never";
        public string Owner { get; set; } = "Maya Chen";
        public bool Revealed { get; set; }
        public bool ConfirmRevoke { get; set; }
    }

    public override string ItemSlug => "api-keys";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-key-list", "state-revealed-secret", "state-masked-secret", "variant-environment-badge"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The developer settings page of an API product.",
        "Key management with scopes, environments and a one-time secret reveal."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use webhooks for event endpoints and signing secrets, or roles-permissions for user access."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Escape closes the create form and returns focus to the Create key button.",
        "Reveal and copy are labelled with the key name, and creation, copy and revoke results are announced."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Create key unfolds into its form and a new key slides into the list; the one-time secret banner fades in.",
        "Reduced motion replaces the unfold with an instant swap and skips the slide."
    };

    public override string? ImplementationNotes =>
        "Keys and secrets are simulated sample data; a real deployment creates keys on the server and passes the " +
        "secret back once. Hold-to-revoke is approximated with a two step button, which needs no pointer hold while " +
        "keeping the stray-click protection. Scopes are shown but not enforced. No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var keys = new List<ApiKey>
        {
            new()
            {
                Name = "CI pipeline",
                Environment = "Production",
                Secret = "arc_live_8f3c2a91d4b7",
                Scopes = "Read, Write",
                LastUsed = "Never",
                Owner = "Priya Raman",
                Revealed = true
            },
            new()
            {
                Name = "Support console",
                Environment = "Production",
                Secret = "arc_live_2b71e0c9a5f4",
                Scopes = "Read",
                LastUsed = "3 hours ago",
                Owner = "Lena Fischer"
            },
            new()
            {
                Name = "Local scripts",
                Environment = "Development",
                Secret = "arc_test_1d9a4c7e2b6f",
                Scopes = "Read, Write",
                LastUsed = "Yesterday",
                Owner = "Sam Okafor"
            }
        };

        var keyHost = new StackPanel { Spacing = 8 };
        var formHost = new Border { IsVisible = false };
        var bannerHost = new Border
        {
            IsVisible = false,
            Background = TeamKit.Tint(ArcTokens.Accent, 0.09),
            BorderBrush = TeamKit.Tint(ArcTokens.Accent, 0.28),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12, 9)
        };
        var status = TeamKit.Caption("Keys are scoped to this workspace. Secrets cannot be recovered after creation.");

        var formName = TeamKit.Input("Key name, for example CI pipeline", width: 260);
        var formEnv = "Production";
        var formScopes = "Read";

        void RenderBanner(string name, string secret)
        {
            bannerHost.IsVisible = true;
            var copy = new ArcCopyButton
            {
                Value = secret,
                Label = "Copy secret",
                CopiedLabel = "Copied",
                Variant = ArcButtonVariant.Secondary,
                Size = ArcButtonSize.Sm
            };
            var dismiss = TeamKit.Btn("Done", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () =>
            {
                bannerHost.IsVisible = false;
            });

            bannerHost.Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
                Children =
                {
                    new ArcIcon
                    {
                        Icon = "key-round",
                        Size = 18,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = TeamKit.Brush(ArcTokens.AccentStrong)
                    },
                    new StackPanel
                    {
                        Spacing = 2,
                        Margin = new Thickness(10, 0, 12, 0),
                        Children =
                        {
                            new TextBlock { Text = $"{name} created. Copy the secret now.", Classes = { "arc-label" } },
                            TeamKit.Mono(secret)
                        }
                    },
                    copy,
                    dismiss
                }
            };
            if (bannerHost.Child is Grid built)
            {
                Grid.SetColumn(built.Children[1], 1);
                Grid.SetColumn(built.Children[2], 2);
                Grid.SetColumn(built.Children[3], 3);
            }
        }

        void RenderKeys()
        {
            keyHost.Children.Clear();
            foreach (var key in keys.ToList())
            {
                var row = new Border
                {
                    Background = TeamKit.Brush(ArcTokens.Surface),
                    BorderBrush = TeamKit.Brush(ArcTokens.Border),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(14, 10),
                    Child = BuildKey(key, () =>
                    {
                        key.Revealed = !key.Revealed;
                        RenderKeys();
                        status.Text = key.Revealed ? $"{key.Name} secret revealed." : $"{key.Name} secret hidden.";
                    }, () =>
                    {
                        if (!key.ConfirmRevoke)
                        {
                            key.ConfirmRevoke = true;
                            RenderKeys();
                            status.Text = $"Press Revoke again to permanently revoke {key.Name}.";
                        }
                        else
                        {
                            keys.Remove(key);
                            RenderKeys();
                            status.Text = $"{key.Name} revoked. Requests using it now fail.";
                        }
                    })
                };
                keyHost.Children.Add(row);
            }

            if (keys.Count == 0)
            {
                keyHost.Children.Add(TeamKit.Panel(TeamKit.Stack(6,
                    TeamKit.Label("No API keys"),
                    TeamKit.Body("Create a key to start calling the API.")), 18, 18, true));
            }
        }

        void RenderForm()
        {
            var scopes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            foreach (var scope in new[] { "Read", "Write", "Admin" })
            {
                var value = scope;
                var check = TeamKit.Check(value, formScopes.Contains(value, StringComparison.Ordinal), on =>
                {
                    formScopes = on ? value : "Read";
                    status.Text = $"Scopes for the new key: {formScopes}.";
                });
                scopes.Children.Add(check);
            }

            var create = TeamKit.Btn("Create key", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
            {
                var name = string.IsNullOrWhiteSpace(formName.Value) ? "Untitled key" : formName.Value!;
                var secret = formEnv == "Production"
                    ? $"arc_live_{Guid.NewGuid():N}"[..21]
                    : $"arc_test_{Guid.NewGuid():N}"[..21];
                keys.Insert(0, new ApiKey
                {
                    Name = name,
                    Environment = formEnv,
                    Secret = secret,
                    Scopes = formScopes == "Admin" ? "Read, Write, Admin" : formScopes,
                    LastUsed = "Never",
                    Owner = "Maya Chen",
                    Revealed = true
                });
                formHost.IsVisible = false;
                formName.Value = string.Empty;
                RenderKeys();
                RenderBanner(name, secret);
                status.Text = "Key created. The secret is shown once.";
            });

            var cancel = TeamKit.Btn("Cancel", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () =>
            {
                formHost.IsVisible = false;
                status.Text = "Key creation cancelled.";
            });

            var env = TeamKit.Segmented(new[] { ("Production", "Production"), ("Development", "Development") },
                formEnv, value => formEnv = value, "Environment");

            var content = TeamKit.Stack(12,
                new TextBlock { Text = "Create an API key", Classes = { "arc-label" } },
                TeamKit.Row(12, TeamKit.Field("Name", formName, 260), TeamKit.Field("Environment", env)),
                TeamKit.Stack(6, TeamKit.Label("Scopes"), scopes),
                TeamKit.Row(8, create, cancel),
                TeamKit.Caption("The secret appears once. Store it in your password manager or deploy settings."));

            formHost.Child = TeamKit.Panel(content, 14, 16, true);
        }

        var createButton = TeamKit.Btn("Create key", ArcButtonVariant.Primary, ArcButtonSize.Sm, icon: "plus");
        createButton.Click += (_, _) =>
        {
            formHost.IsVisible = true;
            status.Text = "Name the key, choose an environment, then pick its scopes.";
        };

        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = "API keys", Classes = { "arc-heading" } },
                TeamKit.Caption("Workspace keys · 3 active · rotated every 90 days")
            }
        });
        Grid.SetColumn(createButton, 1);
        heading.Children.Add(createButton);

        RenderKeys();
        RenderForm();
        RenderBanner("CI pipeline", keys[0].Secret);

        return TeamKit.Root(new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Padding = new Thickness(22),
            Child = TeamKit.Stack(14,
                heading,
                bannerHost,
                formHost,
                TeamKit.Row(10,
                    TeamKit.Pill("3 active", ArcBadgeTone.Success, dot: true),
                    TeamKit.Caption("90 day rotation · last audit today")),
                keyHost,
                status)
        });
    }

    private static string Mask(string secret) =>
        secret.Length <= 14 ? "••••••••••••" : $"{secret[..9]}••••••{secret[^4..]}";

    private static Control BuildKey(ApiKey key, Action onReveal, Action onRevoke)
    {
        var envTone = key.Environment == "Production" ? ArcBadgeTone.Accent : ArcBadgeTone.Neutral;
        var identity = new StackPanel { Spacing = 2 };
        var titleRow = TeamKit.Row(8,
            new TextBlock { Text = key.Name, Classes = { "arc-label" } },
            TeamKit.Pill(key.Environment, envTone),
            TeamKit.Pill(key.Scopes));
        identity.Children.Add(titleRow);
        identity.Children.Add(TeamKit.Caption($"Owner {key.Owner} · last used {key.LastUsed}"));

        var secret = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        secret.Children.Add(TeamKit.Mono(key.Revealed ? key.Secret : Mask(key.Secret)));
        var reveal = TeamKit.IconBtn(key.Revealed ? "eye-off" : "eye",
            key.Revealed ? $"Hide {key.Name} secret" : $"Reveal {key.Name} secret", onReveal, box: 26);
        secret.Children.Add(reveal);
        var copy = new ArcCopyButton
        {
            Value = key.Secret,
            Label = "Copy",
            CopiedLabel = "Copied",
            IconOnly = true,
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(copy, $"Copy {key.Name} secret");
        secret.Children.Add(copy);

        var right = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var revoke = TeamKit.Btn(key.ConfirmRevoke ? "Confirm revoke" : "Revoke",
            key.ConfirmRevoke ? ArcButtonVariant.Danger : ArcButtonVariant.Ghost, ArcButtonSize.Sm, onRevoke);
        right.Children.Add(revoke);
        right.Children.Add(TeamKit.Caption(key.ConfirmRevoke ? "Second press revokes" : "Two step revoke"));

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var left = new StackPanel { Spacing = 7, Children = { identity, secret } };
        grid.Children.Add(left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }
}
