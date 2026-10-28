using System.Collections.Generic;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialMedia.Controls;

namespace Arc.Avalonia.SpecialMedia.Pages;

/// <summary>Voice recorder: the thread, the review bar with a scrubbable waveform, and send into the thread.</summary>
public sealed class VoiceRecorderPage : ArcItemPage
{
    public override string ItemSlug => "voice-recorder";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Voice notes in chats, comments or field notes where speaking beats typing.",
        "A thread of voice bubbles with playback and scrubbing."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Music or long-form audio: use now playing. Press-and-hold actions that do not record: use hold to confirm."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Every control is announced with its duration, such as \"Play voice message from Sam, 0:07\"; the live and review waveforms are decorative duplicates.",
        "Space or Enter starts, stops, plays or sends depending on the focused control, Left and Right move between them, and Escape discards a take."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Mic, recording bar and review share one layout, so a take grows out of the mic and settles into the thread on send.",
        "The live waveform scrolls while recording and playback fills the bars left to right; reduced motion skips the layout travel and steps the bars."
    };

    public override string? ImplementationNotes =>
        "Recording is simulated: peaks come from a deterministic noise function, so the demo and the screenshots are " +
        "identical on every run. Real capture would push MediaRecorder chunks into the same send path. The seeded " +
        "state is a take in review, which shows the richest frame.";

    protected override Control BuildDemo(ArcDemoContext context) => new ArcVoiceRecorder
    {
        Width = 680,
        Height = 442,
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
    };
}
