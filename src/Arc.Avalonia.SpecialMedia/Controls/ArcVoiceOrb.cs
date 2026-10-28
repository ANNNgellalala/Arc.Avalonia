using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMedia.Controls;

/// <summary>The four states of a voice session, matching Arc's <c>VoiceOrbState</c>.</summary>
public enum ArcVoiceOrbState
{
    Idle,
    Listening,
    Thinking,
    Speaking
}

/// <summary>
/// Arc's voice orb: a luminous fluid sphere that breathes when idle, ripples with the microphone while
/// listening, contracts into a slow swirl while thinking and brightens with the voice while speaking. One
/// still frame per state is drawn under reduced motion. The demo uses a simulated input level, since a
/// desktop gallery does not capture audio.
/// </summary>
public sealed class ArcVoiceOrb : ArcSpecialCanvas
{
    private ArcVoiceOrbState _state = ArcVoiceOrbState.Listening;
    private bool _muted;
    private string _transcript = "The studio opens at nine. Shall I book a table for two?";
    private double _elapsed = 12;
    private double _time;
    private double _reveal = 1;
    private Rect _orb = new(170, 78, 204, 204);
    private Rect _pill;
    private Rect _mic;
    private Rect _end;
    private Rect _start;

    public ArcVoiceOrb()
    {
        SpecialUi.Name(
            this,
            "Voice orb, listening",
            "Press Space or Enter to start or end the session and M to mute the microphone. The orb is decorative; the state is announced with the session.");
    }

    protected override double DefaultWidth => 540;

    protected override double DefaultHeight => 470;

    public ArcVoiceOrbState State
    {
        get => _state;
        set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            _reveal = SpecialUi.Reduced ? 1 : 0;
            UpdateAutomation();
            EnsureFrame();
            InvalidateVisual();
        }
    }

    public bool Muted
    {
        get => _muted;
        set
        {
            _muted = value;
            UpdateAutomation();
            InvalidateVisual();
        }
    }

    /// <summary>The live transcript line under the orb; two lines are kept.</summary>
    public string Transcript
    {
        get => _transcript;
        set
        {
            _transcript = value;
            _reveal = SpecialUi.Reduced ? 1 : 0;
            EnsureFrame();
            InvalidateVisual();
        }
    }

    /// <summary>Seconds since the session began, shown as m:ss in the call pill.</summary>
    public double Elapsed
    {
        get => _elapsed;
        set
        {
            _elapsed = value;
            InvalidateVisual();
        }
    }

    private bool Active => State != ArcVoiceOrbState.Idle;

    private string StateWord => State switch
    {
        ArcVoiceOrbState.Listening => _muted ? "Muted" : "Listening",
        ArcVoiceOrbState.Thinking => "Thinking",
        ArcVoiceOrbState.Speaking => "Speaking",
        _ => "Idle"
    };

    private void UpdateAutomation()
    {
        SpecialUi.Name(this, $"Voice orb, {StateWord.ToLowerInvariant()}");
    }

    protected override void OnFrame(double dt)
    {
        _time += dt;
        _reveal = Math.Min(1, _reveal + dt * 1.8);
        if (Active)
        {
            _elapsed += dt;
        }
    }

    protected override bool NeedsFrame => Active || _reveal < 1;

    private void StartOrEnd()
    {
        if (Active)
        {
            State = ArcVoiceOrbState.Idle;
        }
        else
        {
            _elapsed = 0;
            State = ArcVoiceOrbState.Listening;
        }

        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        var point = e.GetPosition(this);
        Layout(Bounds.Size);

        if (_mic.Contains(point))
        {
            Muted = !Muted;
        }
        else if (_end.Contains(point))
        {
            State = ArcVoiceOrbState.Idle;
        }
        else if (_start.Contains(point) || _orb.Contains(point))
        {
            if (!Active)
            {
                _elapsed = 0;
                State = ArcVoiceOrbState.Listening;
            }
            else if (_orb.Contains(point))
            {
                // Clicking the sphere walks the demo through the session states.
                State = State switch
                {
                    ArcVoiceOrbState.Listening => ArcVoiceOrbState.Thinking,
                    ArcVoiceOrbState.Thinking => ArcVoiceOrbState.Speaking,
                    _ => ArcVoiceOrbState.Listening
                };
            }
        }

        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Space:
            case Key.Enter:
                StartOrEnd();
                e.Handled = true;
                break;
            case Key.M:
                Muted = !Muted;
                e.Handled = true;
                break;
            case Key.Escape:
                State = ArcVoiceOrbState.Idle;
                e.Handled = true;
                break;
        }
    }

    private void Layout(Size size)
    {
        var center = new Point(size.Width / 2, 150);
        _orb = new Rect(center.X - 102, center.Y - 102, 204, 204);
        _pill = new Rect(center.X - 118, 356, 236, 56);
        _mic = new Rect(_pill.X + 8, _pill.Y + 8, 40, 40);
        _end = new Rect(_pill.Right - 48, _pill.Y + 8, 40, 40);
        _start = new Rect(center.X - 88, 360, 176, 48);
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        Layout(size);
        var accent = SpecialUi.Color(ArcTokens.Accent);
        var accentStrong = SpecialUi.Color(ArcTokens.AccentStrong);
        var background = SpecialUi.Color(ArcTokens.Background);
        var foreground = SpecialUi.Color(ArcTokens.Foreground);
        var muted = SpecialUi.Color(ArcTokens.TextMuted);
        var secondary = SpecialUi.Color(ArcTokens.TextSecondary);
        var tint = Muted ? ArcColor.Mix(accent, muted, 0.62) : accent;

        var center = _orb.Center;
        var baseRadius = 62.0;
        var level = SimulatedLevel();
        var breathe = State == ArcVoiceOrbState.Thinking ? 0.92 : 1 + 0.018 * Math.Sin(_time * 0.9);
        var radius = baseRadius * breathe * (1 + level * 0.06);

        // Ambient halo.
        for (var layer = 2; layer >= 1; layer--)
        {
            context.DrawEllipse(
                SpecialUi.Solid(ArcColor.WithAlpha(tint, 0.04 * layer)),
                null,
                center,
                radius + layer * 12,
                radius + layer * 12);
        }

        // Listening ripples: two rings travelling outward on a loop.
        if (State == ArcVoiceOrbState.Listening && !Muted)
        {
            for (var ring = 0; ring < 2; ring++)
            {
                var ripplePhase = (SpecialUi.Reduced ? ring * 0.5 : (_time * 0.55 + ring * 0.5) % 1);
                var ringRadius = radius * 1.12 + ripplePhase * radius * 0.95;
                context.DrawEllipse(
                    null,
                    new Pen(SpecialUi.Solid(ArcColor.WithAlpha(tint, 0.30 * (1 - ripplePhase))), 1.5),
                    center,
                    ringRadius,
                    ringRadius);
            }
        }

        // Thinking: a band of light orbiting the rim.
        if (State == ArcVoiceOrbState.Thinking)
        {
            var bandRadius = radius + 9;
            var startAngle = SpecialUi.Reduced ? -0.6 : _time * 1.1;
            var sweep = 1.7;
            var band = new StreamGeometry();
            using (var geometry = band.Open())
            {
                geometry.BeginFigure(
                    new Point(center.X + Math.Cos(startAngle) * bandRadius, center.Y + Math.Sin(startAngle) * bandRadius),
                    false);
                geometry.ArcTo(
                    new Point(center.X + Math.Cos(startAngle + sweep) * bandRadius, center.Y + Math.Sin(startAngle + sweep) * bandRadius),
                    new Size(bandRadius, bandRadius),
                    0,
                    false,
                    SweepDirection.Clockwise);
            }

            context.DrawGeometry(
                null,
                new Pen(SpecialUi.Solid(ArcColor.WithAlpha(accentStrong, 0.9)), 3, null, PenLineCap.Round, PenLineJoin.Round),
                band);
        }

        // The sphere: a wobbling blob filled with a radial brand gradient.
        var amplitude = State switch
        {
            ArcVoiceOrbState.Listening => 0.028 + level * 0.045,
            ArcVoiceOrbState.Speaking => 0.030 + level * 0.055,
            ArcVoiceOrbState.Thinking => 0.012,
            _ => 0.026
        };
        var phase = SpecialUi.Reduced ? 0.7 : _time * 1.15;
        var blob = Blob(center, radius, amplitude, phase);
        var light = ArcColor.Mix(tint, background, 0.52);
        var deep = ArcColor.Mix(tint, foreground, 0.22);
        var brush = new RadialGradientBrush
        {
            Center = new RelativePoint(0.44, 0.40, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.34, 0.28, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.72, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.72, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(light, 0),
                new GradientStop(tint, 0.55),
                new GradientStop(deep, 1)
            }
        };
        context.DrawGeometry(brush, null, blob);
        context.DrawGeometry(null, new Pen(SpecialUi.Solid(ArcColor.WithAlpha(light, 0.35)), 1), blob);

        // A soft inner light and a rim lift keep the sphere reading as a liquid volume.
        context.DrawEllipse(
            SpecialUi.Solid(ArcColor.WithAlpha(background, Muted ? 0.35 : 0.5)),
            null,
            new Point(center.X - radius * 0.30, center.Y - radius * 0.34),
            radius * 0.16,
            radius * 0.11);
        context.DrawEllipse(
            null,
            new Pen(SpecialUi.Solid(ArcColor.WithAlpha(background, 0.12)), 1),
            center,
            radius * 0.72,
            radius * 0.72);

        // State word and transcript.
        var stateText = SpecialUi.Text(StateWord, 11, SpecialUi.Solid(accentStrong), FontWeight.Medium);
        SpecialUi.DrawTextCentered(context, stateText, new Rect(0, 252, size.Width, 18));

        var label = SpecialUi.Text("Assistant", 11, SpecialUi.Solid(muted));
        SpecialUi.DrawTextCentered(context, label, new Rect(0, 274, size.Width, 16));

        var transcript = SpecialUi.Text(
            _transcript,
            17,
            SpecialUi.Solid(ArcColor.Mix(foreground, muted, 1 - Math.Clamp(_reveal, 0, 1))),
            FontWeight.Normal,
            420,
            2,
            TextAlignment.Center);
        context.DrawText(transcript, new Point(size.Width / 2 - 210, 296));

        // Controls pill.
        if (Active)
        {
            SpecialUi.Lift(context, _pill, 28, 0.9);
            SpecialUi.RoundRect(context, _pill, 28, SpecialUi.Brush(ArcTokens.Surface));
            context.DrawRectangle(null, new Pen(SpecialUi.Brush(ArcTokens.Border), 1), _pill.Deflate(0.5), 28, 28);

            var micFill = Muted ? SpecialUi.Brush(ArcTokens.AccentSubtle) : SpecialUi.Brush(ArcTokens.SurfaceMuted);
            SpecialUi.RoundRect(context, _mic, 20, micFill);
            var micInk = Muted ? accent : foreground;
            SpecialIcons.Draw(context, "mic", _mic.Deflate(10), micInk, 1.8);
            if (Muted)
            {
                context.DrawLine(new Pen(SpecialUi.Solid(accent), 1.6),
                    new Point(_mic.X + 12, _mic.Bottom - 11),
                    new Point(_mic.Right - 11, _mic.Y + 12));
            }

            var clock = SpecialUi.Text(SpecialUi.Time(_elapsed), 15, SpecialUi.Solid(foreground), FontWeight.Medium);
            SpecialUi.DrawTextCentered(context, clock, new Rect(_mic.Right, _pill.Y, _end.X - _mic.Right, _pill.Height));
            if (!Muted)
            {
                context.DrawEllipse(SpecialUi.Solid(accent), null, new Point(_mic.Right + 12, _pill.Center.Y), 3, 3);
            }

            SpecialUi.RoundRect(context, _end, 20, SpecialUi.Brush(ArcTokens.DangerSubtle));
            SpecialIcons.Draw(context, "phone", _end.Deflate(11), SpecialUi.Solid(SpecialUi.Color(ArcTokens.Danger)), 1.8, 135);
        }
        else
        {
            SpecialUi.Lift(context, _start, 24, 0.9);
            SpecialUi.RoundRect(context, _start, 24, SpecialUi.Brush(ArcTokens.Accent));
            SpecialIcons.Draw(context, "phone", new Rect(_start.X + 22, _start.Y + 14, 20, 20), SpecialUi.Brush(ArcTokens.AccentForeground), 1.8);
            var startText = SpecialUi.Text("Start voice chat", 14, SpecialUi.Brush(ArcTokens.AccentForeground), FontWeight.Medium);
            context.DrawText(startText, new Point(_start.X + 50, _start.Y + (_start.Height - startText.Height) / 2));
        }

        if (ShowFocusRing)
        {
            SpecialUi.FocusRing(context, _orb, 104);
        }

        _ = secondary;
    }

    private double SimulatedLevel()
    {
        if (Muted || State == ArcVoiceOrbState.Thinking || State == ArcVoiceOrbState.Idle)
        {
            return 0;
        }

        if (SpecialUi.Reduced)
        {
            return 0.35;
        }

        var speed = State == ArcVoiceOrbState.Speaking ? 3.4 : 2.0;
        var value = 0.42 + 0.26 * Math.Sin(_time * speed) + 0.14 * Math.Sin(_time * speed * 2.3 + 1.4);
        return Math.Clamp(value, 0.05, 0.95);
    }

    private static StreamGeometry Blob(Point center, double radius, double amplitude, double phase)
    {
        const int steps = 90;
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        for (var index = 0; index <= steps; index++)
        {
            var theta = index * Math.PI * 2 / steps;
            var wobble = amplitude * (Math.Sin(3 * theta + phase)
                                      + 0.55 * Math.Sin(5 * theta - phase * 1.4)
                                      + 0.30 * Math.Sin(7 * theta + phase * 0.7));
            var r = radius * (1 + wobble);
            var point = new Point(center.X + Math.Cos(theta) * r, center.Y + Math.Sin(theta) * r);
            if (index == 0)
            {
                path.BeginFigure(point, true);
            }
            else
            {
                path.LineTo(point);
            }
        }

        path.EndFigure(true);
        return geometry;
    }
}
