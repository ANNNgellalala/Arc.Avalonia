using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Media.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Media.Pages;

/// <summary>Voice orb: the four session states, transcript and call controls, driven by demo state buttons.</summary>
public sealed class VoiceOrbPage : ArcItemPage
{
    public override string ItemSlug => "voice-orb";

    public override bool DemoOnCanvas => false;

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
        var orb = new ArcVoiceOrb
        {
            Width = 540,
            Height = 470,
            Transcript = "The studio opens at nine. Shall I book a table for two?",
            State = ArcVoiceOrbState.Listening
        };

        var states = new StackPanel { Spacing = 8, Width = 260, Margin = new Thickness(24, 12, 0, 0) };
        states.Children.Add(new TextBlock { Text = "Session states", Classes = { "arc-label" } });
        states.Children.Add(Caption("Listening draws the microphone level and two ripples; thinking contracts; speaking brightens."));
        foreach (var state in new[]
                 {
                     ArcVoiceOrbState.Listening, ArcVoiceOrbState.Thinking, ArcVoiceOrbState.Speaking, ArcVoiceOrbState.Idle
                 })
        {
            var value = state;
            var button = new ArcButton
            {
                Content = state == ArcVoiceOrbState.Idle ? "End call" : state.ToString(),
                Variant = ArcButtonVariant.Secondary,
                Size = ArcButtonSize.Sm,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            button.Click += (_, _) =>
            {
                orb.State = value;
                if (value == ArcVoiceOrbState.Idle)
                {
                    orb.Elapsed = 0;
                }
            };
            states.Children.Add(button);
        }

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { orb, states }
        };
        return row;
    }

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        Classes = { "arc-caption" },
        TextWrapping = TextWrapping.Wrap
    };
}
