using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Team.Pages;

/// <summary>
/// Roles and permissions: a permissions matrix with role columns, collapsible permission groups, per-role group
/// toggles, role duplication and a review bar for unsaved changes. The owner column is locked. Every cell is a
/// real keyboard-reachable toggle and the review bar appears while the matrix differs from the saved snapshot.
/// </summary>
public sealed class RolesPermissionsPage : ArcItemPage
{
    private sealed record PermissionGroup(string Name, string[] Permissions);

    private static readonly PermissionGroup[] Groups =
    {
        new("Workspace", new[] { "Invite members", "Manage billing", "Edit workspace" }),
        new("Projects", new[] { "Create projects", "Delete projects", "Export data" }),
        new("Billing", new[] { "View invoices", "Manage plan" })
    };

    public override string ItemSlug => "roles-permissions";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-permissions-matrix", "state-locked-owner", "state-mixed-group", "state-unsaved-review"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Workspace settings where admins edit what each role can do.",
        "Permission matrices with grouped permissions and a review step before saving."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use team-members to assign roles to people, and switch rows for a few on and off settings."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Cells are labelled switches with their role and permission, and the owner column reports locked cells.",
        "Group toggles report a mixed state when only some permissions in the group are granted."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "A group toggle ripples across its permissions with a short stagger; the review bar rises while changes exist.",
        "Reduced motion removes the stagger and the rise."
    };

    public override string? ImplementationNotes =>
        "Roles, groups and grants are sample data in memory; Save only marks the current matrix as saved. Role " +
        "headers stay fixed and the matrix scrolls sideways in the reference at narrow widths; this preview is " +
        "shown at gallery width. No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var roles = new List<string> { "Admin", "Editor", "Viewer" };
        var grants = new Dictionary<(string Role, string Permission), bool>();
        foreach (var group in Groups)
        {
            foreach (var permission in group.Permissions)
            {
                grants[("Admin", permission)] = true;
                grants[("Editor", permission)] = permission is "Invite members" or "Create projects" or "Export data";
                grants[("Viewer", permission)] = permission is "View invoices";
            }
        }

        var saved = new Dictionary<(string Role, string Permission), bool>(grants);

        // Two edits are already staged, so the review bar has something honest to report.
        grants[("Editor", "Manage billing")] = true;
        grants[("Viewer", "Export data")] = true;

        var matrixHost = new Grid();
        var reviewHost = new Border { IsVisible = false };
        var status = TeamKit.Caption("Two changes from the last saved review.");

        int CountChanges()
        {
            var count = 0;
            foreach (var pair in grants)
            {
                if (!saved.TryGetValue(pair.Key, out var value) || value != pair.Value)
                {
                    count++;
                }
            }

            return count;
        }

        void RenderReview()
        {
            var changes = CountChanges();
            reviewHost.IsVisible = changes > 0;
            if (changes == 0)
            {
                return;
            }

            var discard = TeamKit.Btn("Discard", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () =>
            {
                foreach (var pair in saved)
                {
                    grants[pair.Key] = pair.Value;
                }

                RenderMatrix();
                RenderReview();
                status.Text = "Changes discarded.";
                status.Foreground = TeamKit.Brush(ArcTokens.TextMuted);
            });

            var save = TeamKit.Btn("Save changes", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
            {
                saved = new Dictionary<(string Role, string Permission), bool>(grants);
                RenderMatrix();
                RenderReview();
                status.Text = "Permissions saved. New requests use them immediately.";
                status.Foreground = TeamKit.Brush(ArcTokens.Success);
            });

            reviewHost.Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    TeamKit.Row(10,
                        TeamKit.Pill($"{changes} unsaved", ArcBadgeTone.Warning, dot: true),
                        TeamKit.Caption("Review the matrix before applying it to the workspace.")),
                    TeamKit.Row(8, discard, save)
                }
            };
            if (reviewHost.Child is Grid grid)
            {
                Grid.SetColumn(grid.Children[1], 1);
            }
        }

        void RenderMatrix()
        {
            var columnCount = 1 + roles.Count;
            var definition = new System.Text.StringBuilder("360");
            for (var index = 0; index < roles.Count; index++)
            {
                definition.Append(",140");
            }

            matrixHost.ColumnDefinitions = new ColumnDefinitions(definition.ToString());
            matrixHost.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", 1 + 1 + Groups.Length + Groups.Sum(group => group.Permissions.Length))));
            matrixHost.Children.Clear();

            // Role headers.
            var headerRow = 1;
            for (var roleIndex = 0; roleIndex < roles.Count; roleIndex++)
            {
                var role = roles[roleIndex];
                var header = new StackPanel
                {
                    Spacing = 1,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 6)
                };
                header.Children.Add(new TextBlock { Text = role, Classes = { "arc-label" }, HorizontalAlignment = HorizontalAlignment.Center });
                header.Children.Add(TeamKit.Caption(role == "Admin" ? "Owner · locked" : $"{MembersFor(role)} members")
                    .AlsoCaptionCenter());
                Grid.SetColumn(header, roleIndex + 1);
                Grid.SetRow(header, 0);
                matrixHost.Children.Add(header);
            }

            var permissionLabel = TeamKit.Caption("Permission");
            permissionLabel.Margin = new Thickness(0, 0, 0, 8);
            Grid.SetRow(permissionLabel, 0);
            matrixHost.Children.Add(permissionLabel);

            var row = headerRow + 1;
            foreach (var group in Groups)
            {
                var groupLabel = new StackPanel
                {
                    Spacing = 1,
                    Margin = new Thickness(0, 8, 0, 2)
                };
                groupLabel.Children.Add(new TextBlock { Text = group.Name, Classes = { "arc-label" } });
                groupLabel.Children.Add(TeamKit.Caption($"{group.Permissions.Length} permissions"));
                Grid.SetRow(groupLabel, row);
                matrixHost.Children.Add(groupLabel);

                for (var roleIndex = 0; roleIndex < roles.Count; roleIndex++)
                {
                    var role = roles[roleIndex];
                    var granted = group.Permissions.Count(permission => grants[(role, permission)]);
                    var locked = role == "Admin";
                    var all = granted == group.Permissions.Length;

                    var count = TeamKit.Selectable(new TextBlock
                    {
                        Text = $"{granted}/{group.Permissions.Length}",
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }, () =>
                    {
                        foreach (var permission in group.Permissions)
                        {
                            grants[(role, permission)] = !all;
                        }

                        RenderMatrix();
                        RenderReview();
                        status.Text = $"{role}: {group.Name} permissions {(all ? "cleared" : "granted")}.";
                        status.Foreground = TeamKit.Brush(ArcTokens.TextMuted);
                    }, false, $"{role}, {group.Name} group", 999, new Thickness(2));

                    count.Width = 54;
                    count.Margin = new Thickness(0, 9, 0, 3);
                    count.Opacity = locked ? 0.5 : 1;
                    if (locked)
                    {
                        count.IsHitTestVisible = false;
                    }

                    Grid.SetColumn(count, roleIndex + 1);
                    Grid.SetRow(count, row);
                    matrixHost.Children.Add(count);
                }

                var rule = TeamKit.Divider();
                rule.Margin = new Thickness(0, 12, 0, 0);
                Grid.SetColumnSpan(rule, columnCount);
                Grid.SetRow(rule, row);
                matrixHost.Children.Add(rule);
                row++;

                foreach (var permission in group.Permissions)
                {
                    var label = new TextBlock
                    {
                        Text = permission,
                        Classes = { "arc-body-sm" },
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(10, 0, 0, 0)
                    };
                    Grid.SetRow(label, row);
                    matrixHost.Children.Add(label);

                    for (var roleIndex = 0; roleIndex < roles.Count; roleIndex++)
                    {
                        var role = roles[roleIndex];
                        var locked = role == "Admin";
                        var granted = grants[(role, permission)];
                        var cell = TeamKit.Selectable(new ArcIcon
                        {
                            Icon = granted ? "check" : "minus",
                            Size = 13,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = granted
                                ? TeamKit.Brush(locked ? ArcTokens.TextMuted : ArcTokens.AccentStrong)
                                : TeamKit.Brush(ArcTokens.TextMuted)
                        }, () =>
                        {
                            grants[(role, permission)] = !granted;
                            RenderMatrix();
                            RenderReview();
                            status.Text = $"{role}: {permission} {(granted ? "revoked" : "granted")}.";
                            status.Foreground = TeamKit.Brush(ArcTokens.TextMuted);
                        }, granted, $"{role}, {permission}", 7, new Thickness(0));

                        cell.Width = 26;
                        cell.Height = 26;
                        cell.HorizontalAlignment = HorizontalAlignment.Center;
                        cell.Margin = new Thickness(0, 2);
                        cell.Opacity = locked ? 0.55 : 1;
                        if (locked)
                        {
                            cell.IsHitTestVisible = false;
                        }

                        Grid.SetColumn(cell, roleIndex + 1);
                        Grid.SetRow(cell, row);
                        matrixHost.Children.Add(cell);
                    }

                    row++;
                }
            }
        }

        var duplicate = TeamKit.Btn("Duplicate Viewer", ArcButtonVariant.Secondary, ArcButtonSize.Sm, icon: "copy");
        duplicate.Click += (_, _) =>
        {
            const string source = "Viewer";
            var name = "Viewer copy";
            if (roles.Contains(name))
            {
                status.Text = "The duplicated role already exists in this preview.";
                return;
            }

            roles.Add(name);
            foreach (var group in Groups)
            {
                foreach (var permission in group.Permissions)
                {
                    grants[(name, permission)] = grants[(source, permission)];
                }
            }

            RenderMatrix();
            status.Text = "Viewer duplicated. Rename it and adjust its permissions before saving.";
            status.Foreground = TeamKit.Brush(ArcTokens.TextMuted);
        };

        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = "Roles and permissions", Classes = { "arc-heading" } },
                TeamKit.Row(6,
                    TeamKit.Avatar("Priya Raman", ArcAvatarSize.Xs),
                    TeamKit.Caption("Northwind Studio · 24 members · last reviewed by Priya on 2 Nov"))
            }
        });
        Grid.SetColumn(duplicate, 1);
        heading.Children.Add(duplicate);

        reviewHost.Background = TeamKit.Tint(ArcTokens.Warning, 0.09);
        reviewHost.BorderBrush = TeamKit.Tint(ArcTokens.Warning, 0.24);
        reviewHost.BorderThickness = new Thickness(1);
        reviewHost.CornerRadius = new CornerRadius(14);
        reviewHost.Padding = new Thickness(12, 8);

        RenderMatrix();
        RenderReview();

        return TeamKit.Root(new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Padding = new Thickness(20),
            Child = TeamKit.Stack(12,
                heading,
                TeamKit.Divider(),
                matrixHost,
                reviewHost,
                status)
        });
    }

    private static string MembersFor(string role) => role switch
    {
        "Admin" => "2",
        "Editor" => "9",
        _ => "13"
    };
}

internal static class TextBlockConvenience
{
    public static TextBlock AlsoCaptionCenter(this TextBlock block)
    {
        block.HorizontalAlignment = HorizontalAlignment.Center;
        return block;
    }
}
