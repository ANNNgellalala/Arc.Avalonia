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
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Team.Pages;

/// <summary>
/// Release readiness: a launch checklist that keeps prerequisites, progress and the final action together.
/// Ticking a check updates the readiness bar and the unlocked state of the release button; the release action
/// is disabled until every blocking check is done and the notify switch controls the announcement.
/// </summary>
public sealed class ReleaseReadinessPage : ArcItemPage
{
    private sealed class Check
    {
        public string Title { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public string Owner { get; init; } = string.Empty;
        public bool Done { get; set; }
    }

    public override string ItemSlug => "release-readiness";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-checklist", "state-blocking", "state-readiness-meter", "state-notify-toggle"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A pre-launch or handoff gate where several checks must be done before one action unlocks.",
        "Release checklists with a readiness bar and an owner for every prerequisite."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use onboarding-checklist for a user's own getting-started tasks rather than a release gate."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The completed count is a polite live region, and each check is a real labelled checkbox.",
        "Success is announced with a status region and failures with an alert; the release button explains why it is disabled."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The count swaps with a small rise and fade, and the readiness bar grows without overshoot because it reports a value.",
        "Reduced motion sets the duration to zero."
    };

    public override string? ImplementationNotes =>
        "The checklist, owners and release name are sample data in memory; Release only marks the release as " +
        "started. The notify switch changes copy only and no notification is sent. No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var checks = new List<Check>
        {
            new() { Title = "Production smoke test passed", Detail = "Checkout, billing and invites on prod", Owner = "Lena Fischer", Done = true },
            new() { Title = "Migration dry run finished", Detail = "Run 2026_11_09 on a production copy", Owner = "Priya Raman" },
            new() { Title = "Support macros updated", Detail = "Billing v2 replies and escalation paths", Owner = "Diego Alvarez", Done = true },
            new() { Title = "Release notes approved", Detail = "Reviewed by product and comms", Owner = "Maya Chen", Done = true },
            new() { Title = "Status page maintenance scheduled", Detail = "Window 22:00–22:30 UTC", Owner = "Lena Fischer" }
        };

        var notify = true;
        var released = false;
        var checklistHost = new StackPanel { Spacing = 12 };
        var summaryHost = new Border();
        var status = TeamKit.Caption("Release v2.4 · Thursday 12 Nov, 22:00 UTC");

        void RenderSummary()
        {
            var done = checks.Count(check => check.Done);
            var total = checks.Count;
            var ready = done == total;

            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(TeamKit.Caption("Readiness"));
            content.Children.Add(new TextBlock { Text = $"{done} of {total} checks", Classes = { "arc-heading" } });
            content.Children.Add(TeamKit.Meter((double)done / total, 8));
            content.Children.Add(ready
                ? TeamKit.Pill("Ready to ship", ArcBadgeTone.Success, dot: true)
                : TeamKit.Pill($"{total - done} blocking", ArcBadgeTone.Warning, dot: true));
            content.Children.Add(TeamKit.Divider());

            var release = TeamKit.Btn(released ? "Released v2.4" : "Release v2.4",
                released ? ArcButtonVariant.Secondary : ArcButtonVariant.Primary, ArcButtonSize.Md,
                onClick: () =>
                {
                    if (!ready)
                    {
                        status.Text = "Finish the blocking checks before releasing.";
                        status.Foreground = TeamKit.Brush(ArcTokens.Danger);
                        return;
                    }

                    released = true;
                    status.Text = notify
                        ? "Release started. Everyone in #release will be notified."
                        : "Release started quietly. No one was notified.";
                    status.Foreground = TeamKit.Brush(ArcTokens.Success);
                    RenderSummary();
                });
            release.IsEnabled = ready && !released;
            release.HorizontalAlignment = HorizontalAlignment.Stretch;
            release.HorizontalContentAlignment = HorizontalAlignment.Center;
            content.Children.Add(release);

            if (!ready)
            {
                content.Children.Add(TeamKit.Caption("Release unlocks after every blocking check passes."));
            }

            content.Children.Add(TeamKit.Divider());
            content.Children.Add(TeamKit.Toggle("Notify the team on release", notify, value =>
            {
                notify = value;
                status.Text = value ? "The team will be notified in #release." : "Release will run without an announcement.";
                status.Foreground = TeamKit.Brush(ArcTokens.TextMuted);
            }));

            summaryHost.Child = content;
        }

        void RenderChecklist()
        {
            checklistHost.Children.Clear();
            var groups = new[]
            {
                ("Engineering", new[] { "Production smoke test passed", "Migration dry run finished" }),
                ("Go to market", new[] { "Support macros updated", "Release notes approved", "Status page maintenance scheduled" })
            };

            foreach (var (group, titles) in groups)
            {
                var rows = new StackPanel { Spacing = 6 };
                foreach (var title in titles)
                {
                    var check = checks.First(entry => entry.Title == title);
                    var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                    var box = TeamKit.Check(check.Title, check.Done, value =>
                    {
                        check.Done = value;
                        RenderChecklist();
                        RenderSummary();
                        status.Text = value
                            ? $"{check.Title} checked. {checks.Count(entry => !entry.Done)} checks left."
                            : $"{check.Title} reopened.";
                        status.Foreground = TeamKit.Brush(value ? ArcTokens.Success : ArcTokens.TextMuted);
                    }, check.Detail);
                    row.Children.Add(box);
                    var owner = TeamKit.Row(6,
                        TeamKit.Avatar(check.Owner, ArcAvatarSize.Xs),
                        TeamKit.Caption(check.Owner.Split(' ')[0]));
                    owner.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetColumn(owner, 1);
                    row.Children.Add(owner);
                    rows.Children.Add(new Border
                    {
                        Padding = new Thickness(10, 6),
                        CornerRadius = new CornerRadius(12),
                        Background = TeamKit.Brush(ArcTokens.SurfaceMuted),
                        Child = row
                    });
                }

                var groupCard = new Border
                {
                    Background = TeamKit.Brush(ArcTokens.Surface),
                    BorderBrush = TeamKit.Brush(ArcTokens.Border),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(12),
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = group, Classes = { "arc-label" } },
                            rows
                        }
                    }
                };
                checklistHost.Children.Add(groupCard);
            }
        }

        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(new StackPanel
        {
            Spacing = 3,
            Children =
            {
                TeamKit.Row(8,
                    new TextBlock { Text = "Release readiness", Classes = { "arc-heading" } },
                    TeamKit.Pill("v2.4", ArcBadgeTone.Accent)),
                TeamKit.Caption("Northwind web · billing v2 · release owner Lena Fischer")
            }
        });
        var notes = TeamKit.Btn("Open release notes", ArcButtonVariant.Ghost, ArcButtonSize.Sm, icon: "external-link");
        Grid.SetColumn(notes, 1);
        heading.Children.Add(notes);

        RenderChecklist();
        RenderSummary();

        summaryHost.Background = TeamKit.Brush(ArcTokens.SurfaceMuted);
        summaryHost.CornerRadius = new CornerRadius(18);
        summaryHost.Padding = new Thickness(16);
        summaryHost.ClipToBounds = true;

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,340") };
        body.Children.Add(checklistHost);
        var rightHolder = new Border { Margin = new Thickness(24, 0, 0, 0), Child = summaryHost };
        Grid.SetColumn(rightHolder, 1);
        body.Children.Add(rightHolder);

        return TeamKit.Root(new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Padding = new Thickness(20),
            Child = TeamKit.Stack(16, heading, body, status)
        });
    }
}
