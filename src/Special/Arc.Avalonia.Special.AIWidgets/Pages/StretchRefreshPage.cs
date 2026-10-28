using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIWidgets.Controls;

namespace Arc.Avalonia.Special.AIWidgets.Pages;

/// <summary>
/// Catalog page for stretch refresh. The demo holds the feed mid pull so the stretched line and the spread rows are
/// visible; dragging the feed or pressing Refresh runs the real gesture and the result.
/// </summary>
public sealed class StretchRefreshPage : ArcItemPage
{
    public override string ItemSlug => "stretch-refresh";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var feed = new ArcStretchRefresh
        {
            Title = "Inbox",
            Subtitle = "Updated 14 min ago",
            Threshold = 72,
            Items = new List<ArcFeedItem>
            {
                new() { Id = "1", Icon = "code", Title = "Mina opened a pull request in arc-ui/web", Meta = "Review requested · 3 files", Time = "09:41" },
                new() { Id = "2", Icon = "message-circle", Title = "Rowan replied in #design-system", Meta = "The radii look right on the nested cards", Time = "09:12" },
                new() { Id = "3", Icon = "bell", Title = "Nightly visual regression finished", Meta = "412 of 412 snapshots match", Time = "08:30" },
                new() { Id = "4", Icon = "box", Title = "Arc 4.1.2 was published", Meta = "Patch · motion fixes", Time = "07:58" }
            }
        };

        feed.HoldPreview(1.15);
        return feed;
    }

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Timelines, inboxes and activity feeds where new entries arrive at the top.",
        "Panels that support pull to refresh by touch, mouse drag or trackpad.",
        "A mobile-shaped list inside a desktop shell needs the gesture people expect."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "For static content that never refreshes, use a plain list.",
        "For paging through older items, use pagination rather than a pull gesture."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "A refresh button gives keyboard and switch users the same path as the pull, and reports busy while it runs.",
        "The feed is a labelled region and the list is marked busy while refreshing.",
        "Refreshing and its result are announced in a polite status region."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The sheet rubber-bands with growing resistance while the rows spread apart.",
        "The line in the gap reaches full length exactly at the threshold, runs while refreshing, then gives way to the result as new rows settle in.",
        "Releasing below the threshold springs back on the smooth spring; reduced motion keeps the same states and only fades."
    };

    public override string? ImplementationNotes =>
        "Recreated from the public stretch-refresh documentation. The demo feed is not scrollable, so the pull is a pointer drag on the feed rather " +
        "than a scroll-over-the-top handoff, and the stretch line is drawn by the control instead of being a DOM element that gains height. " +
        "The still frame holds the pull just past the threshold so the armed state is visible in screenshots.";
}
