using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.AdvancedActions.Controls;

public enum ArcIconMorphName
{
    /// <summary>Menu to close: the lines turn into an X.</summary>
    Menu,

    /// <summary>Play to pause.</summary>
    Play,

    /// <summary>Plus to check, for add and copy actions.</summary>
    Plus,

    /// <summary>Copy to check.</summary>
    Copy,

    /// <summary>Sun to moon.</summary>
    Theme,

    /// <summary>A chevron that turns for disclosure and sort controls.</summary>
    Chevron
}

public enum ArcChevronPointing
{
    Down,
    Right
}

/// <summary>
/// Two-state icon built from two stacked <see cref="ArcIcon"/> layers. Because Avalonia's icon set is path
/// data rather than animatable SVG, true path interpolation is approximated: the outgoing layer fades,
/// turns and scales away while the incoming layer arrives from the opposite direction on the snappy
/// spring. That keeps the causal read of the Arc morph (menu folding to close, play splitting to pause)
/// without re-authoring every path as keyframes. Icons are decorative; the button around them must carry
/// the accessible name and the state. The bell and heart one-shot moments are not ported: the shared icon
/// set has no filled or burst primitives to animate.
/// </summary>
public sealed class ArcIconMorph : TemplatedControl
{
    public static readonly StyledProperty<ArcIconMorphName> IconProperty =
        AvaloniaProperty.Register<ArcIconMorph, ArcIconMorphName>(nameof(Icon));

    public static readonly StyledProperty<bool> StateProperty =
        AvaloniaProperty.Register<ArcIconMorph, bool>(nameof(State));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<ArcIconMorph, double>(nameof(Size), 24);

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<ArcIconMorph, double>(nameof(StrokeWidth), 1.75);

    public static readonly StyledProperty<double> TurnProperty =
        AvaloniaProperty.Register<ArcIconMorph, double>(nameof(Turn), 180);

    public static readonly StyledProperty<ArcChevronPointing> PointingProperty =
        AvaloniaProperty.Register<ArcIconMorph, ArcChevronPointing>(nameof(Pointing));

    private ArcIcon? _from;
    private ArcIcon? _to;

    static ArcIconMorph()
    {
        AffectsMeasure<ArcIconMorph>(SizeProperty);
        IconProperty.Changed.AddClassHandler<ArcIconMorph>((icon, _) => icon.Apply(animate: true));
        StateProperty.Changed.AddClassHandler<ArcIconMorph>((icon, _) => icon.Apply(animate: true));
        SizeProperty.Changed.AddClassHandler<ArcIconMorph>((icon, _) => icon.Apply(animate: false));
        StrokeWidthProperty.Changed.AddClassHandler<ArcIconMorph>((icon, _) => icon.Apply(animate: false));
        TurnProperty.Changed.AddClassHandler<ArcIconMorph>((icon, _) => icon.Apply(animate: true));
        PointingProperty.Changed.AddClassHandler<ArcIconMorph>((icon, _) => icon.Apply(animate: false));
    }

    public ArcIconMorph()
    {
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>Which pair to show.</summary>
    public ArcIconMorphName Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>The second state: menu to close, play to pause, plus or copy to check, sun to moon, chevron turned.</summary>
    public bool State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Rendered size in px.</summary>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    /// <summary>Degrees the chevron turns when <see cref="State"/> is true. 180 flips down to up; 90 turns right to down.</summary>
    public double Turn
    {
        get => GetValue(TurnProperty);
        set => SetValue(TurnProperty, value);
    }

    /// <summary>Where the chevron points at rest.</summary>
    public ArcChevronPointing Pointing
    {
        get => GetValue(PointingProperty);
        set => SetValue(PointingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _from = e.NameScope.Find<ArcIcon>("From");
        _to = e.NameScope.Find<ArcIcon>("To");
        Apply(animate: false);
    }

    private void Apply(bool animate)
    {
        if (_from is null || _to is null)
        {
            return;
        }

        var reduced = ArcThemeManager.ReducedMotion;
        ApplyMotion(_from, animate && !reduced);
        ApplyMotion(_to, animate && !reduced);

        _from.Size = Size;
        _to.Size = Size;
        _from.StrokeWidth = StrokeWidth;
        _to.StrokeWidth = StrokeWidth;

        if (Icon == ArcIconMorphName.Chevron)
        {
            var name = Pointing == ArcChevronPointing.Right ? "chevron-right" : "chevron-down";
            _from.Icon = name;
            _to.Icon = name;
            _from.Opacity = 1;
            _to.Opacity = 0;
            _from.RenderTransform = TransformOperations.Parse($"rotate({(State ? Turn : 0).ToString(System.Globalization.CultureInfo.InvariantCulture)}deg)");
            _to.RenderTransform = TransformOperations.Parse("rotate(0deg)");
            return;
        }

        var (from, to, outTransform, inTransform) = Pair(Icon);
        _from.Icon = from;
        _to.Icon = to;
        _from.Opacity = State ? 0 : 1;
        _to.Opacity = State ? 1 : 0;
        _from.RenderTransform = TransformOperations.Parse(State ? outTransform : "rotate(0deg) scale(1) translate(0px, 0px)");
        _to.RenderTransform = TransformOperations.Parse(State ? "rotate(0deg) scale(1) translate(0px, 0px)" : inTransform);
    }

    private static void ApplyMotion(ArcIcon icon, bool animate)
    {
        icon.RenderTransformOrigin = RelativePoint.Center;
        if (!animate)
        {
            icon.Transitions = null;
            return;
        }

        icon.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = ArcMotion.Fast,
                Easing = ArcMotion.EaseStandard
            },
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = ArcMotion.SpringSnappyDuration,
                Easing = ArcMotion.SpringSnappy
            }
        };
    }

    private static (string From, string To, string Out, string In) Pair(ArcIconMorphName name) => name switch
    {
        ArcIconMorphName.Menu => ("menu", "x", "rotate(45deg) scale(0.78)", "rotate(-45deg) scale(0.78)"),
        ArcIconMorphName.Play => ("play", "pause", "scale(0.6)", "scale(0.6)"),
        ArcIconMorphName.Plus => ("plus", "check", "rotate(45deg) scale(0.82)", "rotate(-45deg) scale(0.82)"),
        ArcIconMorphName.Copy => ("copy", "check", "translate(0px, -3px) scale(0.8)", "translate(0px, 3px) scale(0.8)"),
        _ => ("sun", "moon", "rotate(90deg) scale(0.5)", "rotate(-90deg) scale(0.5)")
    };
}
