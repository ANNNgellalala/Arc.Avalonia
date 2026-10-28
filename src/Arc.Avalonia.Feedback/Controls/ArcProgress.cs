using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

public enum ArcProgressSize
{
    Sm,
    Md,
    Lg
}

public enum ArcProgressTone
{
    Accent,
    Success,
    Warning,
    Danger
}

/// <summary>
/// Determinate (or indeterminate) task progress. One spring drives the fill and the counted
/// percentage together, so the graphic never disagrees with the number.
/// </summary>
public class ArcProgress : TemplatedControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ArcProgress, double>(nameof(Value));

    public static readonly StyledProperty<double> MaxProperty =
        AvaloniaProperty.Register<ArcProgress, double>(nameof(Max), 100d);

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcProgress, string?>(nameof(Label));

    public static readonly StyledProperty<bool> ShowValueProperty =
        AvaloniaProperty.Register<ArcProgress, bool>(nameof(ShowValue));

    public static readonly StyledProperty<ArcProgressSize> SizeProperty =
        AvaloniaProperty.Register<ArcProgress, ArcProgressSize>(nameof(Size));

    public static readonly StyledProperty<ArcProgressTone> ToneProperty =
        AvaloniaProperty.Register<ArcProgress, ArcProgressTone>(nameof(Tone));

    public static readonly StyledProperty<bool> IndeterminateProperty =
        AvaloniaProperty.Register<ArcProgress, bool>(nameof(Indeterminate));

    private ArcProgressTrack? _track;
    private TextBlock? _labelText;
    private TextBlock? _valueText;
    private ArcIcon? _check;
    private Panel? _header;
    private Panel? _valuePanel;
    private readonly ArcTween _tween = new();
    private DispatcherTimer? _sweepTimer;
    private double _displayed;
    private bool _loaded;

    static ArcProgress()
    {
        ValueProperty.Changed.AddClassHandler<ArcProgress>((progress, _) => progress.UpdateValue(animate: true));
        MaxProperty.Changed.AddClassHandler<ArcProgress>((progress, _) => progress.UpdateValue(animate: true));
        ShowValueProperty.Changed.AddClassHandler<ArcProgress>((progress, _) => progress.UpdateHeader());
        LabelProperty.Changed.AddClassHandler<ArcProgress>((progress, _) => progress.UpdateHeader());
        SizeProperty.Changed.AddClassHandler<ArcProgress>((progress, _) => progress.UpdateClasses());
        ToneProperty.Changed.AddClassHandler<ArcProgress>((progress, _) => progress.UpdateClasses());
        IndeterminateProperty.Changed.AddClassHandler<ArcProgress>((progress, _) => progress.UpdateIndeterminate());
    }

    public ArcProgress()
    {
        UpdateClasses();
    }

    /// <summary>Current value, clamped between zero and <see cref="Max"/>.</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Value that counts as complete.</summary>
    public double Max
    {
        get => GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    /// <summary>Visible label and accessible name; assistive tech falls back to "Progress".</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Shows the counted percentage and a check at 100%.</summary>
    public bool ShowValue
    {
        get => GetValue(ShowValueProperty);
        set => SetValue(ShowValueProperty, value);
    }

    public ArcProgressSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Status tone for the fill; use only when the progress itself reports that state.</summary>
    public ArcProgressTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <summary>Sweeps a band while no percentage is known.</summary>
    public bool Indeterminate
    {
        get => GetValue(IndeterminateProperty);
        set => SetValue(IndeterminateProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _track = e.NameScope.Find<ArcProgressTrack>("Bar");
        _labelText = e.NameScope.Find<TextBlock>("LabelText");
        _valueText = e.NameScope.Find<TextBlock>("ValueText");
        _check = e.NameScope.Find<ArcIcon>("Check");
        _header = e.NameScope.Find<Panel>("Header");
        _valuePanel = e.NameScope.Find<Panel>("ValuePanel");

        if (_track is not null)
        {
            _track.Thickness = Size switch
            {
                ArcProgressSize.Sm => 6,
                ArcProgressSize.Lg => 12,
                _ => 8
            };
            _track.Indeterminate = Indeterminate;
        }

        if (_valueText is not null)
        {
            _valueText.FontFeatures = ArcUi.Tabular;
        }

        if (_check is not null)
        {
            _check.Paint(ArcIcon.ForegroundProperty, ArcTokens.Success);
        }

        UpdateHeader();
        UpdateValue(animate: false);
        UpdateIndeterminate();
        ApplyBarBrush();
        _loaded = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_loaded)
        {
            UpdateValue(animate: true);
        }

        UpdateIndeterminate();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _tween.Stop();
        _sweepTimer?.Stop();
    }

    private void UpdateClasses()
    {
        Classes.Set("sm", Size == ArcProgressSize.Sm);
        Classes.Set("md", Size == ArcProgressSize.Md);
        Classes.Set("lg", Size == ArcProgressSize.Lg);
        Classes.Set("success", Tone == ArcProgressTone.Success);
        Classes.Set("warning", Tone == ArcProgressTone.Warning);
        Classes.Set("danger", Tone == ArcProgressTone.Danger);
        Classes.Set("indeterminate", Indeterminate);

        if (_track is not null)
        {
            _track.Thickness = Size switch
            {
                ArcProgressSize.Sm => 6,
                ArcProgressSize.Lg => 12,
                _ => 8
            };
        }

        ApplyBarBrush();
    }

    private void ApplyBarBrush()
    {
        if (_track is null)
        {
            return;
        }

        var complete = !Indeterminate && Fraction() >= 0.999;
        var token = complete
            ? ArcTokens.Success
            : Tone switch
            {
                ArcProgressTone.Success => ArcTokens.Success,
                ArcProgressTone.Warning => ArcTokens.Warning,
                ArcProgressTone.Danger => ArcTokens.Danger,
                _ => ArcTokens.Accent
            };
        _track.Paint(ArcProgressTrack.FillBrushProperty, token);
    }

    private double Fraction()
    {
        var max = Max;
        if (max <= 0 || double.IsNaN(max))
        {
            return 0;
        }

        return Math.Clamp(Value / max, 0, 1);
    }

    private void UpdateValue(bool animate)
    {
        var target = Fraction();
        var complete = target >= 0.999;

        void Apply(double fraction)
        {
            _displayed = fraction;
            if (_track is not null)
            {
                _track.Fraction = fraction;
            }

            if (_valueText is not null)
            {
                _valueText.Text = $"{Math.Round(fraction * 100)}%";
            }

            if (_check is not null)
            {
                _check.IsVisible = ShowValue && !Indeterminate && fraction >= 0.999;
            }

            AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Label) ? "Progress" : Label);
            AutomationProperties.SetHelpText(this, Indeterminate ? "Working" : $"{Math.Round(fraction * 100)} percent complete");
        }

        var shouldAnimate = animate && _loaded;
        if (shouldAnimate)
        {
            _tween.Run(_displayed, target, TimeSpan.FromMilliseconds(420), Apply, () => Apply(target));
        }
        else
        {
            _tween.Stop();
            Apply(target);
        }

        Classes.Set("complete", complete);
        ApplyBarBrush();
    }

    private void UpdateHeader()
    {
        if (_header is not null)
        {
            _header.IsVisible = !string.IsNullOrWhiteSpace(Label) || ShowValue;
        }

        if (_labelText is not null)
        {
            _labelText.Text = Label;
            _labelText.IsVisible = !string.IsNullOrWhiteSpace(Label);
        }

        if (_valuePanel is not null)
        {
            _valuePanel.IsVisible = ShowValue;
        }

        UpdateValue(animate: false);
    }

    private void UpdateIndeterminate()
    {
        Classes.Set("indeterminate", Indeterminate);
        if (_track is not null)
        {
            _track.Indeterminate = Indeterminate;
            _track.Sweep = ArcThemeManager.ReducedMotion ? 0.38 : 0;
        }

        _sweepTimer?.Stop();
        _sweepTimer = null;

        if (!Indeterminate)
        {
            UpdateValue(animate: false);
            return;
        }

        if (ArcThemeManager.ReducedMotion || _track is null)
        {
            return;
        }

        var started = DateTime.UtcNow;
        _sweepTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            var seconds = (DateTime.UtcNow - started).TotalSeconds;
            if (_track is not null)
            {
                _track.Sweep = (seconds % 1.6) / 1.6;
            }
        });
        _sweepTimer.Start();
    }
}

/// <summary>The drawn track and fill; rounded like a pill so the shared progress language holds.</summary>
public sealed class ArcProgressTrack : Control
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<ArcProgressTrack, double>(nameof(Fraction));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<ArcProgressTrack, double>(nameof(Thickness), 8d);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<ArcProgressTrack, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<ArcProgressTrack, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<bool> IndeterminateProperty =
        AvaloniaProperty.Register<ArcProgressTrack, bool>(nameof(Indeterminate));

    public static readonly StyledProperty<double> SweepProperty =
        AvaloniaProperty.Register<ArcProgressTrack, double>(nameof(Sweep));

    static ArcProgressTrack()
    {
        AffectsRender<ArcProgressTrack>(FractionProperty, ThicknessProperty, TrackBrushProperty, FillBrushProperty, IndeterminateProperty, SweepProperty);
        AffectsMeasure<ArcProgressTrack>(ThicknessProperty);
    }

    public double Fraction
    {
        get => GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public bool Indeterminate
    {
        get => GetValue(IndeterminateProperty);
        set => SetValue(IndeterminateProperty, value);
    }

    /// <summary>Band position from 0 to 1 for the indeterminate sweep.</summary>
    public double Sweep
    {
        get => GetValue(SweepProperty);
        set => SetValue(SweepProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, Math.Max(2, Thickness));

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TrackBrushProperty && TrackBrush is null)
        {
            this.Paint(TrackBrushProperty, ArcTokens.ControlTrack);
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var thickness = Math.Max(2, Math.Min(Thickness, Bounds.Height));
        var radius = thickness / 2;
        var top = (Bounds.Height - thickness) / 2;
        var trackBrush = TrackBrush ?? ArcResources.Brush(ArcTokens.ControlTrack);
        var fillBrush = FillBrush ?? ArcResources.Brush(ArcTokens.Accent);

        var track = new Rect(0, top, Bounds.Width, thickness);
        context.DrawRectangle(trackBrush, null, track, radius, radius);

        using (context.PushClip(new RoundedRect(track, radius)))
        {
            if (Indeterminate)
            {
                var band = Math.Max(thickness, Bounds.Width * 0.34);
                var x = Sweep * (Bounds.Width + band) - band;
                context.DrawRectangle(fillBrush, null, new Rect(x, top, band, thickness), radius, radius);
                return;
            }

            var width = Math.Clamp(Fraction, 0, 1) * Bounds.Width;
            if (width <= 0.25)
            {
                return;
            }

            var drawWidth = Math.Max(thickness, width);
            context.DrawRectangle(fillBrush, null, new Rect(0, top, drawWidth, thickness), radius, radius);
        }
    }
}
