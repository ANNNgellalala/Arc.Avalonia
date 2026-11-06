using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// An odometer-style number with tabular figures, a small label, prefix and suffix. It settles
/// quickly on a value change and skips travel entirely when Arc reduced motion is on.
/// </summary>
public class ArcChartAnimatedCounter : ArcChartBase
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ArcChartAnimatedCounter, double>(nameof(Value));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcChartAnimatedCounter, string?>(nameof(Label));

    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<ArcChartAnimatedCounter, string?>(nameof(Prefix));

    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<ArcChartAnimatedCounter, string?>(nameof(Suffix));

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<ArcChartAnimatedCounter, int>(nameof(Decimals));

    public static readonly StyledProperty<bool> AnimateOnLoadProperty =
        AvaloniaProperty.Register<ArcChartAnimatedCounter, bool>(nameof(AnimateOnLoad));

    public static readonly StyledProperty<TimeSpan> AnimationDurationProperty =
        AvaloniaProperty.Register<ArcChartAnimatedCounter, TimeSpan>(nameof(AnimationDuration), TimeSpan.FromMilliseconds(480));

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcChartAnimatedCounter, Func<double, string>?>(nameof(ValueFormat));

    private DispatcherTimer? _timer;
    private Stopwatch? _watch;
    private double _from;
    private double _to;
    private double _displayValue;

    static ArcChartAnimatedCounter()
    {
        AffectsRender<ArcChartAnimatedCounter>(
            LabelProperty,
            PrefixProperty,
            SuffixProperty,
            DecimalsProperty,
            ValueFormatProperty);
        ValueProperty.Changed.AddClassHandler<ArcChartAnimatedCounter>((counter, _) => counter.OnValueChanged());
    }

    public ArcChartAnimatedCounter()
    {
        _displayValue = Value;
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Small label above the number.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Text before the number, such as "$".</summary>
    public string? Prefix
    {
        get => GetValue(PrefixProperty);
        set => SetValue(PrefixProperty, value);
    }

    /// <summary>Text after the number, such as "%".</summary>
    public string? Suffix
    {
        get => GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    /// <summary>Fixed fraction digits.</summary>
    public int Decimals
    {
        get => GetValue(DecimalsProperty);
        set => SetValue(DecimalsProperty, value);
    }

    /// <summary>Rolls from zero the first time it enters the visual tree.</summary>
    public bool AnimateOnLoad
    {
        get => GetValue(AnimateOnLoadProperty);
        set => SetValue(AnimateOnLoadProperty, value);
    }

    public TimeSpan AnimationDuration
    {
        get => GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    /// <summary>Optional custom format, which replaces <see cref="Decimals"/>.</summary>
    public Func<double, string>? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    protected override double DefaultHeight => 116;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _displayValue = AnimateOnLoad && !ArcThemeManager.ReducedMotion ? 0 : Value;
        if (AnimateOnLoad && !ArcThemeManager.ReducedMotion)
        {
            StartAnimation(0, Value);
        }
        else
        {
            InvalidateVisual();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopTimer();
        _displayValue = Value;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var centerX = Bounds.Width / 2;
        var hasLabel = !string.IsNullOrWhiteSpace(Label);
        if (hasLabel)
        {
            ChartText.Draw(
                context,
                Label!,
                centerX,
                20,
                ChartMetrics.LabelFontSize,
                ChartPalette.Muted,
                alignment: TextAlignment.Center,
                maxWidth: Bounds.Width - 16);
        }

        var number = Format(_displayValue);
        var numberText = ChartText.Create(number, 40, ChartPalette.Foreground, FontWeight.Medium);
        var prefixText = string.IsNullOrEmpty(Prefix) ? null : ChartText.Create(Prefix!, 18, ChartPalette.Secondary);
        var suffixText = string.IsNullOrEmpty(Suffix) ? null : ChartText.Create(Suffix!, 18, ChartPalette.Secondary);
        var totalWidth = numberText.Width + (prefixText?.Width ?? 0) + (suffixText?.Width ?? 0) + 4;
        var x = centerX - totalWidth / 2;
        var centerY = hasLabel ? Bounds.Height / 2 + 12 : Bounds.Height / 2;
        if (prefixText is not null)
        {
            ChartText.Draw(context, prefixText, x, centerY + 7);
            x += prefixText.Width + 2;
        }

        context.DrawText(numberText, new Point(x, centerY - numberText.Height / 2));
        x += numberText.Width + 2;
        if (suffixText is not null)
        {
            ChartText.Draw(context, suffixText, x, centerY + 7);
        }
    }

    private void OnValueChanged()
    {
        if (VisualRoot is null)
        {
            _displayValue = Value;
            InvalidateVisual();
            return;
        }

        if (ArcThemeManager.ReducedMotion)
        {
            _displayValue = Value;
            StopTimer();
            InvalidateVisual();
            return;
        }

        StartAnimation(_displayValue, Value);
    }

    private void StartAnimation(double from, double to)
    {
        StopTimer();
        _from = from;
        _to = to;
        _watch = Stopwatch.StartNew();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private void Tick()
    {
        if (_watch is null)
        {
            StopTimer();
            return;
        }

        var total = Math.Max(1, AnimationDuration.TotalMilliseconds);
        var progress = Math.Clamp(_watch.Elapsed.TotalMilliseconds / total, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);
        _displayValue = _from + (_to - _from) * eased;
        InvalidateVisual();
        if (progress >= 1)
        {
            _displayValue = _to;
            StopTimer();
        }
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
        _watch = null;
    }

    private string Format(double value)
        => ValueFormat?.Invoke(value) ?? ChartFormat.Number(value, Decimals);
}
