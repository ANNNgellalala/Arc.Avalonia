using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMedia.Controls;

/// <summary>
/// Arc's now playing player: a mini bar that grows into the full player in one continuous morph. The shell,
/// the artwork and the scrubber share the animation, so the transition reads as one surface changing shape;
/// the artwork can be pulled down to collapse and the waveform scrubs.
/// </summary>
public sealed class ArcNowPlaying : ArcSpecialCanvas
{
    private sealed record Track(string Title, string Artist, double Duration, int Seed);

    private static readonly Track[] Tracks =
    {
        new("Night drive", "Arc Ensemble", 212, 8),
        new("Low tide", "Harbor", 185, 1),
        new("Glass house", "Palms", 246, 6)
    };

    private Spring _morph = Spring.At(1, 190, 26);
    private Spring _art = Spring.At(1, 200, 26);
    private Spring _skipOffset = Spring.At(0, 230, 26);
    private int _index;
    private double _progress = 65;
    private bool _playing = true;
    private bool _expanded = true;
    private bool _pointerDown;
    private Point _pressPoint;
    private double _dragY;
    private bool _scrubbing;
    private Rect _shell;
    private Rect _artRect;
    private Rect _waveform;
    private Rect _play;
    private Rect _previous;
    private Rect _next;
    private Rect _collapse;
    private double _time;

    public ArcNowPlaying()
    {
        SpecialUi.Name(
            this,
            "Now playing",
            "A mini player that expands into the full player. Space toggles playback, Left and Right seek five seconds, PageUp and PageDown thirty, Home and End jump, Escape collapses.");
    }

    protected override double DefaultWidth => 880;

    protected override double DefaultHeight => 450;

    private Track Current => Tracks[_index];

    private void ComputeLayout(Size size)
    {
        var full = new Rect((size.Width - 340) / 2, 14, 340, Math.Min(410, size.Height - 28));
        var mini = new Rect((size.Width - 420) / 2, size.Height - 96, 420, 76);
        var t = Math.Clamp(_morph.Value, 0, 1);
        _shell = SpecialUi.Lerp(mini, full, t);

        var miniArt = new Rect(mini.X + 12, mini.Y + 12, 52, 52);
        var fullArt = new Rect(full.X + (full.Width - 214) / 2, full.Y + 24, 214, 214);
        _artRect = SpecialUi.Lerp(miniArt, fullArt, t);

        _waveform = new Rect(_shell.X + 26, _artRect.Bottom + 60, _shell.Width - 52, 40);
        var controlsY = _waveform.Bottom + 26;
        _play = new Rect(_shell.Center.X - 22, controlsY, 44, 44);
        _previous = new Rect(_play.X - 66, controlsY + 4, 36, 36);
        _next = new Rect(_play.Right + 30, controlsY + 4, 36, 36);
        _collapse = new Rect(_shell.Right - 44, _shell.Y + 12, 32, 32);
        if (t < 0.5)
        {
            _play = new Rect(_shell.Right - 78, _shell.Y + 21, 34, 34);
            _next = new Rect(_shell.Right - 38, _shell.Y + 24, 28, 28);
            _collapse = new Rect(_shell.Right - 14, _shell.Y - 30, 28, 28);
        }
    }

    private void TogglePlay()
    {
        _playing = !_playing;
        if (_playing)
        {
            EnsureFrame();
        }
    }

    private void Skip(int direction)
    {
        _index = (_index + direction + Tracks.Length) % Tracks.Length;
        _progress = 0;
        _skipOffset.Value = direction * 26;
        if (SpecialUi.Reduced)
        {
            _art.Jump(1);
        }
        else
        {
            _art.Jump(0);
        }
        EnsureFrame();
        InvalidateVisual();
    }

    private void Seek(double seconds) => _progress = Math.Clamp(seconds, 0, Current.Duration);

    private void Expand(bool open)
    {
        _expanded = open;
        EnsureFrame();
        InvalidateVisual();
    }

    protected override void OnFrame(double dt)
    {
        _morph.Step(_expanded ? 1 : 0, dt);
        _art.Step(1, dt);
        _skipOffset.Step(0, dt);
        _time += dt;
        if (_playing)
        {
            _progress += dt;
            if (_progress >= Current.Duration)
            {
                _index = (_index + 1) % Tracks.Length;
                _progress = 0;
            }
        }
    }

    protected override bool NeedsFrame =>
        Math.Abs(_morph.Value - (_expanded ? 1 : 0)) > 0.002 ||
        Math.Abs(_skipOffset.Value) > 0.2 || Math.Abs(_art.Value - 1) > 0.002 ||
        _playing;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        ComputeLayout(Bounds.Size);
        var point = e.GetPosition(this);
        _pressPoint = point;

        if (_play.Contains(point))
        {
            TogglePlay();
            e.Handled = true;
            return;
        }

        if (_next.Contains(point))
        {
            Skip(1);
            e.Handled = true;
            return;
        }

        if (_morph.Value > 0.5 && _previous.Contains(point))
        {
            if (_progress > 3)
            {
                Seek(0);
            }
            else
            {
                Skip(-1);
            }

            e.Handled = true;
            return;
        }

        if (_morph.Value > 0.5 && _collapse.Contains(point))
        {
            Expand(false);
            e.Handled = true;
            return;
        }

        if (_morph.Value > 0.5 && _waveform.Inflate(10).Contains(point))
        {
            _scrubbing = true;
            e.Pointer.Capture(this);
            Seek((point.X - _waveform.X) / _waveform.Width * Current.Duration);
            e.Handled = true;
            return;
        }

        if (_morph.Value > 0.5 && _artRect.Contains(point))
        {
            _pointerDown = true;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (_morph.Value < 0.5 && _shell.Contains(point))
        {
            Expand(true);
            e.Handled = true;
            return;
        }
        if (_morph.Value >= 0.5 && !_shell.Contains(point))
        {
            Expand(false);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_scrubbing)
        {
            var x = e.GetPosition(this).X;
            Seek((x - _waveform.X) / _waveform.Width * Current.Duration);
            e.Handled = true;
            return;
        }

        if (!_pointerDown || e.Pointer.Captured != this)
        {
            return;
        }

        var dy = e.GetPosition(this).Y - _pressPoint.Y;
        _dragY = dy > 0 ? dy : dy * 0.3;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_scrubbing)
        {
            _scrubbing = false;
            if (e.Pointer.Captured == this)
            {
                e.Pointer.Capture(null);
            }

            return;
        }

        if (!_pointerDown)
        {
            return;
        }

        _pointerDown = false;
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        var dy = _dragY;
        _dragY = 0;
        if (dy > 70)
        {
            Expand(false);
        }

        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Space:
            case Key.Enter:
                TogglePlay();
                e.Handled = true;
                break;
            case Key.Left:
                Seek(_progress - 5);
                e.Handled = true;
                break;
            case Key.Right:
                Seek(_progress + 5);
                e.Handled = true;
                break;
            case Key.PageUp:
                Seek(_progress + 30);
                e.Handled = true;
                break;
            case Key.PageDown:
                Seek(_progress - 30);
                e.Handled = true;
                break;
            case Key.Home:
                Seek(0);
                e.Handled = true;
                break;
            case Key.End:
                Seek(Current.Duration);
                e.Handled = true;
                break;
            case Key.Up:
                Expand(true);
                e.Handled = true;
                break;
            case Key.Escape:
                Expand(false);
                e.Handled = true;
                break;
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        ComputeLayout(size);
        var t = Math.Clamp(_morph.Value, 0, 1);
        var surface = SpecialUi.Brush(ArcTokens.Surface);
        var border = SpecialUi.Brush(ArcTokens.Border);
        var foreground = SpecialUi.Color(ArcTokens.Foreground);
        var secondary = SpecialUi.Brush(ArcTokens.TextSecondary);
        var muted = SpecialUi.Brush(ArcTokens.TextMuted);

        // A soft ground shadow that tightens as the player grows.
        SpecialUi.Lift(context, _shell, SpecialUi.Lerp(22, 30, t), SpecialUi.Lerp(0.7, 1.5, t));
        SpecialUi.RoundRect(context, _shell, SpecialUi.Lerp(22, 30, t), surface);
        context.DrawRectangle(null, new Pen(border, 1), _shell.Deflate(0.5), SpecialUi.Lerp(22, 30, t), SpecialUi.Lerp(22, 30, t));

        var artworkOffset = _skipOffset.Value;
        var art = new Rect(
            _artRect.X + artworkOffset * (1 - t) * 0.5,
            _artRect.Y + _dragY * (1 - t),
            _artRect.Width,
            _artRect.Height);
        using (context.PushClip(new RoundedRect(art, SpecialUi.Lerp(10, 18, t))))
        {
            using (context.PushOpacity(Math.Clamp(_art.Value, 0, 1)))
            {
                SampleArt.Draw(context, art, Current.Seed, SpecialUi.Lerp(10, 18, t));
            }
        }

        context.DrawRectangle(null, new Pen(SpecialUi.Alpha(foreground, 0.10), 1), art, SpecialUi.Lerp(10, 18, t), SpecialUi.Lerp(10, 18, t));

        var fullOpacity = Math.Clamp((t - 0.35) / 0.65, 0, 1);
        var miniOpacity = Math.Clamp((0.55 - t) / 0.55, 0, 1);

        // Mini bar content.
        if (miniOpacity > 0.01)
        {
            using (context.PushOpacity(miniOpacity))
            {
                var title = SpecialUi.Text(Current.Title, 14, SpecialUi.Brush(ArcTokens.Foreground), FontWeight.Medium, _shell.Width - 200);
                var artist = SpecialUi.Text(Current.Artist, 11, secondary, FontWeight.Normal, _shell.Width - 200);
                context.DrawText(title, new Point(art.Right + 12, art.Y + 2));
                context.DrawText(artist, new Point(art.Right + 12, art.Y + 20));

                DrawPlayIcon(context, _play, foreground);
                SpecialIcons.DrawSkip(context, _next.Deflate(6), SpecialUi.Solid(foreground), true);

                var chevron = new Rect(_shell.Right - 4, _shell.Y - 24, 22, 22);
                SpecialIcons.Draw(context, "chevron-up", chevron, muted, 1.8);
            }
        }

        // Full player content.
        if (fullOpacity > 0.01)
        {
            using (context.PushOpacity(fullOpacity))
            {
                var title = SpecialUi.Text(Current.Title, 20, SpecialUi.Brush(ArcTokens.Foreground), FontWeight.Medium, _shell.Width - 48, 1, TextAlignment.Center);
                var artist = SpecialUi.Text(Current.Artist, 13, secondary, FontWeight.Normal, _shell.Width - 48, 1, TextAlignment.Center);
                var textY = _artRect.Bottom + 16;
                SpecialUi.DrawTextCentered(context, title, new Rect(_shell.X, textY, _shell.Width, title.Height));
                SpecialUi.DrawTextCentered(context, artist, new Rect(_shell.X, textY + title.Height + 2, _shell.Width, artist.Height));

                // Waveform scrubber: 54 bars.
                const int bars = 54;
                var gap = 3.0;
                var barWidth = Math.Max(1.5, (_waveform.Width - (bars - 1) * gap) / bars);
                var played = _progress / Current.Duration;
                for (var index = 0; index < bars; index++)
                {
                    var noise = 0.25 + SpecialUi.Noise(index * 17 + Current.Seed) * 0.75;
                    var height = Math.Max(3, noise * _waveform.Height);
                    var x = _waveform.X + index * (barWidth + gap);
                    var rect = new Rect(x, _waveform.Center.Y - height / 2, barWidth, height);
                    var active = (index + 0.5) / bars <= played;
                    var brush = active ? SpecialUi.Brush(ArcTokens.Accent) : SpecialUi.Alpha(ArcTokens.BorderStrong, 0.9);
                    context.DrawRectangle(brush, null, rect, barWidth / 2, barWidth / 2);
                }

                var elapsed = SpecialUi.Text(SpecialUi.Time(_progress), 11, muted);
                var total = SpecialUi.Text(SpecialUi.Time(Current.Duration), 11, muted);
                context.DrawText(elapsed, new Point(_waveform.X, _waveform.Bottom + 8));
                context.DrawText(total, new Point(_waveform.Right - total.Width, _waveform.Bottom + 8));

                // Three level bars that only move while the track plays.
                for (var index = 0; index < 3; index++)
                {
                    var phase = _time * 6 + index * 1.7;
                    var level = _playing ? 0.35 + 0.65 * Math.Abs(Math.Sin(phase)) : 0.22;
                    var height = 3 + level * 9;
                    var rect = new Rect(_waveform.X + 34 + index * 6, _waveform.Bottom + 2 + (12 - height), 3, height);
                    context.DrawRectangle(SpecialUi.Alpha(ArcTokens.Accent, _playing ? 0.9 : 0.5), null, rect, 1.5, 1.5);
                }

                DrawPlayIcon(context, _play, foreground);
                SpecialIcons.DrawSkip(context, _previous.Deflate(8), SpecialUi.Solid(foreground), false);
                SpecialIcons.DrawSkip(context, _next.Deflate(8), SpecialUi.Solid(foreground), true);

                SpecialUi.RoundRect(context, _collapse, 16, SpecialUi.Brush(ArcTokens.SurfaceMuted));
                SpecialIcons.Draw(context, "chevron-down", _collapse.Deflate(9), foreground, 2);
            }
        }

        if (ShowFocusRing)
        {
            SpecialUi.FocusRing(context, _morph.Value > 0.5 ? _waveform : _shell, 20);
        }
    }

    private void DrawPlayIcon(DrawingContext context, Rect rect, Color foreground)
    {
        SpecialUi.RoundRect(context, rect, rect.Width / 2, SpecialUi.Brush(ArcTokens.Accent));
        var icon = new Rect(rect.Center.X - 7, rect.Center.Y - 7, 14, 14);
        SpecialIcons.Draw(context, _playing ? "pause" : "play", icon, SpecialUi.Brush(ArcTokens.AccentForeground), 2.2);
    }
}
