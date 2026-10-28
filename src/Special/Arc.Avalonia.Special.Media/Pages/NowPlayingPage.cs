using System.Collections.Generic;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Media.Controls;

namespace Arc.Avalonia.Special.Media.Pages;

/// <summary>Now playing: the mini bar and full player as one continuous morph.</summary>
public sealed class NowPlayingPage : ArcItemPage
{
    public override string ItemSlug => "now-playing";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Media that keeps playing while people browse, such as music, podcasts or audio lessons.",
        "A mini player bar that expands into a full player with artwork and a waveform scrubber."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "A one-off overlay that is not a player: use bottom sheet or dialog. A plain seek bar: use slider."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The waveform is a slider with a spoken value such as \"1:05 of 3:32\"; Left and Right seek five seconds, PageUp and PageDown thirty, Home and End jump.",
        "Space toggles playback from the focused player, Up expands, Escape collapses, and the current track and play state are announced when they change."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The mini bar morphs into the full player on one spring: the shell grows, the artwork follows the same curve and the mini controls crossfade into the full set.",
        "The artwork can be pulled down more than 70 px to collapse, and skipping slides the artwork in the direction of travel. Reduced motion replaces the travel with short fades and holds the level bars still."
    };

    public override string? ImplementationNotes =>
        "Playback runs on an internal clock so the player works without audio; wire a real media element to the playing " +
        "and index changes in product code. The artwork is generated in code. Morphing a single drawn shell replaces " +
        "shared-layout animation, which Avalonia does not expose.";

    protected override Control BuildDemo(ArcDemoContext context) => new ArcNowPlaying
    {
        Width = 880,
        Height = 450,
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
    };
}
