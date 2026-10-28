using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialAIExplainers.Controls;

/// <summary>
/// Shared frame for the AI explainer illustrations. Every illustration is one custom drawn control with a
/// single normalised phase: at any time the picture is a pure function of that phase, which keeps the loop
/// seamless and lets the reduced-motion branch show one honest at-rest frame (the poster phase) instead of
/// pretending to animate. A small transport button in the corner pauses the clock, Space toggles it, and
/// each control adds its own hover and keyboard detail.
/// </summary>
/// <remarks>
/// The drawings are recreated independently from Arc's public component pages and markdown. They render
/// with DrawingContext only: no SVG, no images, no external libraries. Colours come from the token set so
/// both themes and all eight accents work without extra wiring.
/// </remarks>
public abstract class ArcExplainer : Control
{
    private readonly DispatcherTimer _timer;
    private double _time;
    private bool _playing = true;
    private bool _pointerOver;
    private bool _transportOver;
    private bool _keyboardFocus;
    private Rect _transport = default;

    protected ArcExplainer()
    {
        ClipToBounds = true;
        Focusable = true;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => OnClockTick();
    }

    /// <summary>Height used when the page does not set one.</summary>
    protected virtual double DefaultArtHeight => 470;

    /// <summary>Seconds for one full loop of the story.</summary>
    protected virtual double CycleSeconds => 12;

    /// <summary>The phase shown under reduced motion: a legible, representative moment.</summary>
    protected virtual double PosterPhase => 0.4;

    protected bool Reduced => ArcThemeManager.ReducedMotion;

    protected bool IsPlaying => _playing;

    protected bool PointerOver => _pointerOver;

    protected bool KeyboardFocus => _keyboardFocus;

    /// <summary>Normalised story position in 0..1.</summary>
    protected double Phase
    {
        get
        {
            var phase = Reduced ? PosterPhase : (_time / CycleSeconds) % 1.0;
            return phase < 0 ? phase + 1 : phase;
        }
    }

    /// <summary>Story position in seconds (phase × cycle).</summary>
    protected double Story => Phase * CycleSeconds;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 960 : availableSize.Width;
        var height = double.IsNaN(Height) ? DefaultArtHeight : Height;
        return new Size(Math.Max(0, width), Math.Max(0, height));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    private void OnClockTick()
    {
        var clockMoved = false;
        if (!Reduced && _playing)
        {
            _time += _timer.Interval.TotalSeconds;
            clockMoved = true;
        }

        var contentMoved = OnTick(_timer.Interval.TotalSeconds);
        if (clockMoved || contentMoved)
        {
            InvalidateVisual();
        }
    }

    /// <summary>Per-tick hook for interaction driven motion (flicks, snaps). Return true to repaint.</summary>
    protected virtual bool OnTick(double seconds) => false;

    public void SetPaused(bool paused)
    {
        if (_playing == !paused)
        {
            return;
        }

        _playing = !paused;
        InvalidateVisual();
    }

    public void TogglePlay()
    {
        if (Reduced)
        {
            return;
        }

        _playing = !_playing;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width < 24 || height < 24)
        {
            return;
        }

        DrawBackdrop(context, width, height);
        DrawScene(context, width, height, Phase);
        DrawTransport(context, width, height);

        if (_keyboardFocus && IsFocused)
        {
            var ring = new Rect(2, 2, Math.Max(0, width - 4), Math.Max(0, height - 4));
            context.DrawRectangle(null, new Pen(Ink.B(ArcTokens.FocusOutline), 2), ring, 24, 24);
        }
    }

    /// <summary>The figure surface. Override for a staged or muted backdrop.</summary>
    protected virtual void DrawBackdrop(DrawingContext context, double width, double height)
    {
        Ink.Card(
            context,
            new Rect(0.5, 0.5, width - 1, height - 1),
            ArcResources.Radius(ArcTokens.RadiusPanel).TopLeft,
            Ink.B(ArcTokens.Surface),
            Ink.B(ArcTokens.Border));
    }

    /// <summary>Draws the illustration at the given phase. Width and height are the control's bounds.</summary>
    protected abstract void DrawScene(DrawingContext context, double width, double height, double phase);

    private void DrawTransport(DrawingContext context, double width, double height)
    {
        if (Reduced)
        {
            _transport = default;
            return;
        }

        const double size = 28;
        var rect = new Rect(width - 14 - size, height - 14 - size, size, size);
        _transport = rect;
        var hot = _transportOver || (_keyboardFocus && IsFocused);
        context.DrawRectangle(
            hot ? Ink.B(ArcTokens.SurfaceRaised) : Ink.B(ArcTokens.Surface),
            new Pen(hot ? Ink.B(ArcTokens.BorderStrong) : Ink.B(ArcTokens.Border), 1),
            rect,
            14,
            14);
        Ink.Icon(
            context,
            _playing ? "pause" : "play",
            rect.Center.X,
            rect.Center.Y,
            13,
            hot ? Ink.B(ArcTokens.Foreground) : Ink.B(ArcTokens.TextSecondary),
            1.6);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointerOver = true;
        var point = e.GetPosition(this);
        var transportOver = _transport.Contains(point);
        var changed = transportOver != _transportOver;
        _transportOver = transportOver;
        if (OnHover(point))
        {
            changed = true;
        }

        if (changed)
        {
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerOver = false;
        _transportOver = false;
        OnHover(null);
        InvalidateVisual();
    }

    /// <summary>Hover hook. Null means the pointer left the control. Return true when the picture changed.</summary>
    protected virtual bool OnHover(Point? point) => false;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Focus(NavigationMethod.Pointer);
        _keyboardFocus = false;
        var point = e.GetPosition(this);
        if (_transport.Contains(point))
        {
            TogglePlay();
            e.Handled = true;
            return;
        }

        if (OnPress(point, e))
        {
            e.Handled = true;
            InvalidateVisual();
        }
    }

    /// <summary>Pointer press inside the figure. Return true when the press was consumed.</summary>
    protected virtual bool OnPress(Point point, PointerPressedEventArgs e) => false;

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (OnRelease(e.GetPosition(this), e))
        {
            e.Handled = true;
            InvalidateVisual();
        }
    }

    protected virtual bool OnRelease(Point point, PointerReleasedEventArgs e) => false;

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (OnWheel(e.GetPosition(this), e.Delta.Y, e))
        {
            e.Handled = true;
            InvalidateVisual();
        }
    }

    protected virtual bool OnWheel(Point point, double delta, PointerWheelEventArgs e) => false;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        _keyboardFocus = true;
        if (!Reduced && e.Key == Key.Space)
        {
            TogglePlay();
            e.Handled = true;
            InvalidateVisual();
            return;
        }

        if (OnKey(e.Key))
        {
            e.Handled = true;
            InvalidateVisual();
            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>Keyboard detail beyond the shared Space toggle. Arrows, Home and End live here.</summary>
    protected virtual bool OnKey(Key key) => false;

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _keyboardFocus = false;
        InvalidateVisual();
    }

    /// <summary>Sets the accessible name and help text every illustration needs.</summary>
    protected void Describe(string name, string help)
    {
        AutomationProperties.SetName(this, name);
        AutomationProperties.SetHelpText(this, help);
    }

    /// <summary>Resolves the series brush for a topic or lane index.</summary>
    protected static IBrush Series(int index) => index switch
    {
        0 => Ink.B(ArcTokens.Series1),
        1 => Ink.B(ArcTokens.Series2),
        2 => Ink.B(ArcTokens.Series3),
        _ => Ink.B(ArcTokens.Series4)
    };

    /// <summary>Draws a quiet section heading inside the figure.</summary>
    protected static void Caption(DrawingContext context, string text, double x, double y, Align align = Align.Left, double maxWidth = double.PositiveInfinity)
        => Ink.Label(context, text, x, y, 11, Ink.B(ArcTokens.TextMuted), FontWeight.Medium, align, maxWidth);
}
