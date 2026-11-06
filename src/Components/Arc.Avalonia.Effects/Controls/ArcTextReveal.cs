using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;
using Avalonia.Controls.Metadata;

namespace Arc.Avalonia.Effects.Controls;

/// <summary>
/// Reveals a short headline once: every word (or character) rises out of place while it fades in, on a
/// per-unit stagger whose total is capped so long copy never drags. Reduced motion shows the final
/// state immediately, and the accessibility name is the whole sentence rather than the fragments.
/// </summary>
[TemplatePart("PART_Lines", typeof(StackPanel))]
public class ArcTextReveal : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ArcTextReveal, string?>(nameof(Text));

    public static readonly StyledProperty<ArcTextLevel> LevelProperty =
        AvaloniaProperty.Register<ArcTextReveal, ArcTextLevel>(nameof(Level), ArcTextLevel.H2);

    public static readonly StyledProperty<ArcRevealTrigger> TriggerProperty =
        AvaloniaProperty.Register<ArcTextReveal, ArcRevealTrigger>(nameof(Trigger));

    public static readonly StyledProperty<ArcTextUnit> UnitProperty =
        AvaloniaProperty.Register<ArcTextReveal, ArcTextUnit>(nameof(Unit));

    /// <summary>Seconds before the first unit rises, matching Arc's `delay`.</summary>
    public static readonly StyledProperty<double> DelayProperty =
        AvaloniaProperty.Register<ArcTextReveal, double>(nameof(Delay));

    /// <summary>Milliseconds between units; the total stagger is capped at 400 ms.</summary>
    public static readonly StyledProperty<int> StaggerProperty =
        AvaloniaProperty.Register<ArcTextReveal, int>(nameof(Stagger), 40);

    private static readonly TimeSpan RiseDuration = ArcMotion.Considered;

    private readonly List<TextBlock> _units = new();
    private readonly List<TranslateTransform> _moves = new();
    private StackPanel? _host;
    private ArcViewport? _watch;
    private ArcTween? _tween;
    private bool _played;
    private bool _attached;

    static ArcTextReveal()
    {
        TextProperty.Changed.AddClassHandler<ArcTextReveal>((reveal, _) => reveal.Build());
        LevelProperty.Changed.AddClassHandler<ArcTextReveal>((reveal, _) => reveal.Build());
        UnitProperty.Changed.AddClassHandler<ArcTextReveal>((reveal, _) => reveal.Build());
        StaggerProperty.Changed.AddClassHandler<ArcTextReveal>((reveal, _) => reveal.Build());
        DelayProperty.Changed.AddClassHandler<ArcTextReveal>((reveal, _) => reveal.Build());
        TriggerProperty.Changed.AddClassHandler<ArcTextReveal>((reveal, _) =>
        {
            reveal.Disarm();
            reveal.Build();
        });
    }

    public ArcTextReveal()
    {
        Focusable = false;
    }

    /// <summary>The copy to reveal. Use \n for a deliberate line break.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Heading level; keeps the page outline correct.</summary>
    public ArcTextLevel Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    /// <summary>Immediate plays on first paint; Visible waits until the text enters the viewport.</summary>
    public ArcRevealTrigger Trigger
    {
        get => GetValue(TriggerProperty);
        set => SetValue(TriggerProperty, value);
    }

    public ArcTextUnit Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public double Delay
    {
        get => GetValue(DelayProperty);
        set => SetValue(DelayProperty, value);
    }

    public int Stagger
    {
        get => GetValue(StaggerProperty);
        set => SetValue(StaggerProperty, value);
    }

    /// <summary>Plays the entrance again, the analogue of remounting the React component.</summary>
    public void Replay()
    {
        _tween?.Stop();
        _tween = null;
        _played = false;
        ApplyStart();
        Play();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<StackPanel>("PART_Lines");
        Build();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _played = false;
        ApplyStart();
        Arm();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _tween?.Stop();
        _tween = null;
        Disarm();
    }

    private void Build()
    {
        _units.Clear();
        _moves.Clear();
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        var level = Level;
        var space = ArcText.SpaceWidth(level);

        foreach (var line in ArcText.Lines(Text))
        {
            var row = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            foreach (var run in ArcText.Runs(line, Unit))
            {
                var unit = ArcText.Unit(run, level);
                if (Unit == ArcTextUnit.Word)
                {
                    unit.Margin = new Thickness(0, 0, space, 0);
                }

                var move = new TranslateTransform(0, 0);
                unit.RenderTransform = move;
                _units.Add(unit);
                _moves.Add(move);
                row.Children.Add(unit);
            }

            _host.Children.Add(row);
        }

        AutomationProperties.SetName(this, string.Join(" ", ArcText.Lines(Text)));
        ApplyStart();
        if (_attached)
        {
            _played = false;
            Arm();
        }
    }

    private void Arm()
    {
        Disarm();
        if (_played || _units.Count == 0)
        {
            return;
        }

        if (ArcThemeManager.ReducedMotion || Trigger == ArcRevealTrigger.Immediate)
        {
            Play();
            return;
        }

        _watch = ArcViewport.Watch(this, Play, once: true);
    }

    private void Disarm()
    {
        _watch?.Dispose();
        _watch = null;
    }

    private void ApplyStart()
    {
        if (ArcThemeManager.ReducedMotion || _played)
        {
            ApplyEnd();
            return;
        }

        var rise = ArcText.FontSizeOf(Level) * 0.45;
        for (var i = 0; i < _units.Count; i++)
        {
            _units[i].Opacity = 0;
            _moves[i].Y = rise;
        }
    }

    private void ApplyEnd()
    {
        for (var i = 0; i < _units.Count; i++)
        {
            _units[i].Opacity = 1;
            _moves[i].Y = 0;
        }
    }

    private void Play()
    {
        if (_units.Count == 0)
        {
            return;
        }

        if (_played)
        {
            return;
        }

        _played = true;
        _watch?.Dispose();
        _watch = null;

        if (ArcThemeManager.ReducedMotion)
        {
            ApplyEnd();
            return;
        }

        var count = _units.Count;
        var rise = ArcText.FontSizeOf(Level) * 0.45;
        var stagger = Math.Min(Math.Max(0, Stagger), count > 1 ? 400.0 / (count - 1) : 0);
        var delay = Math.Max(0, Delay);
        var total = delay + (count - 1) * stagger / 1000.0 + RiseDuration.TotalSeconds;

        ApplyStart();
        _tween?.Stop();
        _tween = ArcTween.Run(TimeSpan.FromSeconds(total), ArcEffectVisuals.Linear, progress =>
        {
            var elapsed = progress * total;
            for (var i = 0; i < count; i++)
            {
                var local = Math.Clamp((elapsed - delay - i * stagger / 1000.0) / RiseDuration.TotalSeconds, 0, 1);
                var eased = ArcMotion.EaseEnter.Ease(local);
                _units[i].Opacity = eased;
                _moves[i].Y = (1 - eased) * rise;
            }
        });
    }
}
