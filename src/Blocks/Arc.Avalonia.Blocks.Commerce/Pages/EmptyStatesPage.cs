using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;
using Arc.Avalonia.Blocks.Commerce.Controls;
using Ck = Arc.Avalonia.Blocks.Commerce.CommerceKit;

namespace Arc.Avalonia.Blocks.Commerce.Pages;

/// <summary>
/// Empty states: four moments (no results, offline, inbox zero, missing page) sharing one drawn illustration
/// whose shapes interpolate between scenes as the tabs change. The actions only update local status; nothing
/// fetches or retries.
/// </summary>
public sealed class EmptyStatesPage : ArcItemPage
{
    public override string ItemSlug => "empty-states";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A reference for no results, offline, inbox zero and missing page moments with one illustration style.",
        "Extracting one scene as a richer empty state for a key screen."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "A simple empty state with an icon and a line of text; use empty-state.",
        "Content that is still loading; use skeleton."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The scene tabs are a single-select group reachable with arrow keys, and the illustration is labelled by its scene.",
        "Action results are announced politely, and every action is a real button rather than an illustration hot spot."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Each shape interpolates toward the next scene with a 420 ms eased morph while the copy changes.",
        "Reduced motion jumps straight to the target scene with no morph."
    };

    public override string? ImplementationNotes =>
        "The illustration is a drawn Control (ArcEmptyScene) rather than the reference's SVG path morph: six token shapes interpolate position, size and opacity, which reads as the same continuous change. Actions simulate their real outcome, and the tab strip is an ArcSegmentedControl so arrow keys work without a custom roving focus.";

    private sealed record Scene(string Id, string Label, string Title, string Body, string Action, string Secondary, string Result);

    private static readonly Scene[] Scenes =
    {
        new("results", "No results", "Nothing matches those filters",
            "Try a wider date range, or clear one of the facets. The search still runs over everything you can see.",
            "Clear filters", "Search instead", "Filters cleared. 248 records are back in view."),
        new("offline", "Offline", "You are offline",
            "Drafts stay on this device until the connection returns. Nothing you typed has been lost.",
            "Try again", "Work offline", "Still offline. We will retry when the network returns."),
        new("inbox", "Inbox zero", "Inbox zero",
            "Everything assigned to you is answered. New requests will land here and notify you once.",
            "View the archive", "Change notifications", "The archive opens in the reference; this specimen keeps the summary."),
        new("missing", "Missing page", "This page moved",
            "The link is from an older release. Search the docs, or start from the dashboard.",
            "Go to the dashboard", "Search the docs", "The dashboard would open here; this specimen stays on the scene.")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var index = 0;
        var status = Ck.Status();

        var art = new ArcEmptyScene { Height = 176, Scene = 0 };
        var title = Ck.Text(Scenes[0].Title, "arc-subtitle");
        var body = Ck.Body(Scenes[0].Body, 460);
        var primaryLabel = new TextBlock
        {
            Text = Scenes[0].Action,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Ck.Brush(ArcTokens.AccentForeground)
        };
        var primary = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Md,
            Height = 42,
            MinHeight = 42,
            Content = primaryLabel
        };
        primary.Click += (_, _) => Ck.Say(status, Scenes[index].Result);
        var secondaryHost = new ContentControl();

        void Render(int next, bool announce = true)
        {
            index = Math.Clamp(next, 0, Scenes.Length - 1);
            var scene = Scenes[index];
            art.Scene = index;
            title.Text = scene.Title;
            body.Text = scene.Body;
            primaryLabel.Text = scene.Action;
            secondaryHost.Content = Ck.Link(scene.Secondary, () => Ck.Say(status, scene.Result));
            if (announce)
            {
                Ck.Say(status, $"{scene.Label} scene shown.");
            }
        }

        var tabs = Ck.Segmented(
            new[]
            {
                ("results", "No results"), ("offline", "Offline"), ("inbox", "Inbox zero"), ("missing", "Missing page")
            },
            "results",
            value =>
            {
                var next = Array.FindIndex(Scenes, scene => scene.Id == value);
                Render(next < 0 ? 0 : next);
            },
            "Empty state scene");

        title.TextAlignment = TextAlignment.Center;
        title.HorizontalAlignment = HorizontalAlignment.Center;
        body.TextAlignment = TextAlignment.Center;
        body.HorizontalAlignment = HorizontalAlignment.Center;

        var copy = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { title, body }
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { primary, secondaryHost }
        };

        var stage = new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new Border { Width = 360, Height = 176, Child = art },
                copy,
                buttons
            }
        };

        Render(0, false);

        return Ck.Specimen(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { tabs }
                },
                stage,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { status }
                }
            }
        }, 16);
    }
}
