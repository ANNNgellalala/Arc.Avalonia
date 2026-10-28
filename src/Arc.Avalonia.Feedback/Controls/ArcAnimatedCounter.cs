using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

/// <summary>
/// A number that rolls to its target with tabular figures and a width that does not jitter while
/// the digits change. Reduced motion jumps straight to the value.
/// </summary>
public class ArcAnimatedCounter : TemplatedControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ArcAnimatedCounter, double>(nameof(Value));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcAnimatedCounter, string?>(nameof(Label));

    public static readonly StyledProperty<string> PrefixProperty =
        AvaloniaProperty.Register<ArcAnimatedCounter, string>(nameof(Prefix), string.Empty);

    public static readonly StyledProperty<string> SuffixProperty =
        AvaloniaProperty.Register<ArcAnimatedCounter, string>(nameof(Suffix), string.Empty);

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<ArcAnimatedCounter, int>(nameof(Decimals));

    public static readonly StyledProperty<bool> AnimateOnViewProperty =
        AvaloniaProperty.Register<ArcAnimatedCounter, bool>(nameof(AnimateOnView));

    public static readonly StyledProperty<string> LocaleProperty =
        AvaloniaProperty.Register<ArcAnimatedCounter, string>(nameof(Locale), "en-US");

    private TextBlock? _labelText;
    private TextBlock? _prefixText;
    private TextBlock? _valueText;
    private TextBlock? _suffixText;
    private readonly ArcTween _tween = new();
    private double _displayed;
    private bool _started;

    static ArcAnimatedCounter()
    {
        ValueProperty.Changed.AddClassHandler<ArcAnimatedCounter>((counter, _) => counter.UpdateValue(animate: true));
        LabelProperty.Changed.AddClassHandler<ArcAnimatedCounter>((counter, _) => counter.UpdateParts());
        PrefixProperty.Changed.AddClassHandler<ArcAnimatedCounter>((counter, _) => counter.UpdateParts());
        SuffixProperty.Changed.AddClassHandler<ArcAnimatedCounter>((counter, _) => counter.UpdateParts());
        DecimalsProperty.Changed.AddClassHandler<ArcAnimatedCounter>((counter, _) => counter.UpdateValue(animate: false));
        AnimateOnViewProperty.Changed.AddClassHandler<ArcAnimatedCounter>((counter, _) => counter.UpdateValue(animate: false));
        LocaleProperty.Changed.AddClassHandler<ArcAnimatedCounter>((counter, _) => counter.UpdateValue(animate: false));
    }

    /// <summary>The number to show.</summary>
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
    public string Prefix
    {
        get => GetValue(PrefixProperty);
        set => SetValue(PrefixProperty, value);
    }

    /// <summary>Text after the number, such as "%".</summary>
    public string Suffix
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

    /// <summary>Rolls every digit up from zero the first time the counter is shown.</summary>
    public bool AnimateOnView
    {
        get => GetValue(AnimateOnViewProperty);
        set => SetValue(AnimateOnViewProperty, value);
    }

    public string Locale
    {
        get => GetValue(LocaleProperty);
        set => SetValue(LocaleProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _labelText = e.NameScope.Find<TextBlock>("LabelText");
        _prefixText = e.NameScope.Find<TextBlock>("PrefixText");
        _valueText = e.NameScope.Find<TextBlock>("ValueText");
        _suffixText = e.NameScope.Find<TextBlock>("SuffixText");

        if (_valueText is not null)
        {
            _valueText.FontFeatures = FeedbackUi.Tabular;
        }

        UpdateParts();
        UpdateValue(animate: false);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_started)
        {
            _started = true;
            _displayed = 0;
            UpdateValue(animate: AnimateOnView || Value != 0);
        }
    }

    private void UpdateParts()
    {
        if (_labelText is not null)
        {
            _labelText.Text = Label;
            _labelText.IsVisible = !string.IsNullOrWhiteSpace(Label);
        }

        if (_prefixText is not null)
        {
            _prefixText.Text = Prefix;
            _prefixText.IsVisible = !string.IsNullOrEmpty(Prefix);
        }

        if (_suffixText is not null)
        {
            _suffixText.Text = Suffix;
            _suffixText.IsVisible = !string.IsNullOrEmpty(Suffix);
        }

        AutomationProperties.SetName(this, BuildText(Value));
        UpdateValue(animate: false);
    }

    private void UpdateValue(bool animate)
    {
        if (_valueText is null)
        {
            return;
        }

        var target = Value;
        void Apply(double value)
        {
            _displayed = value;
            if (_valueText is null)
            {
                return;
            }

            _valueText.Text = BuildText(value);
            var width = MeasureWidth(_valueText.Text);
            if (width > 0 && width > _valueText.MinWidth)
            {
                _valueText.MinWidth = width;
            }
        }

        if (!animate || !_started)
        {
            _tween.Stop();
            Apply(target);
        }
        else
        {
            _tween.Run(_displayed, target, TimeSpan.FromMilliseconds(700), Apply, () => Apply(target));
        }

        AutomationProperties.SetName(this, BuildText(Value));
    }

    private string BuildText(double value)
    {
        var decimals = Math.Clamp(Decimals, 0, 4);
        var culture = CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(Locale) ? "en-US" : Locale);
        var format = decimals > 0 ? "#,##0." + new string('0', decimals) : "#,##0";
        return value.ToString(format, culture);
    }

    private double MeasureWidth(string text)
    {
        try
        {
            var family = Application.Current?.FindResource(ArcTokens.FontDisplay) as FontFamily ?? FontFamily.Default;
            var typeface = new Typeface(family, FontStyle.Normal, FontWeight.Medium);
            var size = _valueText?.FontSize ?? 36;
            var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, Brushes.White);
            return Math.Ceiling(formatted.Width) + 1;
        }
        catch
        {
            return 0;
        }
    }
}
