using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>
/// The price line that goes with the billing toggle: the amount rolls to its new value with tabular
/// figures, the old price fades in struck through, and the period swaps in place.
/// </summary>
public class ArcBillingPrice : Control
{
    public static readonly StyledProperty<double> AmountProperty =
        AvaloniaProperty.Register<ArcBillingPrice, double>(nameof(Amount));

    public static readonly StyledProperty<string?> CurrencyProperty =
        AvaloniaProperty.Register<ArcBillingPrice, string?>(nameof(Currency), "$");

    public static readonly StyledProperty<string?> PeriodProperty =
        AvaloniaProperty.Register<ArcBillingPrice, string?>(nameof(Period));

    public static readonly StyledProperty<double> WasProperty =
        AvaloniaProperty.Register<ArcBillingPrice, double>(nameof(Was), double.NaN);

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<ArcBillingPrice, int>(nameof(Decimals));

    private double _display;

    static ArcBillingPrice()
    {
        AffectsRender<ArcBillingPrice>(AmountProperty, CurrencyProperty, PeriodProperty, WasProperty, DecimalsProperty);
        AmountProperty.Changed.AddClassHandler<ArcBillingPrice>((price, _) => price.OnAmountChanged());
        WasProperty.Changed.AddClassHandler<ArcBillingPrice>((price, _) => price.InvalidateVisual());
        PeriodProperty.Changed.AddClassHandler<ArcBillingPrice>((price, _) => price.InvalidateVisual());
        CurrencyProperty.Changed.AddClassHandler<ArcBillingPrice>((price, _) => price.InvalidateVisual());
        DecimalsProperty.Changed.AddClassHandler<ArcBillingPrice>((price, _) => price.InvalidateVisual());
    }

    public ArcBillingPrice()
    {
        _display = Amount;
    }

    /// <summary>Raised once the amount has rolled to its new value.</summary>
    public event EventHandler? PricesRolled;

    /// <summary>The price to show.</summary>
    public double Amount
    {
        get => GetValue(AmountProperty);
        set => SetValue(AmountProperty, value);
    }

    /// <summary>Symbol before the amount and the old price.</summary>
    public string? Currency
    {
        get => GetValue(CurrencyProperty);
        set => SetValue(CurrencyProperty, value);
    }

    /// <summary>Text after the price, such as "per month"; swaps in place.</summary>
    public string? Period
    {
        get => GetValue(PeriodProperty);
        set => SetValue(PeriodProperty, value);
    }

    /// <summary>Previous price, shown struck through only when it is higher than the amount.</summary>
    public double Was
    {
        get => GetValue(WasProperty);
        set => SetValue(WasProperty, value);
    }

    public int Decimals
    {
        get => GetValue(DecimalsProperty);
        set => SetValue(DecimalsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 240 : availableSize.Width;
        return new Size(width, 48);
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0)
        {
            return;
        }

        var center = Bounds.Height / 2;
        var foreground = ArcResources.Brush(ArcTokens.Foreground);
        var muted = ArcResources.Brush(ArcTokens.TextMuted);
        var secondary = ArcResources.Brush(ArcTokens.TextSecondary);
        var currency = Currency ?? string.Empty;

        var x = 0d;

        var showWas = !double.IsNaN(Was) && Was > Amount;
        if (showWas)
        {
            var was = ArcText.Create(currency + Format(Was), 15, muted);
            was.SetTextDecorations(TextDecorations.Strikethrough);
            ArcDraw.Text(context, was, x, center + 4);
            x += was.Width + 10;
        }

        var target = ArcText.Create(currency + Format(Amount), 36, foreground, FontWeight.Medium, display: true);
        var current = ArcText.Create(currency + Format(_display), 36, foreground, FontWeight.Medium, display: true);

        // The slot keeps the widest of the two amounts so the period beside it never jitters mid roll.
        var slot = Math.Max(target.Width, current.Width);
        ArcDraw.Text(context, current, x, center);

        x += slot + 6;

        if (!string.IsNullOrWhiteSpace(Period))
        {
            var period = ArcText.Create(Period!, 14, secondary);
            ArcDraw.Text(context, period, x, center + 4);
        }
        else
        {
            x += 0;
        }

        AutomationProperties.SetHelpText(this, currency + Format(Amount) + (Period is null ? string.Empty : " " + Period));
    }

    private string Format(double value)
    {
        var decimals = Math.Clamp(Decimals, 0, 4);
        return value.ToString("0." + new string('0', decimals), CultureInfo.InvariantCulture);
    }

    private void OnAmountChanged()
    {
        if (VisualRoot is null || ArcThemeManager.ReducedMotion)
        {
            _display = Amount;
            InvalidateVisual();
            PricesRolled?.Invoke(this, EventArgs.Empty);
            return;
        }

        var from = _display;
        ArcAnim.To(from, Amount, TimeSpan.FromMilliseconds(480), ArcAnim.Smooth, value =>
        {
            _display = value;
            InvalidateVisual();
        }, () => PricesRolled?.Invoke(this, EventArgs.Empty));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _display = ArcThemeManager.ReducedMotion ? Amount : _display;
        InvalidateVisual();
    }
}
