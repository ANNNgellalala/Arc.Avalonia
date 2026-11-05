using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
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
/// Environment variables: keys scoped per environment with masked values, write-only sensitive rows, search,
/// .env paste import with duplicate and invalid-key warnings, an environment diff by fingerprint, and a
/// redeploy banner that appears while changes are queued. All values are sample data in memory.
/// </summary>
public sealed class EnvVariablesPage : ArcItemPage
{
    private static readonly string[] Environments = { "Development", "Preview", "Production" };

    private static readonly string SampleDotenv =
        "# paste a whole file\nNEXT_PUBLIC_APP_URL=https://northwind.co\n1INVALID=oops\nDATABASE_URL=postgres://new-host/app\nFEATURE_FLAGS=checkout,sso";

    private sealed class EnvVar
    {
        public string Key { get; init; } = string.Empty;
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Scopes { get; } = new(StringComparer.Ordinal);
        public bool Sensitive { get; init; }
        public bool Revealed { get; set; }
        public string Updated { get; init; } = "2 hours ago";
        public string UpdatedBy { get; init; } = "Priya Raman";
    }

    public override string ItemSlug => "env-variables";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-variable-rows", "state-masked-value", "state-sensitive-writeonly", "state-redeploy-banner"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A settings screen where developers manage configuration and secrets across environments.",
        "Onboarding a project by pasting an existing .env file and scoping it in one step."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Reveal and environment toggles are buttons labelled with their key, and masked values are hidden from screen readers.",
        "The .env parse summary updates live and every import, toggle and redeploy result is announced."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The redeploy banner, import panel and row editor open with height and opacity; masked and revealed values crossfade in a fixed slot.",
        "Reduced motion lands every change at once."
    };

    public override string? ImplementationNotes =>
        "Values, comparisons and deploy state are simulated in memory; Redeploy only acknowledges the queue. " +
        "Write-only sensitive values have no reveal, matching the documented behaviour, and the .env parser handles " +
        "comments, export prefixes and duplicates. No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var variables = new List<EnvVar>
        {
            new()
            {
                Key = "DATABASE_URL",
                Sensitive = true,
                UpdatedBy = "Priya Raman",
                Updated = "2 hours ago"
            },
            new()
            {
                Key = "STRIPE_SECRET_KEY",
                Sensitive = true,
                UpdatedBy = "Lena Fischer",
                Updated = "Yesterday"
            },
            new()
            {
                Key = "NEXT_PUBLIC_APP_URL",
                UpdatedBy = "Maya Chen",
                Updated = "3 days ago"
            },
            new()
            {
                Key = "FEATURE_FLAGS",
                UpdatedBy = "Sam Okafor",
                Updated = "Last week"
            },
            new()
            {
                Key = "LOG_LEVEL",
                UpdatedBy = "Jonas Weber",
                Updated = "Last week"
            }
        };

        void SetValue(EnvVar variable, string environment, string value)
        {
            variable.Values[environment] = value;
            variable.Scopes.Add(environment);
        }

        var db = variables[0];
        SetValue(db, "Development", "postgres://localhost/northwind");
        SetValue(db, "Preview", "postgres://preview-db.northwind.co/app");
        SetValue(db, "Production", "postgres://prod-db.northwind.co/app");

        var stripe = variables[1];
        SetValue(stripe, "Preview", "sk_test_51Nz8");
        SetValue(stripe, "Production", "sk_live_51Nz8");

        var url = variables[2];
        SetValue(url, "Development", "http://localhost:3000");
        SetValue(url, "Preview", "https://preview.northwind.co");
        SetValue(url, "Production", "https://northwind.co");

        var flags = variables[3];
        SetValue(flags, "Development", "checkout,sso");
        SetValue(flags, "Preview", "checkout");

        var log = variables[4];
        SetValue(log, "Development", "debug");
        SetValue(log, "Preview", "info");
        SetValue(log, "Production", "warn");

        var environment = "Production";
        var view = "variables";
        var query = string.Empty;
        var queued = 3;
        var compareLeft = "Preview";
        var compareRight = "Production";
        var status = TeamKit.Caption("Values are masked until revealed. Sensitive values are write-only.");

        var rowHost = new StackPanel { Spacing = 4 };
        var bannerHost = new Border
        {
            Background = TeamKit.Tint(ArcTokens.Warning, 0.09),
            BorderBrush = TeamKit.Tint(ArcTokens.Warning, 0.24),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12, 8)
        };
        var importHost = new Border { IsVisible = false };
        var addHost = new Border { IsVisible = false };
        var compareHost = new Border { IsVisible = false };
        var viewHost = new Border();

        void RenderBanner()
        {
            bannerHost.IsVisible = queued > 0;
            if (queued == 0)
            {
                return;
            }

            var redeploy = TeamKit.Btn("Redeploy", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
            {
                queued = 0;
                RenderBanner();
                status.Text = "Redeploy queued. Production picks up the new values in about a minute.";
                status.Foreground = TeamKit.Brush(ArcTokens.Success);
            });

            bannerHost.Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    TeamKit.Row(10,
                        new ArcIcon
                        {
                            Icon = "triangle-alert",
                            Size = 16,
                            Foreground = TeamKit.Brush(ArcTokens.Warning),
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = $"{queued} changes not deployed in Production",
                            Classes = { "arc-label" }
                        }),
                    redeploy
                }
            };
            if (bannerHost.Child is Grid grid)
            {
                Grid.SetColumn(grid.Children[1], 1);
            }
        }

        string Fingerprint(string value)
        {
            uint hash = 2166136261;
            foreach (var character in value)
            {
                hash = (hash ^ character) * 16777619;
            }

            return hash.ToString("x8")[..6].ToUpperInvariant();
        }

        void RenderCompare()
        {
            var keys = variables.Select(variable => variable.Key).ToList();
            var table = new StackPanel { Spacing = 4 };
            var header = new Grid { ColumnDefinitions = new ColumnDefinitions("240,*,*,120") };
            header.Children.Add(TeamKit.Caption("Key"));
            var left = TeamKit.Caption(compareLeft);
            Grid.SetColumn(left, 1);
            header.Children.Add(left);
            var right = TeamKit.Caption(compareRight);
            Grid.SetColumn(right, 2);
            header.Children.Add(right);
            var diff = TeamKit.Caption("Status");
            Grid.SetColumn(diff, 3);
            header.Children.Add(diff);
            table.Children.Add(header);

            foreach (var key in keys)
            {
                var variable = variables.First(entry => entry.Key == key);
                var hasLeft = variable.Scopes.Contains(compareLeft);
                var hasRight = variable.Scopes.Contains(compareRight);
                var leftFp = hasLeft ? Fingerprint(variable.Values[compareLeft]) : "—";
                var rightFp = hasRight ? Fingerprint(variable.Values[compareRight]) : "—";
                var state = !hasLeft && !hasRight ? "Missing" : !hasLeft || !hasRight ? "Partial" : leftFp == rightFp ? "Match" : "Different";

                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("240,*,*,120") };
                row.Children.Add(new TextBlock { Text = key, Classes = { "arc-mono" }, VerticalAlignment = VerticalAlignment.Center });
                var first = TeamKit.Mono(leftFp);
                Grid.SetColumn(first, 1);
                row.Children.Add(first);
                var second = TeamKit.Mono(rightFp);
                Grid.SetColumn(second, 2);
                row.Children.Add(second);
                var badge = TeamKit.Pill(state, state switch
                {
                    "Match" => ArcBadgeTone.Success,
                    "Different" => ArcBadgeTone.Warning,
                    "Partial" => ArcBadgeTone.Warning,
                    _ => ArcBadgeTone.Danger
                });
                Grid.SetColumn(badge, 3);
                row.Children.Add(badge);

                table.Children.Add(new Border
                {
                    Padding = new Thickness(8, 6),
                    CornerRadius = new CornerRadius(10),
                    Background = TeamKit.Brush(ArcTokens.SurfaceMuted),
                    Child = row
                });
            }

            var envSelects = TeamKit.Row(10,
                TeamKit.Select(Environments.Select(e => (e, e)), compareLeft, value =>
                {
                    compareLeft = value;
                    RenderCompare();
                }, 140),
                new ArcIcon { Icon = "chevron-right", Size = 14, Foreground = TeamKit.Brush(ArcTokens.TextMuted) },
                TeamKit.Select(Environments.Select(e => (e, e)), compareRight, value =>
                {
                    compareRight = value;
                    RenderCompare();
                }, 140));

            compareHost.Child = TeamKit.Stack(12,
                new TextBlock { Text = "Compare environments", Classes = { "arc-label" } },
                TeamKit.Caption("Fingerprints are six characters of each value, so drift can be checked without exposing secrets."),
                envSelects,
                table);
            compareHost.IsVisible = true;
        }

        void RenderRows()
        {
            rowHost.Children.Clear();
            var matches = variables.Where(variable =>
                query.Length == 0 || variable.Key.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var variable in matches)
            {
                rowHost.Children.Add(BuildRow(variable));
            }

            if (matches.Count == 0)
            {
                rowHost.Children.Add(TeamKit.Panel(TeamKit.Stack(5,
                    TeamKit.Label("No variable matches"),
                    TeamKit.Body("Search matches key names only, never values.")), 16, 14, true));
            }
        }

        Control BuildRow(EnvVar variable)
        {
            var hasValue = variable.Scopes.Contains(environment);
            var value = hasValue ? variable.Values[environment] : string.Empty;
            var shown = variable.Sensitive
                ? "Sensitive value · write-only"
                : variable.Revealed
                    ? value
                    : hasValue ? "••••••••••••" : "Not set in this environment";

            var identity = new StackPanel { Spacing = 3 };
            var keyRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
            keyRow.Children.Add(new TextBlock { Text = variable.Key, Classes = { "arc-label" } });
            if (variable.Sensitive)
            {
                var sensitive = TeamKit.Pill("Sensitive", ArcBadgeTone.Warning);
                sensitive.Margin = new Thickness(8, 0, 0, 0);
                Grid.SetColumn(sensitive, 1);
                keyRow.Children.Add(sensitive);
            }

            var updated = TeamKit.Caption($"{variable.UpdatedBy.Split(' ')[0]} · {variable.Updated.ToLowerInvariant()}");
            updated.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(updated, 3);
            keyRow.Children.Add(updated);

            identity.Children.Add(keyRow);

            var valueHost = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            var valueText = TeamKit.Mono(shown);
            valueText.VerticalAlignment = VerticalAlignment.Center;
            valueHost.Children.Add(valueText);
            if (hasValue && !variable.Sensitive)
            {
                var eye = TeamKit.IconBtn(variable.Revealed ? "eye-off" : "eye",
                    variable.Revealed ? $"Hide {variable.Key}" : $"Reveal {variable.Key}", () =>
                    {
                        variable.Revealed = !variable.Revealed;
                        RenderRows();
                    }, box: 24);
                valueHost.Children.Add(eye);
            }

            if (hasValue && !variable.Sensitive)
            {
                var copy = new ArcCopyButton
                {
                    Value = value,
                    Label = "Copy",
                    CopiedLabel = "Copied",
                    IconOnly = true,
                    Variant = ArcButtonVariant.Ghost,
                    Size = ArcButtonSize.Sm,
                    VerticalAlignment = VerticalAlignment.Center
                };
                AutomationProperties.SetName(copy, $"Copy {variable.Key} value");
                valueHost.Children.Add(copy);
            }

            var scopes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            foreach (var env in Environments)
            {
                var target = env;
                var on = variable.Scopes.Contains(env);
                var pill = TeamKit.ScopePill(env switch
                {
                    "Development" => "Dev",
                    "Preview" => "Prev",
                    _ => "Prod"
                }, on, () =>
                {
                    if (on)
                    {
                        variable.Scopes.Remove(target);
                    }
                    else
                    {
                        variable.Scopes.Add(target);
                    }

                    queued++;
                    RenderBanner();
                    RenderRows();
                    status.Text = $"{variable.Key}: {target} {(on ? "scope removed" : "scope added")}.";
                    status.Foreground = TeamKit.Brush(ArcTokens.TextMuted);
                });
                pill.Width = 58;
                scopes.Children.Add(pill);
            }

            var edit = TeamKit.IconBtn("pencil", $"Edit {variable.Key}", () =>
            {
                status.Text = $"Editing {variable.Key}. Saving replaces the value for {environment}.";
                status.Foreground = TeamKit.Brush(ArcTokens.TextMuted);
            }, box: 28);

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,184,36") };
            row.Children.Add(new StackPanel { Spacing = 4, Children = { identity, valueHost } });
            var scopeHost = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Children = { scopes } };
            Grid.SetColumn(scopeHost, 1);
            row.Children.Add(scopeHost);
            Grid.SetColumn(edit, 2);
            row.Children.Add(edit);

            return new Border
            {
                Background = TeamKit.Brush(ArcTokens.Surface),
                BorderBrush = TeamKit.Brush(ArcTokens.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 5),
                Child = row
            };
        }

        void RenderView()
        {
            viewHost.Child = view == "compare" ? compareHost : rowHost;
            viewHost.IsVisible = true;
        }

        var envSwitch = TeamKit.Segmented(Environments.Select(e => (e, e)), "Production", value =>
        {
            environment = value;
            RenderRows();
            status.Text = $"Showing values that apply to {environment}.";
            status.Foreground = TeamKit.Brush(ArcTokens.TextMuted);
        }, "Environment");

        var viewSwitch = TeamKit.Segmented(new[] { ("variables", "Variables"), ("compare", "Compare") }, "variables",
            value =>
            {
                view = value;
                if (view == "compare")
                {
                    RenderCompare();
                }

                RenderView();
            }, "View");

        var search = TeamKit.Search("Search keys", width: 220);
        search.ValueChanged += (_, _) =>
        {
            query = search.Value ?? string.Empty;
            RenderRows();
        };

        var add = TeamKit.Btn("Add variable", ArcButtonVariant.Secondary, ArcButtonSize.Sm, icon: "plus");
        add.Click += (_, _) =>
        {
            addHost.IsVisible = !addHost.IsVisible;
        };

        var import = TeamKit.Btn("Import .env", ArcButtonVariant.Secondary, ArcButtonSize.Sm, icon: "upload");
        import.Click += (_, _) =>
        {
            importHost.IsVisible = !importHost.IsVisible;
        };

        var addKey = TeamKit.Input("KEY_NAME", width: 220);
        var addValue = TeamKit.Input("value", width: 280);
        var addSensitive = TeamKit.Check("Sensitive, write-only", false);
        var addPanelContent = TeamKit.Stack(10,
            new TextBlock { Text = "Add variable", Classes = { "arc-label" } },
            TeamKit.Row(10, TeamKit.Field("Key", addKey, 220), TeamKit.Field("Value", addValue, 280)),
            TeamKit.Row(10, addSensitive, TeamKit.Caption($"Scope: {environment}")),
            TeamKit.Row(8,
                TeamKit.Btn("Add", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
                {
                    var key = (addKey.Value ?? string.Empty).Trim();
                    if (!IsValidKey(key) || variables.Any(entry => entry.Key == key))
                    {
                        status.Text = !IsValidKey(key)
                            ? $"\"{key}\" is not a valid key. Use letters, digits and underscores, starting with a letter."
                            : $"{key} already exists. Edit it instead.";
                        status.Foreground = TeamKit.Brush(ArcTokens.Danger);
                        return;
                    }

                    var variable = new EnvVar
                    {
                        Key = key,
                        Sensitive = addSensitive.Checked == true,
                        UpdatedBy = "You",
                        Updated = "Just now"
                    };
                    variable.Values[environment] = addValue.Value ?? string.Empty;
                    variable.Scopes.Add(environment);
                    variables.Insert(0, variable);
                    queued++;
                    addKey.Value = string.Empty;
                    addValue.Value = string.Empty;
                    addHost.IsVisible = false;
                    RenderRows();
                    RenderBanner();
                    status.Text = $"{key} added to {environment}.";
                    status.Foreground = TeamKit.Brush(ArcTokens.Success);
                }),
                TeamKit.Btn("Cancel", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () => addHost.IsVisible = false)));
        addHost.Child = TeamKit.Panel(addPanelContent, 14, 16, true);
        addHost.IsVisible = false;

        var paste = TeamKit.Textarea("Paste a .env file", SampleDotenv, 76);
        var importSummary = TeamKit.Caption("Nothing imported yet.");
        var importPanelContent = TeamKit.Stack(8,
            TeamKit.Row(10, new TextBlock { Text = "Import .env", Classes = { "arc-label" } },
                TeamKit.Caption($"Imports into {environment}")),
            paste,
            TeamKit.Row(8,
                TeamKit.Btn("Import lines", ArcButtonVariant.Primary, ArcButtonSize.Sm, icon: "upload", onClick: () =>
                {
                    var added = 0;
                    var duplicates = 0;
                    var invalid = 0;
                    foreach (var raw in (paste.Value ?? string.Empty).Split('\n'))
                    {
                        var line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith('#'))
                        {
                            continue;
                        }

                        if (line.StartsWith("export ", StringComparison.Ordinal))
                        {
                            line = line[7..].Trim();
                        }

                        var split = line.IndexOf('=');
                        if (split <= 0)
                        {
                            invalid++;
                            continue;
                        }

                        var key = line[..split].Trim();
                        var value = line[(split + 1)..].Trim().Trim('"');
                        if (!IsValidKey(key))
                        {
                            invalid++;
                            continue;
                        }

                        var existing = variables.FirstOrDefault(entry => entry.Key == key);
                        if (existing is not null)
                        {
                            if (existing.Values.ContainsKey(environment))
                            {
                                duplicates++;
                                continue;
                            }

                            existing.Values[environment] = value;
                            existing.Scopes.Add(environment);
                        }
                        else
                        {
                            var variable = new EnvVar { Key = key, UpdatedBy = "You", Updated = "Just now" };
                            variable.Values[environment] = value;
                            variable.Scopes.Add(environment);
                            variables.Insert(0, variable);
                        }

                        added++;
                    }

                    queued += added;
                    importSummary.Text = $"{added} imported · {duplicates} duplicate{(duplicates == 1 ? string.Empty : "s")} skipped · {invalid} invalid";
                    importSummary.Foreground = TeamKit.Brush(invalid > 0 ? ArcTokens.Warning : ArcTokens.Success);
                    RenderRows();
                    RenderBanner();
                    status.Text = "Import finished. Review the scope of each new key before redeploying.";
                    status.Foreground = TeamKit.Brush(ArcTokens.TextMuted);
                }),
                TeamKit.Btn("Done", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () => importHost.IsVisible = false)),
            importSummary);
        importHost.Child = TeamKit.Panel(importPanelContent, 14, 16, true);

        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = "Environment variables", Classes = { "arc-heading" } },
                TeamKit.Mono("northwind-storefront / settings / environment")
            }
        });
        var actions = TeamKit.Row(8, import, add);
        Grid.SetColumn(actions, 1);
        heading.Children.Add(actions);

        RenderBanner();
        RenderRows();
        RenderView();

        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        toolbar.Children.Add(envSwitch);
        var searchHolder = new Border { Child = search, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 0) };
        Grid.SetColumn(searchHolder, 1);
        toolbar.Children.Add(searchHolder);
        Grid.SetColumn(viewSwitch, 2);
        toolbar.Children.Add(viewSwitch);

        return TeamKit.Root(new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Padding = new Thickness(20),
            Child = TeamKit.Stack(10,
                heading,
                toolbar,
                bannerHost,
                addHost,
                importHost,
                viewHost,
                status)
        });
    }

    private static bool IsValidKey(string key)
    {
        if (key.Length == 0 || !char.IsLetter(key[0]))
        {
            return false;
        }

        return key.All(character => char.IsLetterOrDigit(character) || character is '_' or '.');
    }
}

