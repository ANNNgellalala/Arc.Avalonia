using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Media.Controls;

namespace Arc.Avalonia.Special.Media.Pages;

/// <summary>
/// Voice orb's completeness preview: the four session states laid out as still frames so Listening,
/// Thinking, Speaking and Muted are all visible without any input. Declared state keys match
/// Docs/voice-orb.json so the audit can compare the two lists.
/// </summary>
public sealed class VoiceOrbPage : ArcItemPage
{
    public override string ItemSlug => "voice-orb";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-listening", "state-thinking", "state-speaking", "variant-muted"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A voice mode where the person needs to see whether the assistant is listening, thinking or speaking.",
        "Hands-free or call-like flows that need start, mute, a running clock and end in one compact control."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Recording a clip to send or save: use voice recorder. Typed chat: use chat thread with text stream."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The orb is named for its state (Listening, Thinking, Speaking, Muted) and changes are announced; the clock reads minutes and seconds.",
        "Space or Enter starts or ends the session, M toggles mute and Escape ends the call; clicking the sphere walks through the states in this demo."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Listening swells with the input level and draws two ripple rings, thinking contracts into a slow swirl with a band of light, and speaking brightens with the voice.",
        "Reduced motion stops the loop and draws one still frame per state; the transcript and pill keep short opacity fades."
    };

    public override string? ImplementationNotes =>
        "The sphere is a 90 point wobbling blob filled with a radial gradient instead of a WebGL shader, and the " +
        "microphone level is simulated because the gallery does not capture audio. Reduced motion draws a fixed " +
        "phase so every state has a deterministic still.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var grid = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };

        grid.Children.Add(StateOrb(
            ArcVoiceOrbState.Listening, false,
            "Listening：随模拟电平呼吸，外扩两圈涟漪，胶囊显示计时与挂断。",
            12, "The studio opens at nine. Shall I book a table for two?"));

        grid.Children.Add(StateOrb(
            ArcVoiceOrbState.Thinking, false,
            "Thinking：球体收缩到 92%，弹性最小，边缘转一段强调色光带。",
            12, "Checking the calendar…"));

        grid.Children.Add(StateOrb(
            ArcVoiceOrbState.Speaking, false,
            "Speaking：亮度与波动幅度提高，表示助手正在说话。",
            21, "There is a table at nine. I can hold it for you."));

        grid.Children.Add(StateOrb(
            ArcVoiceOrbState.Listening, true,
            "Muted：球体与胶囊转中性色，麦克风上画一道强调色斜杠。",
            34, "The studio opens at nine. Shall I book a table for two?"));

        return grid;
    }

    /// <summary>One state as a still: the orb is 418px wide so two fit per row with the copy underneath.</summary>
    private static Control StateOrb(
        ArcVoiceOrbState state, bool muted, string caption, double elapsed, string transcript)
    {
        var orb = new ArcVoiceOrb
        {
            Width = 418,
            Height = 430,
            HorizontalAlignment = HorizontalAlignment.Left,
            State = state,
            Muted = muted,
            Elapsed = elapsed,
            Transcript = transcript
        };

        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 0, 10, 16),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        panel.Children.Add(orb);
        panel.Children.Add(new TextBlock
        {
            Text = caption,
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 418,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        return panel;
    }
}
