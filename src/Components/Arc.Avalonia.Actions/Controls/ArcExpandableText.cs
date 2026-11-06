using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Controls.Metadata;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Controls;

/// <summary>
/// Long text clamped to a few lines with a soft bottom fade. Read more opens it to its full height and
/// Show less closes it; the toggle only appears when the text really overflows.
/// </summary>
[TemplatePart("PART_Body", typeof(TextBlock))]
[TemplatePart("PART_Clip", typeof(Border))]
[TemplatePart("PART_Fade", typeof(Border))]
[TemplatePart("PART_Toggle", typeof(ArcButton))]
public sealed class ArcExpandableText : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ArcExpandableText, string?>(nameof(Text));

    public static readonly StyledProperty<int> LinesProperty =
        AvaloniaProperty.Register<ArcExpandableText, int>(nameof(Lines), 3);

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ArcExpandableText, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<string?> MoreLabelProperty =
        AvaloniaProperty.Register<ArcExpandableText, string?>(nameof(MoreLabel), "Read more");

    public static readonly StyledProperty<string?> LessLabelProperty =
        AvaloniaProperty.Register<ArcExpandableText, string?>(nameof(LessLabel), "Show less");

    private TextBlock? _body;
    private Border? _clip;
    private Border? _fade;
    private ArcButton? _toggle;
    private bool _overflows;
    private double _lastWidth;
    private double _fullHeight;
    private double _clampedHeight;

    static ArcExpandableText()
    {
        TextProperty.Changed.AddClassHandler<ArcExpandableText>((text, _) => text.Refresh(remeasure: true));
        LinesProperty.Changed.AddClassHandler<ArcExpandableText>((text, _) => text.Refresh(remeasure: true));
        IsExpandedProperty.Changed.AddClassHandler<ArcExpandableText>((text, _) => text.Refresh(remeasure: false));
        MoreLabelProperty.Changed.AddClassHandler<ArcExpandableText>((text, _) => text.UpdateToggle());
        LessLabelProperty.Changed.AddClassHandler<ArcExpandableText>((text, _) => text.UpdateToggle());
    }

    public ArcExpandableText()
    {
        LayoutUpdated += (_, _) =>
        {
            if (Math.Abs(Bounds.Width - _lastWidth) > 0.5)
            {
                Refresh(remeasure: true);
            }
        };
    }

    public event EventHandler<bool>? ExpandedChanged;
    /// <summary>Bubbled copy of <see cref="ExpandedChanged"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcExpandableTextExpandedChangedEventArgs> ExpandedChangedEvent =
        RoutedEvent.Register<ArcExpandableText, ArcExpandableTextExpandedChangedEventArgs>("ExpandedChanged", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="ExpandedChanged"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcExpandableTextExpandedChangedEventArgs>? ExpandedChangedRouted
    {
        add => AddHandler(ExpandedChangedEvent, value);
        remove => RemoveHandler(ExpandedChangedEvent, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Lines shown while collapsed, measured in the text's own line height.</summary>
    public int Lines
    {
        get => GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public string? MoreLabel
    {
        get => GetValue(MoreLabelProperty);
        set => SetValue(MoreLabelProperty, value);
    }

    public string? LessLabel
    {
        get => GetValue(LessLabelProperty);
        set => SetValue(LessLabelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _body = e.NameScope.Find<TextBlock>("PART_Body");
        _clip = e.NameScope.Find<Border>("PART_Clip");
        _fade = e.NameScope.Find<Border>("PART_Fade");
        _toggle = e.NameScope.Find<ArcButton>("PART_Toggle");

        if (_clip is not null && !ArcThemeManager.ReducedMotion)
        {
            _clip.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = HeightProperty,
                    Duration = ArcMotion.SpringSmoothDuration,
                    Easing = ArcMotion.SpringSmooth
                }
            };
        }

        if (_toggle is not null)
        {
            _toggle.Click += (_, _) =>
            {
                SetCurrentValue(IsExpandedProperty, !IsExpanded);
                        var expandedChangedArgs = new ArcExpandableTextExpandedChangedEventArgs(ExpandedChangedEvent) { IsExpanded = IsExpanded };
        ExpandedChanged?.Invoke(this, IsExpanded);
        RaiseEvent(expandedChangedArgs);
            };
        }

        if (_fade is not null)
        {
            if (!ArcThemeManager.ReducedMotion)
            {
                _fade.Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = ArcMotion.Standard,
                        Easing = ArcMotion.EaseStandard
                    }
                };
            }

            UpdateFadeBrush();
        }

        Refresh(remeasure: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BackgroundProperty)
        {
            UpdateFadeBrush();
        }
    }

    private void Refresh(bool remeasure)
    {
        if (_body is null || _clip is null)
        {
            return;
        }

        _body.Text = Text;
        _body.MaxLines = IsExpanded ? 0 : Math.Max(1, Lines);

        if (remeasure)
        {
            MeasureHeights();
        }

        if (IsExpanded)
        {
            _clip.Height = _fullHeight > 0 ? _fullHeight : double.NaN;
            _clip.ClipToBounds = false;
        }
        else
        {
            _clip.Height = _clampedHeight > 0 ? _clampedHeight : double.NaN;
            _clip.ClipToBounds = true;
        }

        UpdateToggle();
        UpdateFade();
    }

    private void MeasureHeights()
    {
        if (_body is null)
        {
            return;
        }

        var width = Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        _lastWidth = width;
        var lines = Math.Max(1, Lines);

        _body.MaxLines = 0;
        _body.Measure(new Size(width, double.PositiveInfinity));
        _fullHeight = _body.DesiredSize.Height;

        _body.MaxLines = lines;
        _body.Measure(new Size(width, double.PositiveInfinity));
        var clamped = _body.DesiredSize.Height;

        _clampedHeight = clamped;
        _overflows = _fullHeight > clamped + 1;

        _body.MaxLines = IsExpanded ? 0 : lines;
    }

    private void UpdateToggle()
    {
        if (_toggle is null)
        {
            return;
        }

        _toggle.IsVisible = _overflows || IsExpanded;
        _toggle.Content = new TextBlock
        {
            Text = IsExpanded ? LessLabel ?? "Show less" : MoreLabel ?? "Read more",
            Classes = { "arc-label" }
        };
        AutomationProperties.SetName(_toggle, IsExpanded ? LessLabel ?? "Show less" : MoreLabel ?? "Read more");
        AutomationProperties.SetHelpText(_toggle, IsExpanded ? "Collapses the text" : "Expands the text");
        _toggle.Classes.Set("expanded", IsExpanded);
    }

    private void UpdateFade()
    {
        if (_fade is null)
        {
            return;
        }

        _fade.IsVisible = _overflows && !IsExpanded;
        _fade.Opacity = _overflows && !IsExpanded ? 1 : 0;
    }

    private void UpdateFadeBrush()
    {
        if (_fade is null)
        {
            return;
        }

        var solid = Background as ISolidColorBrush;
        var color = solid?.Color ?? ArcResources.Color(ArcTokens.Background);
        _fade.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(color, 0),
                new GradientStop(ArcColor.WithAlpha(color, 0), 1)
            }
        };
    }
}