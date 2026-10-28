using System;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Marketing.Controls;

/// <summary>
/// A headline number that rolls between values on a short ease-out. It is a <see cref="TextBlock"/> so the
/// blocks can style it with the shared type classes, and it lands on the exact final string: a number that
/// reports a value must never overshoot or leave a stale figure in a screenshot. Reduced motion sets the
/// final value immediately.
/// </summary>
public sealed class ArcStatNumber : TextBlock
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ArcStatNumber, double>(nameof(Value));

    public static readonly StyledProperty<string> PrefixProperty =
        AvaloniaProperty.Register<ArcStatNumber, string>(nameof(Prefix), string.Empty);

    public static readonly StyledProperty<string> SuffixProperty =
        AvaloniaProperty.Register<ArcStatNumber, string>(nameof(Suffix), string.Empty);

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<ArcStatNumber, int>(nameof(Decimals));

    public static readonly StyledProperty<bool> AnimateChangesProperty =
        AvaloniaProperty.Register<ArcStatNumber, bool>(nameof(AnimateChanges), true);

    private readonly Stopwatch _clock = new();
    private DispatcherTimer? _timer;
    private double _from;
    private double _to;
    private TimeSpan _duration = TimeSpan.FromMilliseconds(460);
    private bool _attached;

    static ArcStatNumber()
    {
        ValueProperty.Changed.AddClassHandler<ArcStatNumber>((number, _) => number.OnValueChanged());
        DecimalsProperty.Changed.AddClassHandler<ArcStatNumber>((number, _) => number.Snap());
        PrefixProperty.Changed.AddClassHandler<ArcStatNumber>((number, _) => number.Snap());
        SuffixProperty.Changed.AddClassHandler<ArcStatNumber>((number, _) => number.Snap());
    }

    /// <summary>The number to show.</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Prefix
    {
        get => GetValue(PrefixProperty);
        set => SetValue(PrefixProperty, value);
    }

    public string Suffix
    {
        get => GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    public int Decimals
    {
        get => GetValue(DecimalsProperty);
        set => SetValue(DecimalsProperty, value);
    }

    /// <summary>Rolls between values; the first paint always shows the final value.</summary>
    public bool AnimateChanges
    {
        get => GetValue(AnimateChangesProperty);
        set => SetValue(AnimateChangesProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Snap();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer?.Stop();
        _timer = null;
        _attached = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnValueChanged()
    {
        if (!_attached || !AnimateChanges || ArcThemeManager.ReducedMotion)
        {
            Snap();
            return;
        }

        _from = Parse(Text ?? string.Empty);
        _to = Value;
        _clock.Restart();
        if (_timer is null)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += (_, _) => Step();
        }

        _timer.Start();
    }

    private void Step()
    {
        var progress = Math.Clamp(_clock.Elapsed.TotalMilliseconds / _duration.TotalMilliseconds, 0, 1);
        var eased = 1 - Math.Pow(1 - progress, 3);
        SetText(_from + ((_to - _from) * eased));
        if (progress >= 1)
        {
            _timer?.Stop();
            Snap();
        }
    }

    private void Snap()
    {
        _timer?.Stop();
        SetText(Value);
    }

    private void SetText(double value)
    {
        var decimals = Math.Clamp(Decimals, 0, 4);
        var format = decimals > 0 ? "#,##0." + new string('0', decimals) : "#,##0";
        base.Text = Prefix + value.ToString(format, CultureInfo.GetCultureInfo("en-US")) + Suffix;
    }

    private double Parse(string text)
    {
        var trimmed = text;
        if (!string.IsNullOrEmpty(Prefix) && trimmed.StartsWith(Prefix, StringComparison.Ordinal))
        {
            trimmed = trimmed[Prefix.Length..];
        }

        if (!string.IsNullOrEmpty(Suffix) && trimmed.EndsWith(Suffix, StringComparison.Ordinal))
        {
            trimmed = trimmed[..^Suffix.Length];
        }

        trimmed = trimmed.Replace(",", string.Empty);
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}
