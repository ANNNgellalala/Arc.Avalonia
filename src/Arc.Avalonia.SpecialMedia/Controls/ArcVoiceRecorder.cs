using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMedia.Controls;

/// <summary>
/// Arc's voice recorder: a voice note composer and thread. Tap the mic to record into a live bar with a
/// scrolling waveform and timer, stop into a review you can play and scrub, then send the take into the
/// thread as a voice bubble. Audio is simulated; the demo seeds a take in review so the whole loop is visible.
/// </summary>
public sealed class ArcVoiceRecorder : ArcSpecialCanvas
{
    private enum Phase
    {
        Idle,
        Recording,
        Review
    }

    private sealed class Message
    {
        public string From { get; init; } = "You";
        public bool Incoming { get; init; }
        public double Duration { get; init; }
        public int Seed { get; init; }
        public string Meta { get; init; } = "Just now";
    }

    private readonly List<Message> _messages = new()
    {
        new Message { From = "Sam", Incoming = true, Duration = 7.2, Seed = 3, Meta = "9:41 AM" },
        new Message { From = "You", Duration = 12.4, Seed = 8, Meta = "9:42 AM" },
        new Message { From = "Sam", Incoming = true, Duration = 4.6, Seed = 11, Meta = "9:43 AM" },
        new Message { From = "You", Duration = 21.0, Seed = 7, Meta = "9:44 AM" }
    };

    private Phase _phase = Phase.Review;
    private double _takeDuration = 8.4;
    private int _takeSeed = 5;
    private double _recorded;
    private double _playhead = 2.4;
    private bool _playing;
    private int _playingMessage = -1;
    private double _messagePlayhead;
    private bool _scrubbing;
    private int _focus;
    private double _time;
    private Rect _composer;
    private Rect _waveform;
    private Rect _primary;
    private Rect _secondary;
    private Rect _tertiary;
    private readonly List<(Rect Rect, int Index)> _messageButtons = new();

    public ArcVoiceRecorder()
    {
        SpecialUi.Name(
            this,
            "Voice recorder",
            "A voice note composer. Space or Enter starts, stops, plays or sends depending on the focused control; Escape discards a take in review.");
    }

    protected override double DefaultWidth => 680;

    protected override double DefaultHeight => 442;

    private double TakeElapsed => _phase == Phase.Recording ? _recorded : _takeDuration;

    private void StartRecording()
    {
        _phase = Phase.Recording;
        _recorded = 0;
        _takeSeed = 3 + (int)(_time * 7);
        EnsureFrame();
        InvalidateVisual();
    }

    private void StopRecording()
    {
        _takeDuration = Math.Max(1.2, _recorded);
        _phase = Phase.Review;
        _playhead = 0;
        _playing = false;
        EnsureFrame();
        InvalidateVisual();
    }

    private void Discard()
    {
        _phase = Phase.Idle;
        _playing = false;
        _playhead = 0;
        InvalidateVisual();
    }

    private void Send()
    {
        _messages.Add(new Message
        {
            From = "You",
            Incoming = false,
            Duration = Math.Max(1.2, _takeDuration),
            Seed = _takeSeed,
            Meta = "Just now"
        });
        if (_messages.Count > 5)
        {
            _messages.RemoveAt(0);
        }

        _phase = Phase.Idle;
        _playing = false;
        InvalidateVisual();
    }

    private void ToggleReviewPlayback()
    {
        _playing = !_playing;
        if (_playing && _playhead >= _takeDuration - 0.05)
        {
            _playhead = 0;
        }

        EnsureFrame();
    }

    protected override void OnFrame(double dt)
    {
        _time += dt;
        if (_phase == Phase.Recording)
        {
            _recorded += dt;
        }
        else if (_playing)
        {
            _playhead += dt;
            if (_playhead >= _takeDuration)
            {
                _playhead = _takeDuration;
                _playing = false;
            }
        }

        if (_playingMessage >= 0)
        {
            _messagePlayhead += dt;
            var duration = _messages[_playingMessage].Duration;
            if (_messagePlayhead >= duration)
            {
                _playingMessage = -1;
                _messagePlayhead = 0;
            }
        }
    }

    protected override bool NeedsFrame =>
        _phase == Phase.Recording ||
        _playing ||
        _playingMessage >= 0;

    private void Layout(Size size)
    {
        _composer = new Rect(0, size.Height - 104, size.Width, 88);
        var button = new Rect(_composer.X + 12, _composer.Y + 20, 48, 48);
        _primary = button;
        _waveform = new Rect(button.Right + 18, _composer.Y + 26, Math.Max(120, _secondary.X - button.Right - 70), 36);
        _tertiary = new Rect(size.Width - 108, _composer.Y + 24, 92, 40);
        _secondary = new Rect(size.Width - 160, _composer.Y + 26, 36, 36);
    }

    private void ActivateFocus()
    {
        switch (_phase)
        {
            case Phase.Idle:
                StartRecording();
                break;
            case Phase.Recording:
                if (_focus == 0)
                {
                    StopRecording();
                }
                else
                {
                    Discard();
                }

                break;
            case Phase.Review:
                if (_focus == 0)
                {
                    ToggleReviewPlayback();
                }
                else if (_focus == 1)
                {
                    Discard();
                }
                else
                {
                    Send();
                }

                break;
        }
    }

    private int FocusCount => _phase switch
    {
        Phase.Recording => 2,
        Phase.Review => 3,
        _ => 1
    };

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        Layout(Bounds.Size);
        var point = e.GetPosition(this);

        foreach (var (rect, index) in _messageButtons)
        {
            if (!rect.Contains(point))
            {
                continue;
            }

            if (_playingMessage == index)
            {
                _playingMessage = -1;
                _messagePlayhead = 0;
            }
            else
            {
                _playingMessage = index;
                _messagePlayhead = 0;
                EnsureFrame();
            }

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_phase == Phase.Recording)
        {
            if (_primary.Contains(point))
            {
                _focus = 0;
                StopRecording();
            }
            else if (_secondary.Contains(point))
            {
                _focus = 1;
                Discard();
            }

            e.Handled = true;
            return;
        }

        if (_phase == Phase.Review)
        {
            if (_primary.Contains(point))
            {
                _focus = 0;
                ToggleReviewPlayback();
            }
            else if (_secondary.Contains(point))
            {
                _focus = 1;
                Discard();
            }
            else if (_tertiary.Contains(point))
            {
                _focus = 2;
                Send();
            }
            else if (_waveform.Inflate(8).Contains(point))
            {
                _scrubbing = true;
                e.Pointer.Capture(this);
                Scrub(point.X);
            }

            e.Handled = true;
            return;
        }

        if (_primary.Contains(point))
        {
            _focus = 0;
            StartRecording();
        }

        e.Handled = true;
    }

    private void Scrub(double x)
    {
        var ratio = Math.Clamp((x - _waveform.X) / Math.Max(1, _waveform.Width), 0, 1);
        _playhead = ratio * _takeDuration;
        _playing = false;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_scrubbing)
        {
            return;
        }

        Scrub(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_scrubbing)
        {
            return;
        }

        _scrubbing = false;
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Left:
                _focus = (_focus - 1 + FocusCount) % FocusCount;
                e.Handled = true;
                break;
            case Key.Right:
                _focus = (_focus + 1) % FocusCount;
                e.Handled = true;
                break;
            case Key.Space:
            case Key.Enter:
                ActivateFocus();
                e.Handled = true;
                break;
            case Key.Escape:
                if (_phase != Phase.Idle)
                {
                    Discard();
                }

                e.Handled = true;
                break;
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        Layout(size);
        _messageButtons.Clear();
        var foreground = SpecialUi.Color(ArcTokens.Foreground);
        var secondary = SpecialUi.Color(ArcTokens.TextSecondary);
        var muted = SpecialUi.Color(ArcTokens.TextMuted);
        var accent = SpecialUi.Color(ArcTokens.Accent);
        var surface = SpecialUi.Brush(ArcTokens.Surface);
        var border = SpecialUi.Brush(ArcTokens.Border);
        var background = SpecialUi.Color(ArcTokens.Background);

        var header = SpecialUi.Text("Thread  ·  Design notes", 13, SpecialUi.Solid(secondary), FontWeight.Medium);
        context.DrawText(header, new Point(0, 0));

        // Seed bubbles.
        var y = 34.0;
        for (var index = 0; index < _messages.Count; index++)
        {
            var message = _messages[index];
            y = DrawMessage(context, size, message, index, y, foreground, secondary, muted, accent);
            y += 12;
        }

        // Composer.
        SpecialUi.Lift(context, _composer, 26, 0.8);
        SpecialUi.RoundRect(context, _composer, 26, surface);
        context.DrawRectangle(null, new Pen(border, 1), _composer.Deflate(0.5), 26, 26);

        if (_phase == Phase.Idle)
        {
            SpecialUi.RoundRect(context, _primary, 24, SpecialUi.Brush(ArcTokens.Accent));
            SpecialIcons.Draw(context, "mic", _primary.Deflate(14), SpecialUi.Brush(ArcTokens.AccentForeground), 1.8);
            var caption = SpecialUi.Text("Record a voice note", 14, SpecialUi.Solid(foreground), FontWeight.Medium);
            context.DrawText(caption, new Point(_primary.Right + 16, _composer.Y + 26));
            var hint = SpecialUi.Text("Tap the mic, then stop to review before sending.", 12, SpecialUi.Solid(muted));
            context.DrawText(hint, new Point(_primary.Right + 16, _composer.Y + 48));
        }
        else if (_phase == Phase.Recording)
        {
            var pulse = 0.55 + 0.35 * Math.Sin(_time * 5);
            context.DrawEllipse(SpecialUi.Solid(ArcColor.WithAlpha(SpecialUi.Color(ArcTokens.Danger), pulse)), null, new Point(_primary.Center.X, _primary.Center.Y), 12, 12);
            var timer = SpecialUi.Text(SpecialUi.Time(_recorded), 16, SpecialUi.Solid(foreground));
            context.DrawText(timer, new Point(_waveform.Right + 8, _waveform.Center.Y - timer.Height / 2));

            var live = _waveform;
            const int bars = 44;
            var gap = 2.5;
            var barWidth = Math.Max(1.5, (live.Width - (bars - 1) * gap) / bars);
            for (var index = 0; index < bars; index++)
            {
                var value = 0.2 + SpecialUi.Noise((int)(_recorded * 12) * 53 + index * 11) * 0.8;
                var height = Math.Max(2, value * live.Height);
                var rect = new Rect(live.X + index * (barWidth + gap), live.Center.Y - height / 2, barWidth, height);
                context.DrawRectangle(SpecialUi.Solid(ArcColor.WithAlpha(accent, 0.55 + 0.45 * value)), null, rect, barWidth / 2, barWidth / 2);
            }

            SpecialUi.RoundRect(context, _primary, 24, SpecialUi.Brush(ArcTokens.SurfaceMuted));
            SpecialIcons.Draw(context, "square", _primary.Deflate(15), SpecialUi.Brush(ArcTokens.Danger), 2);
            SpecialUi.RoundRect(context, _secondary, 18, SpecialUi.Brush(ArcTokens.SurfaceMuted));
            SpecialIcons.Draw(context, "x", _secondary.Deflate(9), SpecialUi.Solid(muted), 1.7);
            var hint = SpecialUi.Text("Recording  ·  stop to review", 11, SpecialUi.Solid(muted));
            SpecialUi.DrawTextRight(context, hint, new Rect(0, _composer.Y + 62, _secondary.X - 8, 18));
        }
        else
        {
            SpecialUi.RoundRect(context, _primary, 24, SpecialUi.Brush(ArcTokens.Accent));
            SpecialIcons.Draw(context, _playing ? "pause" : "play", _primary.Deflate(14), SpecialUi.Brush(ArcTokens.AccentForeground), 2);

            var progress = _takeDuration <= 0 ? 0 : _playhead / _takeDuration;
            DrawWaveform(context, _waveform, _takeSeed, 0.15, progress, accent, ArcColor.WithAlpha(foreground, 0.30));
            var duration = SpecialUi.Text(SpecialUi.Time(_takeDuration), 12, SpecialUi.Solid(foreground), FontWeight.Medium);
            context.DrawText(duration, new Point(_waveform.Right + 10, _waveform.Center.Y - duration.Height / 2));

            SpecialUi.RoundRect(context, _secondary, 18, SpecialUi.Brush(ArcTokens.SurfaceMuted));
            SpecialIcons.Draw(context, "trash-2", _secondary.Deflate(9), SpecialUi.Solid(muted), 1.7);

            SpecialUi.RoundRect(context, _tertiary, 20, SpecialUi.Brush(ArcTokens.Accent));
            var sendIcon = new Rect(_tertiary.X + 12, _tertiary.Y + 10, 20, 20);
            SpecialIcons.Draw(context, "send", sendIcon, SpecialUi.Brush(ArcTokens.AccentForeground), 1.8);
            var sendText = SpecialUi.Text("Send", 13, SpecialUi.Brush(ArcTokens.AccentForeground), FontWeight.Medium);
            context.DrawText(sendText, new Point(sendIcon.Right + 6, _tertiary.Center.Y - sendText.Height / 2));
        }

        if (ShowFocusRing)
        {
            var region = _phase switch
            {
                Phase.Review => _focus == 0 ? _primary : _focus == 1 ? _secondary : _tertiary,
                Phase.Recording => _focus == 0 ? _primary : _secondary,
                _ => _primary
            };
            SpecialUi.FocusRing(context, region, 22);
        }
    }

    private double DrawMessage(
        DrawingContext context,
        Size size,
        Message message,
        int index,
        double y,
        Color foreground,
        Color secondary,
        Color muted,
        Color accent)
    {
        var bubbleWidth = Math.Min(360, size.Width - 120);
        var height = 64.0;
        var x = message.Incoming ? 40 : size.Width - bubbleWidth;
        var bubble = new Rect(x, y, bubbleWidth, height);
        var fill = message.Incoming ? SpecialUi.Brush(ArcTokens.Surface) : SpecialUi.Brush(ArcTokens.AccentSubtle);
        SpecialUi.RoundRect(context, bubble, 20, fill);
        context.DrawRectangle(null, message.Incoming ? new Pen(SpecialUi.Brush(ArcTokens.Border), 1) : null, bubble.Deflate(0.5), 20, 20);

        if (message.Incoming)
        {
            var avatar = new Rect(0, y + 6, 32, 32);
            context.DrawEllipse(SpecialUi.Brush(ArcTokens.SurfaceMuted), null, avatar.Center, 16, 16);
            SpecialUi.DrawTextCentered(context, SpecialUi.Text(message.From.Substring(0, 1), 12, SpecialUi.Solid(secondary), FontWeight.Medium), avatar);
        }

        var button = new Rect(bubble.X + 10, bubble.Y + 14, 36, 36);
        _messageButtons.Add((button, index));
        var playing = _playingMessage == index;
        context.DrawEllipse(SpecialUi.Solid(ArcColor.WithAlpha(playing ? accent : foreground, playing ? 0.16 : 0.06)), null, button.Center, 18, 18);
        SpecialIcons.Draw(context, playing ? "pause" : "play", button.Deflate(10), SpecialUi.Solid(playing ? accent : foreground), 2);

        var wave = new Rect(button.Right + 10, bubble.Y + 18, bubble.Width - 130, 30);
        var progress = playing ? _messagePlayhead / Math.Max(0.1, message.Duration) : 0;
        DrawWaveform(context, wave, message.Seed, 0.18, progress, accent, ArcColor.WithAlpha(foreground, 0.5));

        var duration = SpecialUi.Text(SpecialUi.Time(message.Duration), 11, SpecialUi.Solid(secondary));
        context.DrawText(duration, new Point(bubble.Right - 56, bubble.Y + 20));
        var meta = SpecialUi.Text($"{message.From}  ·  {message.Meta}", 10, SpecialUi.Solid(muted));
        context.DrawText(meta, new Point(bubble.X + 12, bubble.Bottom - 18));
        return bubble.Bottom;
    }


    private static void DrawWaveform(DrawingContext context, Rect rect, int seed, double minimum, double progress, Color accent, Color rest)
    {
        const int bars = 34;
        var gap = 2.4;
        var width = Math.Max(1.5, (rect.Width - (bars - 1) * gap) / bars);
        for (var index = 0; index < bars; index++)
        {
            var value = minimum + SpecialUi.Noise(seed * 31 + index * 17) * (1 - minimum);
            var height = Math.Max(2, value * rect.Height);
            var bar = new Rect(rect.X + index * (width + gap), rect.Center.Y - height / 2, width, height);
            var played = (index + 0.5) / bars <= progress;
            context.DrawRectangle(SpecialUi.Solid(played ? accent : rest), null, bar, width / 2, width / 2);
        }
    }
}
