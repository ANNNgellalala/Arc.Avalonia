using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

public enum ArcMetricTrend
{
    Flat,
    Up,
    Down
}

/// <summary>
/// A compact summary for a number that needs context. Built on <see cref="ArcCard"/>; the value
/// rolls between numbers and the change chip states its direction in text as well as colour.
/// </summary>
public class ArcMetricCard : ArcCard
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcMetricCard, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ArcMetricCard, double>(nameof(Value));

    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<ArcMetricCard, string?>(nameof(Suffix));

    public static readonly StyledProperty<string?> ContextProperty =
        AvaloniaProperty.Register<ArcMetricCard, string?>(nameof(Context));

    public static readonly StyledProperty<string?> ChangeProperty =
        AvaloniaProperty.Register<ArcMetricCard, string?>(nameof(Change));

    public static readonly StyledProperty<ArcMetricTrend> TrendProperty =
        AvaloniaProperty.Register<ArcMetricCard, ArcMetricTrend>(nameof(Trend));

    public static readonly StyledProperty<object?> SparklineProperty =
        AvaloniaProperty.Register<ArcMetricCard, object?>(nameof(Sparkline));

    private TextBlock? _valueText;
    private readonly ArcTween _tween = new();
    private double _displayed;

    static ArcMetricCard()
    {
        LabelProperty.Changed.AddClassHandler<ArcMetricCard>((card, _) => card.Rebuild());
        SuffixProperty.Changed.AddClassHandler<ArcMetricCard>((card, _) => card.Rebuild());
        ContextProperty.Changed.AddClassHandler<ArcMetricCard>((card, _) => card.Rebuild());
        ChangeProperty.Changed.AddClassHandler<ArcMetricCard>((card, _) => card.Rebuild());
        TrendProperty.Changed.AddClassHandler<ArcMetricCard>((card, _) => card.Rebuild());
        SparklineProperty.Changed.AddClassHandler<ArcMetricCard>((card, _) => card.Rebuild());
        ValueProperty.Changed.AddClassHandler<ArcMetricCard>((card, _) => card.UpdateValue(animate: true));
    }

    protected override Type StyleKeyOverride => typeof(ArcCard);

    /// <summary>What is measured.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>The number; rolls up from zero on first view, then between values.</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Text after the number, such as "%" or " ms".</summary>
    public string? Suffix
    {
        get => GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    /// <summary>Line under the number, such as "vs last week".</summary>
    public string? Context
    {
        get => GetValue(ContextProperty);
        set => SetValue(ContextProperty, value);
    }

    /// <summary>Chip in the top corner, such as "+12%". Omit to hide it.</summary>
    public string? Change
    {
        get => GetValue(ChangeProperty);
        set => SetValue(ChangeProperty, value);
    }

    /// <summary>Direction of the change; colours the chip and picks its arrow.</summary>
    public ArcMetricTrend Trend
    {
        get => GetValue(TrendProperty);
        set => SetValue(TrendProperty, value);
    }

    /// <summary>Optional composition slot for a sparkline or mini chart under the value.</summary>
    public object? Sparkline
    {
        get => GetValue(SparklineProperty);
        set => SetValue(SparklineProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_built)
        {
            Rebuild();
        }
    }

    private bool _built;

    private void Rebuild()
    {
        _built = true;
        _valueText = null;

        var root = new StackPanel { Spacing = 10 };

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        top.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var label = ArcUi.Text(Label, "arc-label");
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        top.Children.Add(label);

        var trend = ResolveTrend();
        var change = Change;
        if (!string.IsNullOrWhiteSpace(change))
        {
            var chip = BuildChangeChip(change, trend);
            Grid.SetColumn(chip, 1);
            top.Children.Add(chip);
        }

        root.Children.Add(top);

        var numberRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        _valueText = ArcUi.Numeric(string.Empty);
        _valueText.FontSize = ArcResources.Double(ArcTokens.Text3xl);
        _valueText.FontWeight = FontWeight.Medium;
        _valueText.LineHeight = 40;
        _valueText.LetterSpacing = -1;
        _valueText.VerticalAlignment = VerticalAlignment.Bottom;
        numberRow.Children.Add(_valueText);

        if (!string.IsNullOrWhiteSpace(Suffix))
        {
            var suffix = ArcUi.Text(Suffix, "arc-body-lg", "arc-secondary");
            suffix.VerticalAlignment = VerticalAlignment.Bottom;
            suffix.Margin = new Thickness(2, 0, 0, 4);
            numberRow.Children.Add(suffix);
        }

        root.Children.Add(numberRow);

        if (!string.IsNullOrWhiteSpace(Context))
        {
            var context = ArcUi.Text(Context, "arc-caption");
            context.TextTrimming = TextTrimming.CharacterEllipsis;
            root.Children.Add(context);
        }

        if (Sparkline is not null)
        {
            var slot = new ContentControl
            {
                Content = Sparkline,
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            root.Children.Add(slot);
        }

        Content = root;
        AutomationProperties.SetName(this, BuildAutomationName());
        UpdateValue(animate: !_built);
    }

    private bool _firstValue = true;

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
            if (_valueText is not null)
            {
                _valueText.Text = FormatValue(value);
            }
        }

        if (_firstValue || !animate)
        {
            _tween.Stop();
            Apply(target);
            _firstValue = false;
        }
        else
        {
            _tween.Run(_displayed, target, TimeSpan.FromMilliseconds(650), Apply, () => Apply(target));
        }

        AutomationProperties.SetName(this, BuildAutomationName());
    }

    private string BuildAutomationName()
    {
        var parts = new System.Collections.Generic.List<string> { $"{Label}: {FormatValue(Value)}{Suffix}" };
        if (!string.IsNullOrWhiteSpace(Context))
        {
            parts.Add(Context!);
        }

        if (!string.IsNullOrWhiteSpace(Change))
        {
            parts.Add(Change!);
        }

        return string.Join(". ", parts);
    }

    private static string FormatValue(double value)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        var format = Math.Abs(rounded - Math.Round(rounded)) < 0.001 ? "#,##0" : "#,##0.##";
        return rounded.ToString(format, CultureInfo.InvariantCulture);
    }

    private ArcMetricTrend ResolveTrend()
    {
        if (Trend != ArcMetricTrend.Flat || string.IsNullOrWhiteSpace(Change))
        {
            return Trend;
        }

        var text = Change!.Trim();
        if (text.StartsWith("-", StringComparison.Ordinal) || text.Contains('↓'))
        {
            return ArcMetricTrend.Down;
        }

        if (text.StartsWith("+", StringComparison.Ordinal) || text.Contains('↑'))
        {
            return ArcMetricTrend.Up;
        }

        return ArcMetricTrend.Flat;
    }

    private Control BuildChangeChip(string text, ArcMetricTrend trend)
    {
        var (background, foreground, icon) = trend switch
        {
            ArcMetricTrend.Up => (ArcTokens.SuccessSubtle, ArcTokens.Success, "arrow-up-right"),
            ArcMetricTrend.Down => (ArcTokens.DangerSubtle, ArcTokens.Danger, "arrow-down-right"),
            _ => (ArcTokens.SurfaceMuted, ArcTokens.TextSecondary, "minus")
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(ArcUi.Icon(icon, 12, foreground));
        var label = ArcUi.Numeric(text);
        label.FontSize = ArcResources.Double(ArcTokens.TextXs);
        label.FontWeight = FontWeight.Medium;
        label.Paint(TextBlock.ForegroundProperty, foreground);
        row.Children.Add(label);

        var chip = new Border
        {
            CornerRadius = ArcUi.Radius(ArcTokens.RadiusPill),
            Padding = new Thickness(8, 3, 10, 3),
            Child = row,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        chip.Paint(Border.BackgroundProperty, background);
        ToolTip.SetTip(chip, text);
        return chip;
    }
}
