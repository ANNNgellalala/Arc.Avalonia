using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Effects.Controls;

/// <summary>Token lookups, surface placement and small visual helpers shared by the effect controls.</summary>
internal static class ArcEffectVisuals
{
    public static readonly Easing Linear = new LinearEasing();

    public static IBrush Brush(string key) => ArcResources.Brush(key);

    public static double Size(string key) => ArcResources.Double(key);

    public static CornerRadius Radius(string key) => ArcResources.Radius(key);

    public static BoxShadows Shadow(string key) => ArcResources.Shadows(key);

    public static ArcIcon Icon(string? name, double size = 16) => new()
    {
        Icon = name,
        Size = size,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    public static TextBlock Text(string text, string classes, double? fontSize = null)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        foreach (var name in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            block.Classes.Add(name);
        }

        if (fontSize is { } size)
        {
            block.FontSize = size;
        }

        return block;
    }

    public static void SetOverlay(Canvas? canvas, Control? content)
    {
        if (canvas is null)
        {
            return;
        }

        canvas.Children.Clear();
        if (content is not null)
        {
            canvas.Children.Add(content);
        }
    }

    public static void Place(Control content, double x, double y)
    {
        Canvas.SetLeft(content, x);
        Canvas.SetTop(content, y);
    }

    /// <summary>Turns a chevron or a caret on the snappy spring; reduced motion swaps it instantly.</summary>
    public static void Rotate(Control control, bool flipped)
    {
        control.RenderTransformOrigin = RelativePoint.Center;
        if (control.Transitions is null && !ArcThemeManager.ReducedMotion)
        {
            control.Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = ArcMotion.SpringSnappyDuration,
                    Easing = ArcMotion.SpringSnappy
                }
            };
        }

        control.RenderTransform = TransformOperations.Parse(flipped ? "rotate(180deg)" : "rotate(0deg)");
    }

    /// <summary>Fades and lifts a face in place with the standard token tween.</summary>
    public static void Fade(Control control, bool visible)
    {
        control.IsVisible = visible;
        control.Opacity = visible ? 1 : 0;
    }

    /// <summary>Measures a control that is being switched in, so the frame can spring to its height.</summary>
    public static double MeasureHeight(Control control, double width)
    {
        if (width <= 0)
        {
            return 0;
        }

        control.InvalidateMeasure();
        control.Measure(new Size(width, double.PositiveInfinity));
        return control.DesiredSize.Height;
    }
}

/// <summary>
/// A frame-clock tween used where the value is not an Avalonia transition target (blur radius, clip
/// travel, a whole sequence of staggered words). Reduced motion jumps straight to the end state.
/// </summary>
internal sealed class ArcTween
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private readonly TimeSpan _duration;
    private readonly Easing _easing;
    private readonly Action<double> _apply;
    private readonly Action? _completed;
    private bool _fired;

    private ArcTween(TimeSpan duration, Easing easing, Action<double> apply, Action? completed)
    {
        _duration = duration;
        _easing = easing;
        _apply = apply;
        _completed = completed;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnTick);
    }

    public static ArcTween? Run(TimeSpan duration, Easing? easing, Action<double> apply, Action? completed = null)
    {
        if (ArcThemeManager.ReducedMotion || duration <= TimeSpan.Zero)
        {
            apply(1);
            completed?.Invoke();
            return null;
        }

        var tween = new ArcTween(duration, easing ?? ArcMotion.EaseStandard, apply, completed);
        tween._clock.Start();
        tween._apply(0);
        tween._timer.Start();
        return tween;
    }

    public void Stop()
    {
        _timer.Stop();
        _fired = true;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var progress = _clock.Elapsed.TotalSeconds / _duration.TotalSeconds;
        if (progress >= 1)
        {
            _timer.Stop();
            _apply(1);
            if (!_fired)
            {
                _fired = true;
                _completed?.Invoke();
            }

            return;
        }

        _apply(_easing.Ease(progress));
    }
}

/// <summary>Escape and outside-press dismissal for the page-hosted surfaces in this project.</summary>
internal static class ArcEffectDismiss
{
    public static IDisposable Attach(Visual owner, Action dismiss, Func<bool>? outsidePress = null)
    {
        var top = TopLevel.GetTopLevel(owner);
        return top is null ? new Noop() : new Handle(top, owner, dismiss, outsidePress);
    }

    private sealed class Noop : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class Handle : IDisposable
    {
        private readonly TopLevel _top;
        private readonly Visual _owner;
        private readonly Action _dismiss;
        private readonly Func<bool>? _outsidePress;

        public Handle(TopLevel top, Visual owner, Action dismiss, Func<bool>? outsidePress)
        {
            _top = top;
            _owner = owner;
            _dismiss = dismiss;
            _outsidePress = outsidePress;
            if (outsidePress is not null)
            {
                _top.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            }

            _top.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_outsidePress is null || !_outsidePress())
            {
                return;
            }

            if (e.Source is Visual source && IsWithin(source, _owner))
            {
                return;
            }

            _dismiss();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _dismiss();
            }
        }

        public void Dispose()
        {
            if (_outsidePress is not null)
            {
                _top.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            }

            _top.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        }

        private static bool IsWithin(Visual source, Visual owner)
        {
            for (Visual? visual = source; visual is not null; visual = visual.GetVisualParent())
            {
                if (ReferenceEquals(visual, owner))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

/// <summary>
/// One-shot (or repeating) viewport watcher. Arc triggers on 45% visibility; Avalonia has no
/// IntersectionObserver, so this watches the enclosing <see cref="ScrollViewer"/> offset and the
/// control's own layout, and falls back to first layout when the page is not scrolled at all.
/// </summary>
internal sealed class ArcViewport : IDisposable
{
    private readonly Control _control;
    private readonly Action _onVisible;
    private readonly Action? _onHidden;
    private readonly bool _once;
    private readonly double _threshold;
    private ScrollViewer? _scroll;
    private bool _inside;
    private bool _done;
    private bool _disposed;

    private ArcViewport(Control control, Action onVisible, Action? onHidden, bool once, double threshold)
    {
        _control = control;
        _onVisible = onVisible;
        _onHidden = onHidden;
        _once = once;
        _threshold = Math.Clamp(threshold, 0, 1);
        _scroll = control.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += OnScrollChanged;
        }

        control.LayoutUpdated += OnLayoutUpdated;
    }

    public static ArcViewport Watch(Control control, Action onVisible, bool once = true, double threshold = 0, Action? onHidden = null)
    {
        var watcher = new ArcViewport(control, onVisible, onHidden, once, threshold);
        return watcher;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => Evaluate();

    private void OnLayoutUpdated(object? sender, EventArgs e) => Evaluate();

    private void Evaluate()
    {
        if (_disposed || (_once && _done))
        {
            return;
        }

        if (_control.Bounds.Height <= 0)
        {
            return;
        }

        var inside = IsInsideViewport();
        if (inside == _inside)
        {
            return;
        }

        _inside = inside;
        if (inside)
        {
            _done = true;
            _onVisible();
            if (_once)
            {
                Detach();
            }
        }
        else
        {
            _onHidden?.Invoke();
        }
    }

    private bool IsInsideViewport()
    {
        if (_scroll is null || _control.TranslatePoint(new Point(0, 0), _scroll) is not { } origin)
        {
            return true;
        }

        var viewport = new Rect(_scroll.Bounds.Size);
        var own = new Rect(origin, _control.Bounds.Size);
        if (!own.Intersects(viewport))
        {
            return false;
        }

        if (_threshold <= 0)
        {
            return true;
        }

        var overlap = Math.Min(own.Bottom, viewport.Bottom) - Math.Max(own.Top, viewport.Top);
        return overlap / Math.Max(1, own.Height) >= _threshold;
    }

    private void Detach()
    {
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
            _scroll = null;
        }

        _control.LayoutUpdated -= OnLayoutUpdated;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Detach();
    }
}

/// <summary>Shared helpers for building the per-word or per-character layers of a text effect.</summary>
internal static class ArcText
{
    public static double FontSizeOf(ArcTextLevel level) => level switch
    {
        ArcTextLevel.H1 => ArcEffectVisuals.Size(ArcTokens.Text4xl),
        ArcTextLevel.H2 => ArcEffectVisuals.Size(ArcTokens.Text3xl),
        ArcTextLevel.H3 => ArcEffectVisuals.Size(ArcTokens.Text2xl),
        ArcTextLevel.Paragraph => ArcEffectVisuals.Size(ArcTokens.TextBase),
        _ => ArcEffectVisuals.Size(ArcTokens.TextSm)
    };

    public static string ClassOf(ArcTextLevel level) => level switch
    {
        ArcTextLevel.H1 => "arc-title",
        ArcTextLevel.H2 => "arc-title-sm",
        ArcTextLevel.H3 => "arc-subtitle",
        ArcTextLevel.Paragraph => "arc-body",
        _ => "arc-body-sm"
    };

    public static TextBlock Unit(string text, ArcTextLevel level)
    {
        var block = ArcEffectVisuals.Text(text, ClassOf(level), FontSizeOf(level));
        block.TextWrapping = TextWrapping.NoWrap;
        block.VerticalAlignment = VerticalAlignment.Top;
        return block;
    }

    public static string[] Lines(string? text) =>
        string.IsNullOrEmpty(text) ? Array.Empty<string>() : text.Replace("\r\n", "\n").Split('\n');

    /// <summary>Splits one line into word or character runs, keeping spaces as word separators.</summary>
    public static IReadOnlyList<string> Runs(string line, ArcTextUnit unit)
    {
        if (unit == ArcTextUnit.Character)
        {
            return line.Select(c => c.ToString()).ToArray();
        }

        var runs = new List<string>();
        var start = -1;
        for (var i = 0; i < line.Length; i++)
        {
            if (char.IsWhiteSpace(line[i]))
            {
                if (start >= 0)
                {
                    runs.Add(line[start..i]);
                    start = -1;
                }
            }
            else if (start < 0)
            {
                start = i;
            }
        }

        if (start >= 0)
        {
            runs.Add(line[start..]);
        }

        return runs;
    }

    /// <summary>The space advance between words, read off the resolved font size.</summary>
    public static double SpaceWidth(ArcTextLevel level) => Math.Round(FontSizeOf(level) * 0.26, 2);
}
