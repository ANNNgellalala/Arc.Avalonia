using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Effects.Controls;

/// <summary>
/// Morphs one short label into the next in place: the old label lifts and blurs away while the new one
/// sharpens in from below, and the frame springs to the new width. Reduced motion swaps both instantly.
/// The label never wraps, so keep it short.
/// </summary>
public class ArcTextMorph : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ArcTextMorph, string?>(nameof(Text));

    public static readonly StyledProperty<ArcTextLevel> LevelProperty =
        AvaloniaProperty.Register<ArcTextMorph, ArcTextLevel>(nameof(Level), ArcTextLevel.Span);

    private static readonly TimeSpan MorphDuration = ArcMotion.SpringMorphDuration;
    private static readonly TimeSpan TweenDuration = ArcMotion.Standard;

    private Border? _frame;
    private Panel? _layers;
    private TextBlock? _current;
    private ArcTween? _tween;
    private double _width;

    static ArcTextMorph()
    {
        TextProperty.Changed.AddClassHandler<ArcTextMorph>((morph, _) => morph.Update(animate: true));
        LevelProperty.Changed.AddClassHandler<ArcTextMorph>((morph, _) => morph.Update(animate: false));
    }

    public ArcTextMorph()
    {
        Focusable = false;
    }

    /// <summary>The current label. Change it to morph; it never wraps.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ArcTextLevel Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _frame = e.NameScope.Find<Border>("PART_Frame");
        _layers = e.NameScope.Find<Panel>("PART_Layers");
        _current = null;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _tween?.Stop();
        _tween = null;
    }

    private void Rebuild()
    {
        if (_layers is null)
        {
            return;
        }

        _tween?.Stop();
        _tween = null;
        _layers.Children.Clear();
        _current = null;
        if (_frame is not null)
        {
            _frame.Width = double.NaN;
        }

        Update(animate: false);
    }

    private void Update(bool animate)
    {
        if (_layers is null || _frame is null)
        {
            return;
        }

        var text = Text ?? string.Empty;
        AutomationProperties.SetName(this, text);

        if (_current is not null && _current.Text == text)
        {
            return;
        }

        var next = ArcEffectVisuals.Text(text, ArcText.ClassOf(Level), ArcText.FontSizeOf(Level));
        next.TextWrapping = TextWrapping.NoWrap;

        if (_current is null || !animate || ArcThemeManager.ReducedMotion)
        {
            _tween?.Stop();
            _tween = null;
            _layers.Children.Clear();
            _layers.Children.Add(next);
            _current = next;
            _width = Measure(next);
            _frame.Width = _width;
            return;
        }

        var previous = _current;
        var from = _width > 0 ? _width : Measure(previous);
        var to = Measure(next);
        _width = to;
        _current = next;
        next.Opacity = 0;
        var nextMove = new TranslateTransform(0, 6);
        next.RenderTransform = nextMove;
        next.Effect = new BlurEffect { Radius = 4 };
        _layers.Children.Add(next);

        var previousMove = new TranslateTransform(0, 0);
        previous.RenderTransform = previousMove;
        previous.Effect = null;

        var total = Math.Max(MorphDuration.TotalSeconds, TweenDuration.TotalSeconds);

        _tween?.Stop();
        _tween = ArcTween.Run(TimeSpan.FromSeconds(total), ArcEffectVisuals.Linear, progress =>
        {
            var elapsed = progress * total;

            var outProgress = Math.Clamp(elapsed / TweenDuration.TotalSeconds, 0, 1);
            previous.Opacity = 1 - ArcMotion.EaseExit.Ease(outProgress);
            previousMove.Y = -4 * ArcMotion.EaseExit.Ease(outProgress);

            var inProgress = Math.Clamp(elapsed / TweenDuration.TotalSeconds, 0, 1);
            var easedIn = ArcMotion.EaseEnter.Ease(inProgress);
            next.Opacity = easedIn;
            nextMove.Y = 6 * (1 - easedIn);
            next.Effect = inProgress >= 1 ? null : new BlurEffect { Radius = 4 * (1 - easedIn) };

            var widthProgress = Math.Clamp(elapsed / MorphDuration.TotalSeconds, 0, 1);
            _frame.Width = Math.Max(0, from + (to - from) * ArcMotion.SpringMorph.Ease(widthProgress));
        }, () =>
        {
            _layers.Children.Remove(previous);
            next.Effect = null;
            nextMove.Y = 0;
            next.Opacity = 1;
            _frame.Width = to;
        });
    }

    private static double Measure(TextBlock block)
    {
        block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return Math.Ceiling(block.DesiredSize.Width);
    }
}
