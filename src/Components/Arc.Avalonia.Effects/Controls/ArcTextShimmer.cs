using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;
using Avalonia.Controls.Metadata;

namespace Arc.Avalonia.Effects.Controls;

/// <summary>
/// A calm light band sweeping across one short status line while work is ongoing. The band pauses with
/// the text when the control is disabled, stops entirely under reduced motion, and the label rises into
/// place when it changes. It is a status region, not a progress bar.
/// </summary>
[TemplatePart("PART_Layers", typeof(Panel))]
public class ArcTextShimmer : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ArcTextShimmer, string?>(nameof(Text));

    public static readonly StyledProperty<bool> ActiveProperty =
        AvaloniaProperty.Register<ArcTextShimmer, bool>(nameof(Active), true);

    /// <summary>Seconds for one sweep across the text.</summary>
    public static readonly StyledProperty<double> DurationProperty =
        AvaloniaProperty.Register<ArcTextShimmer, double>(nameof(Duration), 1.8);

    public static readonly StyledProperty<ArcTextLevel> LevelProperty =
        AvaloniaProperty.Register<ArcTextShimmer, ArcTextLevel>(nameof(Level), ArcTextLevel.Span);

    private Panel? _layers;
    private TextBlock? _current;
    private DispatcherTimer? _sweep;
    private ArcTween? _tween;
    private DateTime _started;

    static ArcTextShimmer()
    {
        TextProperty.Changed.AddClassHandler<ArcTextShimmer>((shimmer, _) => shimmer.UpdateText());
        ActiveProperty.Changed.AddClassHandler<ArcTextShimmer>((shimmer, _) => shimmer.SyncSweep());
        DurationProperty.Changed.AddClassHandler<ArcTextShimmer>((shimmer, _) => shimmer.SyncSweep());
        LevelProperty.Changed.AddClassHandler<ArcTextShimmer>((shimmer, _) => shimmer.Rebuild());
    }

    public ArcTextShimmer()
    {
        Focusable = false;
    }

    /// <summary>One short line of status text. A new value rises in place.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Sweeps while true; false settles the text to its solid colour.</summary>
    public bool Active
    {
        get => GetValue(ActiveProperty);
        set => SetValue(ActiveProperty, value);
    }

    public double Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public ArcTextLevel Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _layers = e.NameScope.Find<Panel>("PART_Layers");
        _current = null;
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncSweep();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopSweep();
        _tween?.Stop();
        _tween = null;
    }

    private void Rebuild()
    {
        if (_layers is null)
        {
            return;
        }

        _layers.Children.Clear();
        _current = null;
        UpdateText();
    }

    private void UpdateText()
    {
        if (_layers is null)
        {
            return;
        }

        var text = Text ?? string.Empty;
        AutomationProperties.SetName(this, text);
        AutomationProperties.SetHelpText(this, Active ? "In progress" : "Done");
        AutomationProperties.SetLiveSetting(this, Active ? AutomationLiveSetting.Polite : AutomationLiveSetting.Off);

        var next = ArcEffectVisuals.Text(text, ArcText.ClassOf(Level), ArcText.FontSizeOf(Level));
        next.TextWrapping = TextWrapping.NoWrap;

        if (_current is null || _current.Text == text || ArcThemeManager.ReducedMotion)
        {
            _layers.Children.Clear();
            _layers.Children.Add(next);
            _current = next;
            ApplyBrush();
            SyncSweep();
            return;
        }

        var previous = _current;
        _current = next;
        next.Opacity = 0;
        var move = new TranslateTransform(0, 6);
        next.RenderTransform = move;
        _layers.Children.Add(next);

        _tween?.Stop();
        _tween = ArcTween.Run(ArcMotion.Standard, ArcMotion.EaseEnter, progress =>
        {
            previous.Opacity = 1 - progress;
            next.Opacity = progress;
            move.Y = 6 * (1 - progress);
        }, () =>
        {
            _layers.Children.Remove(previous);
            move.Y = 0;
            next.Opacity = 1;
        });

        ApplyBrush();
        SyncSweep();
    }

    private void SyncSweep()
    {
        StopSweep();
        if (!Active || ArcThemeManager.ReducedMotion)
        {
            ApplyBrush();
            return;
        }

        _started = DateTime.UtcNow;
        _sweep = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => ApplyBrush());
        _sweep.Start();
        ApplyBrush();
    }

    private void StopSweep()
    {
        _sweep?.Stop();
        _sweep = null;
    }

    private void ApplyBrush()
    {
        if (_current is null)
        {
            return;
        }

        if (!Active || ArcThemeManager.ReducedMotion)
        {
            _current.Foreground = ArcEffectVisuals.Brush(ArcTokens.Foreground);
            return;
        }

        var duration = Math.Max(0.3, Duration);
        var phase = ((DateTime.UtcNow - _started).TotalSeconds % duration) / duration;

        // A quiet muted base with a bell-shaped band in the text colour, sweeping left to right.
        var baseColor = ArcResources.Color(ArcTokens.TextMuted);
        var bandColor = ArcResources.Color(ArcTokens.Foreground);
        const double width = 0.2;
        var centre = -0.25 + phase * 1.5;
        var stops = new List<(double Offset, Color Colour)>
        {
            (0, baseColor),
            (centre - width, ArcColor.Mix(baseColor, bandColor, 0.42)),
            (centre, bandColor),
            (centre + width, ArcColor.Mix(baseColor, bandColor, 0.42)),
            (1, baseColor)
        };

        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative)
        };

        foreach (var stop in stops.OrderBy(stop => stop.Offset))
        {
            brush.GradientStops.Add(new GradientStop(stop.Colour, Math.Clamp(stop.Offset, 0, 1)));
        }

        _current.Foreground = brush;
    }
}
