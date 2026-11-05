using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Mk = Arc.Avalonia.Blocks.Marketing.MarketingKit;

namespace Arc.Avalonia.Blocks.Marketing.Pages;

/// <summary>
/// Page header: a project header with breadcrumbs, status, members, progress and tabs whose counts roll,
/// folding into a compact bar. The specimen drives the fold from a switch instead of a scroll listener, which
/// keeps the block honest at gallery height; tab counts are sample data.
/// </summary>
public sealed class PageHeaderPage : ArcItemPage
{
    public override string ItemSlug => "page-header";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-expanded", "variant-tabs", "variant-members", "state-folded"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The top of a project, repository or record page with status, people and tabs.",
        "Detail pages with long content, where the header should fold into a compact bar.",
        "Headers that need a few primary actions plus an overflow menu."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use tabs and a breadcrumb on their own when the page only needs navigation.",
        "Use the workspace sidebar for app-level navigation between sections."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Arrow keys move between tabs and the selected tab is the only tab stop.",
        "The icon-only actions carry labels, and the overflow trigger is named More actions.",
        "The compact state keeps the same reading order; only the meta row and breadcrumb are dropped."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Past the scroll threshold the header folds into a compact bar while the tab row stays put.",
        "Tab counts roll to their new values; reduced motion swaps the states without travel."
    };

    public override string? ImplementationNotes =>
        "The reference folds the header from its own inner scroller. Here the fold is a switch, so the specimen fits one screen; everything else (breadcrumb, status, member group, progress, tab counts, actions) is real Arc controls. Tabs use a segmented control with count accessories because the header only needs the strip, not the tab panel.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var breadcrumb = Mk.Row(6,
            Mk.Caption("Acme"),
            new ArcIcon { Icon = "chevron-right", Size = 12, Foreground = Mk.Brush(ArcTokens.TextMuted) },
            Mk.Caption("Projects"),
            new ArcIcon { Icon = "chevron-right", Size = 12, Foreground = Mk.Brush(ArcTokens.TextMuted) },
            Mk.Caption("Atlas design system"));

        var largeTitle = Mk.Text("Atlas design system", "arc-title-sm", 520);
        var smallTitle = Mk.Text("Atlas design system", "arc-heading", 520);
        smallTitle.IsVisible = false;

        var status = Mk.Pill("In progress", ArcBadgeTone.Warning, dot: true);

        var followers = new ArcAvatarGroup
        {
            Members = new List<ArcAvatarMember>
            {
                new() { Name = "Maya Chen" },
                new() { Name = "Jonas Weber" },
                new() { Name = "Sofia Ahlberg" },
                new() { Name = "Ravi Patel" },
                new() { Name = "Elena Rossi" }
            },
            Max = 5,
            Size = ArcAvatarSize.Xs,
            Label = "Project members"
        };

        var progress = new ArcProgress { Value = 62, Max = 100, ShowValue = true, Size = ArcProgressSize.Sm, Width = 150 };
        var meta = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,*") };
        meta.Children.Add(Mk.Caption("Members"));
        meta.Children.Add(new Border { Width = 8 });
        var members = followers;
        Grid.SetColumn(members, 2);
        meta.Children.Add(members);
        var progressBlock = Mk.Row(8, progress, Mk.Caption("to 14 Mar"));
        progressBlock.Margin = new Thickness(24, 0, 0, 0);
        Grid.SetColumn(progressBlock, 3);
        meta.Children.Add(progressBlock);

        var follow = Mk.Btn("Follow", ArcButtonVariant.Secondary, ArcButtonSize.Sm, icon: "star");
        var share = Mk.IconBtn("share", "Share project");
        var more = Mk.IconBtn("more-horizontal", "More actions");

        var actionRow = Mk.Row(6, follow, share, more);

        var titleRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
        titleRow.Children.Add(largeTitle);
        Grid.SetColumn(smallTitle, 0);
        titleRow.Children.Add(smallTitle);
        status.Margin = new Thickness(12, 0, 0, 0);
        status.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(status, 1);
        titleRow.Children.Add(status);
        actionRow.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(actionRow, 3);
        titleRow.Children.Add(actionRow);

        var tabs = Mk.Segmented(
            new[] { ("overview", "Overview", "24"), ("issues", "Issues", "8"), ("releases", "Releases", "3"), ("roadmap", "Roadmap", (string?)null) },
            "overview",
            _ => { },
            "Project sections");

        var compact = new ArcSwitch { Label = "Compact header", Checked = false };
        compact.CheckedChanged += (_, _) =>
        {
            var folded = compact.Checked;
            breadcrumb.IsVisible = !folded;
            meta.IsVisible = !folded;
            largeTitle.IsVisible = !folded;
            smallTitle.IsVisible = folded;
            status.IsVisible = !folded;
        };

        var head = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Children = { breadcrumb, titleRow, meta }
        };

        var tabRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        tabRow.Children.Add(tabs);
        var compactHost = compact;
        compactHost.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(compactHost, 1);
        tabRow.Children.Add(compactHost);

        // The folded bar is otherwise only reachable through the switch; a static copy keeps it visible.
        var foldedSpecimen = Mk.Surface(new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Margin = new Thickness(24, 14, 24, 12),
            Children =
            {
                Mk.Row(10,
                    Mk.Text("Atlas design system", "arc-heading", 420),
                    Mk.Pill("In progress", ArcBadgeTone.Warning, dot: true),
                    Mk.Caption("62% to 14 Mar")),
                Mk.Caption("Overview · Issues · Releases · Roadmap")
            }
        });

        return Mk.Stack(16,
            Mk.Surface(new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 14,
                Margin = new Thickness(26, 22, 26, 20),
                Children = { head, Mk.Divider(), tabRow }
            }),
            Mk.Caption("Folded compact bar, past the scroll threshold"),
            foldedSpecimen);
    }
}

