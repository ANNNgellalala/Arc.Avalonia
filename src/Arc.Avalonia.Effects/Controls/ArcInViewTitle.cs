using System;
using System.Collections.Generic;
using System.Linq;
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
/// A section heading that brings itself in once it scrolls into view, in Arc's five styles: blur, word,
/// line, tracking and wipe. Opacity leads, the blur clears next and the transform settles last so the
/// words are readable before they arrive. Reduced motion shows the title instantly with no mask.
/// </summary>
public class ArcInViewTitle : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ArcInViewTitle, string?>(nameof(Text));

    public static readonly StyledProperty<ArcInViewVariant> VariantProperty =
        AvaloniaProperty.Register<ArcInViewTitle, ArcInViewVariant>(nameof(Variant), ArcInViewVariant.Blur);

    public static readonly StyledProperty<ArcTextLevel> LevelProperty =
        AvaloniaProperty.Register<ArcInViewTitle, ArcTextLevel>(nameof(Level), ArcTextLevel.H2);

    public static readonly StyledProperty<IReadOnlyList<string>?> LinesProperty =
        AvaloniaProperty.Register<ArcInViewTitle, IReadOnlyList<string>?>(nameof(Lines));

    public static readonly StyledProperty<bool> OnceProperty =
        AvaloniaProperty.Register<ArcInViewTitle, bool>(nameof(Once), true);

    private static readonly TimeSpan RevealDuration = ArcMotion.Considered;
    private static readonly TimeSpan Stagger = TimeSpan.FromMilliseconds(80);

    private readonly List<TextBlock> _units = new();
    private readonly List<TranslateTransform> _moves = new();
    private StackPanel? _host;
    private ArcViewport? _watch;
    private ArcTween? _tween;
    private TextBlock? _single;
    private double _wipeWidth;
    private bool _attached;

    static ArcInViewTitle()
    {
        TextProperty.Changed.AddClassHandler<ArcInViewTitle>((title, _) => title.Build());
        VariantProperty.Changed.AddClassHandler<ArcInViewTitle>((title, _) => title.Build());
        LevelProperty.Changed.AddClassHandler<ArcInViewTitle>((title, _) => title.Build());
        LinesProperty.Changed.AddClassHandler<ArcInViewTitle>((title, _) => title.Build());
        OnceProperty.Changed.AddClassHandler<ArcInViewTitle>((title, _) => title.Build());
    }

    public ArcInViewTitle()
    {
        Focusable = false;
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ArcInViewVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public ArcTextLevel Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    /// <summary>Explicit line breaks for the line variant; other variants wrap on their own.</summary>
    public IReadOnlyList<string>? Lines
    {
        get => GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    /// <summary>False replays the reveal on every entry.</summary>
    public bool Once
    {
        get => GetValue(OnceProperty);
        set => SetValue(OnceProperty, value);
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
        Arm();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _tween?.Stop();
        _tween = null;
        _watch?.Dispose();
        _watch = null;
    }

    private void Build()
    {
        _units.Clear();
        _moves.Clear();
        _single = null;
        _wipeWidth = 0;
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        switch (Variant)
        {
            case ArcInViewVariant.Word:
                BuildFlow();
                break;
            case ArcInViewVariant.Line:
                BuildLines();
                break;
            default:
                BuildSingle();
                break;
        }

        AutomationProperties.SetName(this, Text ?? string.Empty);
        AutomationProperties.SetHeadingLevel(this, HeadingLevelOf(Level));
        ApplyStart();
        if (_attached)
        {
            Arm();
        }
    }

    private static int HeadingLevelOf(ArcTextLevel level) => level switch
    {
        ArcTextLevel.H1 => 1,
        ArcTextLevel.H2 => 2,
        _ => 3
    };

    private void BuildFlow()
    {
        if (_host is null)
        {
            return;
        }

        var level = Level;
        var space = ArcText.SpaceWidth(level);
        foreach (var line in ArcText.Lines(Text))
        {
            var row = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            foreach (var run in ArcText.Runs(line, ArcTextUnit.Word))
            {
                var unit = ArcText.Unit(run, level);
                unit.Margin = new Thickness(0, 0, space, 0);
                var move = new TranslateTransform(0, 0);
                unit.RenderTransform = move;
                _units.Add(unit);
                _moves.Add(move);
                row.Children.Add(unit);
            }

            _host.Children.Add(ClipBox(row));
        }
    }

    private void BuildLines()
    {
        if (_host is null)
        {
            return;
        }

        var level = Level;
        var lines = Lines is { Count: > 0 } explicitLines
            ? explicitLines.ToArray()
            : ArcText.Lines(Text);
        foreach (var line in lines)
        {
            var unit = ArcText.Unit(line, level);
            var move = new TranslateTransform(0, 0);
            unit.RenderTransform = move;
            _units.Add(unit);
            _moves.Add(move);
            _host.Children.Add(ClipBox(unit));
        }
    }

    /// <summary>A line-height box that hides the unit until it rises into place.</summary>
    private static Border ClipBox(Control child) => new()
    {
        ClipToBounds = true,
        HorizontalAlignment = HorizontalAlignment.Left,
        Child = child
    };

    private void BuildSingle()
    {
        if (_host is null)
        {
            return;
        }

        var level = Level;
        var block = ArcText.Unit(Text ?? string.Empty, level);
        block.TextWrapping = TextWrapping.Wrap;
        block.MaxWidth = ArcText.FontSizeOf(level) * 0.55 * 24;
        _single = block;
        _units.Add(block);

        if (Variant == ArcInViewVariant.Tracking)
        {
            block.LetterSpacing = TrackingEnd(level) + ArcText.FontSizeOf(level) * 0.16;
        }

        var move = new TranslateTransform(0, 0);
        block.RenderTransform = move;
        _moves.Add(move);
        _host.Children.Add(Variant == ArcInViewVariant.Wipe ? ClipBox(block) : block);

        if (Variant == ArcInViewVariant.Wipe)
        {
            LayoutUpdated -= MeasureWipe;
            LayoutUpdated += MeasureWipe;
        }
    }

    private void MeasureWipe(object? sender, EventArgs e)
    {
        if (_single is null || _single.Bounds.Width <= 0)
        {
            return;
        }

        _wipeWidth = _single.Bounds.Width;
        LayoutUpdated -= MeasureWipe;
        if (!_attached || ArcThemeManager.ReducedMotion)
        {
            ApplyEnd();
        }
    }

    private static double TrackingEnd(ArcTextLevel level) =>
        ArcEffectVisuals.Size(ArcTokens.TrackingDisplay) * ArcText.FontSizeOf(level);

    private void Arm()
    {
        _watch?.Dispose();
        _watch = null;
        if (_units.Count == 0)
        {
            return;
        }

        if (ArcThemeManager.ReducedMotion)
        {
            ApplyEnd();
            return;
        }

        ArcViewport.Watch(
            this,
            Play,
            once: Once,
            threshold: 0.45,
            onHidden: Once ? null : HideForReplay);
    }

    private void ApplyStart()
    {
        if (ArcThemeManager.ReducedMotion)
        {
            ApplyEnd();
            return;
        }

        var rise = ArcText.FontSizeOf(Level) * 0.35;
        for (var i = 0; i < _units.Count; i++)
        {
            _units[i].Opacity = Variant == ArcInViewVariant.Wipe ? 1 : 0;
            if (_moves.Count > i)
            {
                _moves[i].Y = rise;
                _moves[i].X = Variant == ArcInViewVariant.Wipe && _wipeWidth > 0 ? -_wipeWidth : 0;
            }
        }

        if (_single is not null && Variant == ArcInViewVariant.Tracking)
        {
            _single.LetterSpacing = TrackingEnd(Level) + ArcText.FontSizeOf(Level) * 0.16;
        }

        if (_single is not null && Variant == ArcInViewVariant.Blur)
        {
            _single.Effect = new BlurEffect { Radius = 10 };
        }
    }

    private void ApplyEnd()
    {
        foreach (var unit in _units)
        {
            unit.Opacity = 1;
            unit.Effect = null;
        }

        foreach (var move in _moves)
        {
            move.Y = 0;
            move.X = 0;
        }

        if (_single is not null)
        {
            _single.LetterSpacing = TrackingEnd(Level);
        }
    }

    private void HideForReplay()
    {
        _tween?.Stop();
        _tween = null;
        ApplyStart();
    }

    private void Play()
    {
        if (_units.Count == 0)
        {
            return;
        }

        if (ArcThemeManager.ReducedMotion)
        {
            ApplyEnd();
            return;
        }

        _tween?.Stop();
        ApplyStart();

        if (Variant is ArcInViewVariant.Blur or ArcInViewVariant.Tracking or ArcInViewVariant.Wipe)
        {
            PlaySingle();
            return;
        }

        var count = _units.Count;
        var rise = ArcText.FontSizeOf(Level) * 0.35;
        var total = (count - 1) * Stagger.TotalSeconds + RevealDuration.TotalSeconds;

        _tween = ArcTween.Run(TimeSpan.FromSeconds(total), ArcEffectVisuals.Linear, progress =>
        {
            var elapsed = progress * total;
            for (var i = 0; i < count; i++)
            {
                var local = Math.Clamp((elapsed - i * Stagger.TotalSeconds) / RevealDuration.TotalSeconds, 0, 1);
                var eased = ArcMotion.EaseEnter.Ease(local);
                _units[i].Opacity = eased;
                _moves[i].Y = (1 - eased) * rise;
            }
        });
    }

    /// <summary>
    /// Blur, tracking and wipe all drive one element: opacity resolves first, then the blur clears (or the
    /// mask uncovers), and the short lift settles last.
    /// </summary>
    private void PlaySingle()
    {
        if (_single is null)
        {
            return;
        }

        var block = _single;
        var move = _moves[0];
        var level = Level;
        var fontSize = ArcText.FontSizeOf(level);
        var trackingFrom = TrackingEnd(level) + fontSize * 0.16;
        var trackingTo = TrackingEnd(level);
        var wipeFrom = Variant == ArcInViewVariant.Wipe && _wipeWidth > 0 ? -_wipeWidth : 0;
        var total = RevealDuration.TotalSeconds;
        var blurRadius = Variant == ArcInViewVariant.Blur ? 10 : 0;

        block.Opacity = Variant == ArcInViewVariant.Wipe ? 1 : 0;
        move.Y = fontSize * 0.3;
        move.X = wipeFrom;

        _tween = ArcTween.Run(RevealDuration, ArcEffectVisuals.Linear, progress =>
        {
            var opacity = Math.Clamp(progress / 0.45, 0, 1);
            var blur = Math.Clamp((progress - 0.2) / 0.6, 0, 1);
            var settle = Math.Clamp((progress - 0.35) / 0.65, 0, 1);

            block.Opacity = Variant == ArcInViewVariant.Wipe ? 1 : ArcMotion.EaseEnter.Ease(opacity);
            move.Y = (1 - ArcMotion.EaseEnter.Ease(settle)) * fontSize * 0.3;
            move.X = wipeFrom * (1 - ArcMotion.EaseEnter.Ease(settle));

            if (Variant == ArcInViewVariant.Blur)
            {
                block.Effect = blurRadius <= 0 ? null : new BlurEffect { Radius = blurRadius * (1 - ArcMotion.EaseEnter.Ease(blur)) };
            }
            else if (Variant == ArcInViewVariant.Tracking)
            {
                block.LetterSpacing = trackingFrom + (trackingTo - trackingFrom) * ArcMotion.EaseEnter.Ease(blur);
            }
        }, () =>
        {
            block.Effect = null;
            block.LetterSpacing = trackingTo;
        });
    }
}
