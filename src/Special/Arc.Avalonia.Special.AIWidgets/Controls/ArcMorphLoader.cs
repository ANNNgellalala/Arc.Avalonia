using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.AIWidgets.Controls;

/// <summary>Which shape the loader is currently drawn as.</summary>
public enum ArcMorphLoaderVariant
{
    Dots,
    Bars,
    Ring,
    Square
}

/// <summary>What the loader reports.</summary>
public enum ArcMorphLoaderStatus
{
    Loading,
    Success,
    Error
}

/// <summary>
/// A tiny loader that changes shape and then folds into a check or a cross. Four strokes gather into dots, bars, a
/// ring or a square while loading, and on success or error the loop fades and the mark draws itself.
///
/// Platform approximations: the reference animates each stroke's centre, length, bend and width on its own spring;
/// this port blends a shape pair on one clock and crossfades between variants, so the morph reads as one mark
/// changing rather than four independently sprung strokes. Reduced motion shows a still shape per state.
/// </summary>
public class ArcMorphLoader : Control
{
    public static readonly StyledProperty<ArcMorphLoaderVariant> VariantProperty =
        AvaloniaProperty.Register<ArcMorphLoader, ArcMorphLoaderVariant>(nameof(Variant));

    public static readonly StyledProperty<ArcMorphLoaderStatus> StatusProperty =
        AvaloniaProperty.Register<ArcMorphLoader, ArcMorphLoaderStatus>(nameof(Status));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcMorphLoader, string?>(nameof(Label), "Loading");

    public static readonly StyledProperty<string?> SuccessLabelProperty =
        AvaloniaProperty.Register<ArcMorphLoader, string?>(nameof(SuccessLabel), "Done");

    public static readonly StyledProperty<string?> ErrorLabelProperty =
        AvaloniaProperty.Register<ArcMorphLoader, string?>(nameof(ErrorLabel), "Failed");

    public static readonly StyledProperty<bool> ToneProperty =
        AvaloniaProperty.Register<ArcMorphLoader, bool>(nameof(Tone), true);

    private readonly SvClock _clock = new();
    private double _time;
    private double _settle;
    private ArcMorphLoaderVariant _previous;
    private ArcMorphLoaderVariant _current;
    private double _blend = 1;
    private readonly SvTween _shapeTween = new();
    private readonly SvTween _settleTween = new();

    static ArcMorphLoader()
    {
        AffectsRender<ArcMorphLoader>(VariantProperty, StatusProperty, ToneProperty);
        VariantProperty.Changed.AddClassHandler<ArcMorphLoader>((loader, _) => loader.OnVariantChanged());
        StatusProperty.Changed.AddClassHandler<ArcMorphLoader>((loader, _) => loader.OnStatusChanged());
    }

    public ArcMorphLoader()
    {
        Width = 24;
        Height = 24;
        Focusable = false;
    }

    public ArcMorphLoaderVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public ArcMorphLoaderStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? SuccessLabel
    {
        get => GetValue(SuccessLabelProperty);
        set => SetValue(SuccessLabelProperty, value);
    }

    public string? ErrorLabel
    {
        get => GetValue(ErrorLabelProperty);
        set => SetValue(ErrorLabelProperty, value);
    }

    /// <summary>Colours the check with the success token and the cross with the danger token.</summary>
    public bool Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = double.IsNaN(Width) || Width <= 0 ? 24 : Width;
        return new Size(size, double.IsNaN(Height) || Height <= 0 ? 24 : Height);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _current = Variant;
        _previous = Variant;
        _blend = 1;
        _settle = Status == ArcMorphLoaderStatus.Loading ? 0 : 1;
        Announce();
        _clock.Start(seconds =>
        {
            _time = seconds;
            InvalidateVisual();
        });
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clock.Stop();
        _shapeTween.Stop();
        _settleTween.Stop();
    }

    private void OnVariantChanged()
    {
        if (Variant == _current)
        {
            return;
        }

        _previous = _current;
        _current = Variant;
        if (ArcThemeManager.ReducedMotion)
        {
            _blend = 1;
            InvalidateVisual();
            return;
        }

        _blend = 0;
        _shapeTween.Run(0, 1, 260, value =>
        {
            _blend = value;
            InvalidateVisual();
        }, () => _blend = 1);
    }

    private void OnStatusChanged()
    {
        Announce();
        var target = Status == ArcMorphLoaderStatus.Loading ? 0 : 1;
        _settleTween.Run(_settle, target, 260, value =>
        {
            _settle = value;
            InvalidateVisual();
        }, () =>
        {
            _settle = target;
            InvalidateVisual();
        });
    }

    private void Announce()
    {
        var label = Status switch
        {
            ArcMorphLoaderStatus.Success => SuccessLabel ?? "Done",
            ArcMorphLoaderStatus.Error => ErrorLabel ?? "Failed",
            _ => Label ?? "Loading"
        };
        AutomationProperties.SetName(this, label);
        AutomationProperties.SetHelpText(this, Status == ArcMorphLoaderStatus.Loading ? $"Loading, {Variant} shape" : label);
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }

        var scale = size / 24.0;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            var foreground = Sv.Brush(ArcTokens.Foreground);
            var muted = Sv.Brush(ArcTokens.TextMuted);
            var loadingAlpha = 1 - _settle;

            if (loadingAlpha > 0.01)
            {
                using (context.PushOpacity(loadingAlpha))
                {
                    if (_blend < 1)
                    {
                        DrawShape(context, _previous, 1 - _blend, muted);
                    }

                    DrawShape(context, _current, _blend < 1 ? _blend : 1, foreground);
                }
            }

            if (_settle > 0.01 && Status != ArcMorphLoaderStatus.Loading)
            {
                var tone = Tone
                    ? Status == ArcMorphLoaderStatus.Success ? Sv.Brush(ArcTokens.Success) : Sv.Brush(ArcTokens.Danger)
                    : foreground;
                using (context.PushOpacity(_settle))
                using (context.PushTransform(
                           Matrix.CreateTranslation(-12, -12)
                           * Matrix.CreateScale(1 + 0.12 * (1 - _settle), 1 + 0.12 * (1 - _settle))
                           * Matrix.CreateTranslation(12, 12)))
                {
                    if (Status == ArcMorphLoaderStatus.Success)
                    {
                        DrawCheck(context, _settle, tone);
                    }
                    else
                    {
                        DrawCross(context, _settle, tone);
                    }
                }
            }
        }
    }

    private void DrawShape(DrawingContext context, ArcMorphLoaderVariant variant, double alpha, IBrush brush)
    {
        using (context.PushOpacity(Math.Clamp(alpha, 0, 1)))
        {
            var pen = new Pen(brush, 2.5, null, PenLineCap.Round, PenLineJoin.Round);
            switch (variant)
            {
                case ArcMorphLoaderVariant.Dots:
                    for (var i = 0; i < 4; i++)
                    {
                        var phase = _time * 3.2 - i * 0.7;
                        var bounce = ArcThemeManager.ReducedMotion ? 0 : Math.Max(0, Math.Sin(phase)) * 1.6;
                        var x = 4.0 + i * 5.33;
                        context.DrawEllipse(brush, null, new Point(x, 12 - bounce), 2.3, 2.3);
                    }

                    break;
                case ArcMorphLoaderVariant.Bars:
                    for (var i = 0; i < 4; i++)
                    {
                        var phase = _time * 3.0 - i * 0.6;
                        var growth = ArcThemeManager.ReducedMotion ? 0.55 : 0.4 + 0.6 * (0.5 + 0.5 * Math.Sin(phase));
                        var x = 4.6 + i * 4.95;
                        var half = 3 + 4 * growth;
                        context.DrawLine(pen, new Point(x, 12 - half), new Point(x, 12 + half));
                    }

                    break;
                case ArcMorphLoaderVariant.Ring:
                    for (var i = 0; i < 4; i++)
                    {
                        var start = _time * 150 + i * 90;
                        var sweep = ArcThemeManager.ReducedMotion ? 62 : 46 + 26 * (0.5 + 0.5 * Math.Sin(_time * 2.2 + i));
                        SvDraw.Arc(context, new Point(12, 12), 7.2, start, sweep, pen);
                    }

                    break;
                default:
                    var rotation = ArcThemeManager.ReducedMotion ? 0 : Math.Floor(_time / 0.9) * 22.5 + (_time % 0.9) * 8;
                    using (context.PushTransform(
                               Matrix.CreateTranslation(-12, -12)
                               * Matrix.CreateRotation(rotation * Math.PI / 180.0)
                               * Matrix.CreateTranslation(12, 12)))
                    {
                        const double half2 = 6.4;
                        const double inset = 2.2;
                        context.DrawLine(pen, new Point(12 - half2 + inset, 12 - half2), new Point(12 + half2, 12 - half2));
                        context.DrawLine(pen, new Point(12 + half2, 12 - half2), new Point(12 + half2, 12 + half2 - inset));
                        context.DrawLine(pen, new Point(12 + half2, 12 + half2), new Point(12 - half2, 12 + half2));
                        context.DrawLine(pen, new Point(12 - half2, 12 + half2), new Point(12 - half2, 12 - half2 + inset));
                    }

                    break;
            }
        }
    }

    private static void DrawCheck(DrawingContext context, double progress, IBrush brush)
    {
        var pen = new Pen(brush, 2.6, null, PenLineCap.Round, PenLineJoin.Round);
        var a = new Point(5.6, 12.4);
        var b = new Point(10.2, 16.6);
        var c = new Point(18.6, 7.6);

        if (progress < 0.45)
        {
            var t = progress / 0.45;
            context.DrawLine(pen, a, new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
            return;
        }

        context.DrawLine(pen, a, b);
        var t2 = (progress - 0.45) / 0.55;
        context.DrawLine(pen, b, new Point(b.X + (c.X - b.X) * t2, b.Y + (c.Y - b.Y) * t2));
    }

    private static void DrawCross(DrawingContext context, double progress, IBrush brush)
    {
        var pen = new Pen(brush, 2.6, null, PenLineCap.Round);
        var a = new Point(6.4, 6.4);
        var b = new Point(17.6, 17.6);
        var c = new Point(17.6, 6.4);
        var d = new Point(6.4, 17.6);

        if (progress < 0.5)
        {
            var t = progress / 0.5;
            context.DrawLine(pen, a, new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
            return;
        }

        context.DrawLine(pen, a, b);
        var t2 = (progress - 0.5) / 0.5;
        context.DrawLine(pen, c, new Point(c.X + (d.X - c.X) * t2, c.Y + (d.Y - c.Y) * t2));
    }
}
