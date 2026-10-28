using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>A minimum and maximum value pair.</summary>
public readonly record struct ArcRange(double Start, double End)
{
    public override string ToString() => Start.ToString(CultureInfo.InvariantCulture) + " - " +
                                         End.ToString(CultureInfo.InvariantCulture);
}

public sealed class ArcRangeValueEventArgs : EventArgs
{
    public ArcRangeValueEventArgs(ArcRange value) => Value = value;

    public ArcRange Value { get; }
}

/// <summary>
/// Arc's range histogram slider: two thumbs over a histogram of the data, bars tinted inside the
/// range, a value readout, and thumbs that settle on bar edges when released.
/// </summary>
public class ArcRangeHistogramSlider : Control
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcRangeHistogramSlider, string?>(nameof(Label));

    public static readonly StyledProperty<IList<double>?> DataProperty =
        AvaloniaProperty.Register<ArcRangeHistogramSlider, IList<double>?>(nameof(Data));

    public static readonly StyledProperty<ArcRange> ValueProperty =
        AvaloniaProperty.Register<ArcRangeHistogramSlider, ArcRange>(nameof(Value), new ArcRange(double.NaN, double.NaN));

    public static readonly StyledProperty<double> MinProperty =
        AvaloniaProperty.Register<ArcRangeHistogramSlider, double>(nameof(Min), double.NaN);

    public static readonly StyledProperty<double> MaxProperty =
        AvaloniaProperty.Register<ArcRangeHistogramSlider, double>(nameof(Max), double.NaN);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<ArcRangeHistogramSlider, double>(nameof(Step), 1d);

    public static readonly StyledProperty<int> BinsProperty =
        AvaloniaProperty.Register<ArcRangeHistogramSlider, int>(nameof(Bins), 32);

    public static readonly StyledProperty<bool> SnapToBinsProperty =
        AvaloniaProperty.Register<ArcRangeHistogramSlider, bool>(nameof(SnapToBins), true);

    public static readonly StyledProperty<int> MinStepsBetweenThumbsProperty =
        AvaloniaProperty.Register<ArcRangeHistogramSlider, int>(nameof(MinStepsBetweenThumbs), 1);

    public static readonly StyledProperty<IList<string>?> ThumbLabelsProperty =
        AvaloniaProperty.Register<ArcRangeHistogramSlider, IList<string>?>(nameof(ThumbLabels));

    private const double HeaderHeight = 24;
    private const double ChartTop = 30;
    private const double ChartHeight = 52;
    private const double TrackCenter = ChartTop + ChartHeight + 12;
    private const double ThumbRadius = 9;

    private int _active;
    private int _dragging = -1;
    private bool _pointerFocus;
    private double _strain;
    private double _startDisplay;
    private double _endDisplay;

    static ArcRangeHistogramSlider()
    {
        AffectsRender<ArcRangeHistogramSlider>(
            LabelProperty, ValueProperty, MinProperty, MaxProperty, StepProperty,
            BinsProperty, DataProperty, SnapToBinsProperty, ThumbLabelsProperty);
        ValueProperty.Changed.AddClassHandler<ArcRangeHistogramSlider>((slider, _) => slider.OnValueChanged());
        DataProperty.Changed.AddClassHandler<ArcRangeHistogramSlider>((slider, _) => slider.SyncDisplay(animate: false));
        LabelProperty.Changed.AddClassHandler<ArcRangeHistogramSlider>((slider, _) => slider.UpdateAutomation());
        BinsProperty.Changed.AddClassHandler<ArcRangeHistogramSlider>((slider, _) => slider.InvalidateVisual());
    }

    public ArcRangeHistogramSlider()
    {
        Focusable = true;
    }

    /// <summary>Raised on every change while dragging or pressing keys.</summary>
    public event EventHandler<ArcRangeValueEventArgs>? ValueChanged;

    /// <summary>Raised once a drag is released or a key changes the range.</summary>
    public event EventHandler<ArcRangeValueEventArgs>? ValueCommitted;

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Raw values the histogram counts, such as every listing's price.</summary>
    public IList<double>? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public ArcRange Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Lower bound; NaN derives it from the data.</summary>
    public double Min
    {
        get => GetValue(MinProperty);
        set => SetValue(MinProperty, value);
    }

    /// <summary>Upper bound; NaN derives it from the data.</summary>
    public double Max
    {
        get => GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public int Bins
    {
        get => GetValue(BinsProperty);
        set => SetValue(BinsProperty, value);
    }

    /// <summary>A released thumb near a bar edge settles on it.</summary>
    public bool SnapToBins
    {
        get => GetValue(SnapToBinsProperty);
        set => SetValue(SnapToBinsProperty, value);
    }

    public int MinStepsBetweenThumbs
    {
        get => GetValue(MinStepsBetweenThumbsProperty);
        set => SetValue(MinStepsBetweenThumbsProperty, value);
    }

    /// <summary>Accessible names for the two thumbs; defaults to "label, minimum" and "label, maximum".</summary>
    public IList<string>? ThumbLabels
    {
        get => GetValue(ThumbLabelsProperty);
        set => SetValue(ThumbLabelsProperty, value);
    }

    /// <summary>Formats the readout and the spoken values.</summary>
    public Func<double, string>? Format { get; set; }

    private double Lower => double.IsNaN(Min) ? DataMin() : Min;

    private double Upper
    {
        get
        {
            if (!double.IsNaN(Max))
            {
                return Max;
            }

            var dataMax = DataMax();
            return dataMax > dataMinCache ? dataMax : dataMinCache + 100;
        }
    }

    private double dataMinCache;
    private double dataMax = 1;

    private double DataMin()
    {
        RecomputeDataBounds();
        return dataMinCache;
    }

    private double DataMax()
    {
        RecomputeDataBounds();
        return dataMax;
    }

    private void RecomputeDataBounds()
    {
        if (Data is not { Count: > 0 } data)
        {
            dataMinCache = 0;
            dataMax = 0;
            return;
        }

        dataMinCache = data.Min();
        dataMax = data.Max();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width;
        return new Size(width, TrackCenter + 30);
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0)
        {
            return;
        }

        RecomputeDataBounds();
        var foreground = ArcResources.Brush(ArcTokens.Foreground);
        var secondary = ArcResources.Brush(ArcTokens.TextSecondary);
        var muted = ArcResources.Brush(ArcTokens.TextMuted);
        var track = ArcResources.Brush(ArcTokens.ControlTrack);
        var accent = ArcResources.Brush(ArcTokens.Accent);
        var surface = ArcResources.Brush(ArcTokens.Surface);
        var border = ArcResources.Brush(ArcTokens.BorderStrong);
        var focus = ArcResources.Brush(ArcTokens.FocusOutline);

        var startInset = 10d;
        var x0 = startInset;
        var x1 = Math.Max(x0 + 1, Bounds.Width - startInset);
        var usable = x1 - x0;
        var range = Math.Max(0.0000001, Upper - Lower);

        if (!string.IsNullOrEmpty(Label))
        {
            var label = ArcText.Create(Label!, 14, foreground, FontWeight.Medium);
            ArcDraw.Text(context, label, 0, HeaderHeight / 2 + 1);
        }

        var resolved = Resolve();
        var readout = ArcText.Create($"{FormatValue(resolved.Start)} – {FormatValue(resolved.End)}", 13, secondary);
        ArcDraw.Text(context, readout, Bounds.Width, HeaderHeight / 2 + 1, alignRight: true);

        DrawHistogram(context, x0, usable, range, resolved, accent, track);
        ArcDraw.Pill(context, new Rect(x0, TrackCenter - 2, usable, 4), track);

        var startX = x0 + usable * Fraction(resolved.Start, range);
        var endX = x0 + usable * Fraction(resolved.End, range);
        if (endX > startX)
        {
            ArcDraw.Pill(context, new Rect(startX, TrackCenter - 2, Math.Max(2, endX - startX), 4), accent);
        }

        DrawThumb(context, startX + (_dragging == 0 ? _strain : 0), surface, border, accent,
            _active == 0 && IsFocused && !_pointerFocus, _active == 0 && IsFocused, focus);
        DrawThumb(context, endX + (_dragging == 1 ? _strain : 0), surface, border, accent,
            _active == 1 && IsFocused && !_pointerFocus, _active == 1 && IsFocused, focus);
    }

    private void DrawHistogram(
        DrawingContext context,
        double x0,
        double usable,
        double range,
        ArcRange resolved,
        IBrush accent,
        IBrush track)
    {
        var binCount = Math.Clamp(Bins, 4, 60);
        var counts = Histogram(Data, Lower, Upper, binCount);
        if (counts.Length == 0)
        {
            return;
        }

        var max = Math.Max(1, counts.Max());
        var slot = usable / binCount;
        var barWidth = Math.Max(1, slot - 2);
        var startX = x0 + usable * Fraction(resolved.Start, range);
        var endX = x0 + usable * Fraction(resolved.End, range);

        for (var i = 0; i < counts.Length; i++)
        {
            var height = Math.Max(2, ChartHeight * counts[i] / max);
            var x = x0 + i * slot;
            var bar = new Rect(x, ChartTop + ChartHeight - height, barWidth, height);
            var middle = x + barWidth / 2;
            var inside = middle >= startX - 0.5 && middle <= endX + 0.5;
            ArcDraw.Fill(context, bar, Math.Min(3, barWidth / 2), inside ? accent : track);
        }
    }

    private void DrawThumb(
        DrawingContext context,
        double x,
        IBrush surface,
        IBrush border,
        IBrush accent,
        bool ring,
        bool filled,
        IBrush focus)
    {
        if (ring)
        {
            ArcDraw.Stroke(context, new Rect(x - ThumbRadius - 4, TrackCenter - ThumbRadius - 4,
                (ThumbRadius + 4) * 2, (ThumbRadius + 4) * 2), ThumbRadius + 4, focus, 2);
        }

        context.DrawEllipse(surface, new Pen(filled ? accent : border, 1.5), new Point(x, TrackCenter),
            ThumbRadius, ThumbRadius);
    }

    private double Fraction(double value, double range) => Math.Clamp((value - Lower) / range, 0, 1);

    private ArcRange Resolve()
    {
        var start = Value.Start;
        var end = Value.End;
        if (double.IsNaN(start) || double.IsNaN(end))
        {
            return new ArcRange(Lower, Upper);
        }

        if (start > end)
        {
            (start, end) = (end, start);
        }

        return new ArcRange(
            Math.Clamp(start, Lower, Upper),
            Math.Clamp(end, Lower, Upper));
    }

    /// <summary>Counts values into equal bars between min and max; the last bar keeps max itself.</summary>
    public static int[] Histogram(IEnumerable<double>? data, double min, double max, int bins)
    {
        bins = Math.Clamp(bins, 1, 200);
        var counts = new int[bins];
        if (data is null || max <= min)
        {
            return counts;
        }

        var width = (max - min) / bins;
        foreach (var value in data)
        {
            if (double.IsNaN(value) || value < min || value > max)
            {
                continue;
            }

            var index = value >= max ? bins - 1 : (int)((value - min) / width);
            counts[Math.Clamp(index, 0, bins - 1)]++;
        }

        return counts;
    }

    private string FormatValue(double value)
        => Format?.Invoke(value) ?? ArcFormat.Number(value, Decimals(Step));

    private static int Decimals(double step)
    {
        var decimals = 0;
        var value = Math.Abs(step);
        while (decimals < 3 && value > 0 && Math.Abs(value - Math.Round(value)) > 0.000001)
        {
            value *= 10;
            decimals++;
        }

        return decimals;
    }

    private string ThumbName(int index)
    {
        if (ThumbLabels is { Count: >= 2 } labels)
        {
            return labels[index] ?? (index == 0 ? "Minimum" : "Maximum");
        }

        return (Label ?? "Range") + (index == 0 ? ", minimum" : ", maximum");
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEnabled)
        {
            return;
        }

        _pointerFocus = true;
        Focus();
        var x = e.GetPosition(this).X;
        var x0 = 10d;
        var usable = Math.Max(1, Bounds.Width - 20);
        var resolved = Resolve();
        var range = Math.Max(0.0000001, Upper - Lower);
        var startX = x0 + usable * Fraction(resolved.Start, range);
        var endX = x0 + usable * Fraction(resolved.End, range);

        var thumb = Math.Abs(x - startX) <= Math.Abs(x - endX) ? 0 : 1;
        _active = thumb;
        _dragging = thumb;
        e.Pointer.Capture(this);
        MoveThumb(thumb, x, animate: false);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging < 0)
        {
            return;
        }

        MoveThumb(_dragging, e.GetPosition(this).X, animate: false);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging < 0)
        {
            return;
        }

        e.Pointer.Capture(null);
        if (SnapToBins)
        {
            SnapToBinEdge(_dragging);
        }

        _dragging = -1;
        SpringStrain();
        ValueCommitted?.Invoke(this, new ArcRangeValueEventArgs(Resolve()));
        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = -1;
        SpringStrain();
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEnabled)
        {
            return;
        }

        _pointerFocus = false;
        var resolved = Resolve();
        var bin = Math.Max(Step, (Upper - Lower) / Math.Max(4, Bins));
        var large = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        switch (e.Key)
        {
            case Key.Tab when !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                if (_active == 0)
                {
                    _active = 1;
                    e.Handled = true;
                    InvalidateVisual();
                }

                return;
            case Key.Tab:
                if (_active == 1)
                {
                    _active = 0;
                    e.Handled = true;
                    InvalidateVisual();
                }

                return;
            case Key.Left:
            case Key.Down:
                e.Handled = true;
                Nudge(_active, -(large ? bin : Step));
                return;
            case Key.Right:
            case Key.Up:
                e.Handled = true;
                Nudge(_active, large ? bin : Step);
                return;
            case Key.PageDown:
                e.Handled = true;
                Nudge(_active, -bin);
                return;
            case Key.PageUp:
                e.Handled = true;
                Nudge(_active, bin);
                return;
            case Key.Home:
                e.Handled = true;
                SetRange(_active == 0 ? Lower : Math.Min(resolved.Start + MinGap(), resolved.End),
                    _active == 0 ? resolved.End : resolved.End, announce: true);
                return;
            case Key.End:
                e.Handled = true;
                SetRange(_active == 0 ? resolved.Start : Math.Max(resolved.End, resolved.Start + MinGap()),
                    _active == 1 ? Upper : resolved.End, announce: true);
                return;
        }
    }

    private double MinGap() => Math.Max(Step, Step * Math.Max(1, MinStepsBetweenThumbs));

    private void Nudge(int thumb, double delta)
    {
        var resolved = Resolve();
        var gap = MinGap();
        if (thumb == 0)
        {
            var start = Math.Clamp(resolved.Start + delta, Lower, resolved.End - gap);
            SetRange(start, resolved.End, announce: true);
        }
        else
        {
            var end = Math.Clamp(resolved.End + delta, resolved.Start + gap, Upper);
            SetRange(resolved.Start, end, announce: true);
        }
    }

    private void MoveThumb(int thumb, double x, bool animate)
    {
        var x0 = 10d;
        var usable = Math.Max(1, Bounds.Width - 20);
        var fraction = (x - x0) / usable;
        var value = Lower + fraction * (Upper - Lower);

        if (value < Lower)
        {
            _strain = -Math.Min(6, (Lower - value) / Math.Max(0.0001, Upper - Lower) * usable * 0.2);
            value = Lower;
        }
        else if (value > Upper)
        {
            _strain = Math.Min(6, (value - Upper) / Math.Max(0.0001, Upper - Lower) * usable * 0.2);
            value = Upper;
        }
        else
        {
            _strain = 0;
        }

        value = Snap(value);
        var gap = MinGap();
        var resolved = Resolve();

        if (thumb == 0)
        {
            SetRange(Math.Clamp(value, Lower, resolved.End - gap), resolved.End, announce: true, animate: animate);
        }
        else
        {
            SetRange(resolved.Start, Math.Clamp(value, resolved.Start + gap, Upper), announce: true, animate: animate);
        }
    }

    private void SnapToBinEdge(int thumb)
    {
        if (!SnapToBins)
        {
            return;
        }

        var resolved = Resolve();
        var binWidth = (Upper - Lower) / Math.Max(4, Bins);
        if (binWidth <= 0)
        {
            return;
        }

        if (thumb == 0)
        {
            var edge = Lower + Math.Round((resolved.Start - Lower) / binWidth) * binWidth;
            SetRange(Math.Clamp(edge, Lower, resolved.End - MinGap()), resolved.End, announce: true);
        }
        else
        {
            var edge = Lower + Math.Round((resolved.End - Lower) / binWidth) * binWidth;
            SetRange(resolved.Start, Math.Clamp(edge, resolved.Start + MinGap(), Upper), announce: true);
        }
    }

    private double Snap(double value)
    {
        if (Step <= 0)
        {
            return value;
        }

        var steps = Math.Round((value - Lower) / Step);
        return Math.Round(Lower + steps * Step, 6);
    }

    private void SetRange(double start, double end, bool announce, bool animate = true)
    {
        var next = new ArcRange(Math.Min(start, end), Math.Max(start, end));
        if (Math.Abs(next.Start - Value.Start) < 0.0000001 && Math.Abs(next.End - Value.End) < 0.0000001)
        {
            return;
        }

        SetCurrentValue(ValueProperty, next);
        SyncDisplay(animate);
        ValueChanged?.Invoke(this, new ArcRangeValueEventArgs(next));
        if (announce)
        {
            ValueCommitted?.Invoke(this, new ArcRangeValueEventArgs(next));
        }

        InvalidateVisual();
    }

    private void OnValueChanged()
    {
        SyncDisplay(animate: true);
        UpdateAutomation();
        InvalidateVisual();
    }

    private void SyncDisplay(bool animate)
    {
        var resolved = Resolve();
        if (!animate || ArcThemeManager.ReducedMotion)
        {
            _startDisplay = resolved.Start;
            _endDisplay = resolved.End;
            InvalidateVisual();
            return;
        }

        var fromStart = _startDisplay;
        var fromEnd = _endDisplay;
        ArcAnim.To(0, 1, ArcAnim.SnappyDuration, ArcAnim.Snappy, value =>
        {
            _startDisplay = fromStart + (resolved.Start - fromStart) * value;
            _endDisplay = fromEnd + (resolved.End - fromEnd) * value;
            InvalidateVisual();
        });
    }

    private void SpringStrain()
    {
        if (Math.Abs(_strain) < 0.01)
        {
            _strain = 0;
            return;
        }

        var from = _strain;
        ArcAnim.To(from, 0, ArcAnim.SnappyDuration, ArcAnim.Snappy, value =>
        {
            _strain = value;
            InvalidateVisual();
        });
    }

    private void UpdateAutomation()
    {
        var resolved = Resolve();
        AutomationProperties.SetName(this, Label ?? "Range");
        AutomationProperties.SetHelpText(this,
            $"{ThumbName(0)} {FormatValue(resolved.Start)}, {ThumbName(1)} {FormatValue(resolved.End)}");
    }
}
