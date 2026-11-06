using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

public enum ArcSkeletonShape
{
    Text,
    Rect,
    Circle
}

/// <summary>
/// A pulsing placeholder whose shape matches the content that is loading. Blocks breathe in a
/// staggered wave; reduced motion keeps them still at a readable value.
/// </summary>
[TemplatePart("PART_Placeholder", typeof(StackPanel))]
[TemplatePart("PART_Content", typeof(ContentPresenter))]
public class ArcSkeleton : ContentControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcSkeleton, string>(nameof(Label), "Loading content");

    public static readonly StyledProperty<int> LinesProperty =
        AvaloniaProperty.Register<ArcSkeleton, int>(nameof(Lines), 3);

    public static readonly StyledProperty<bool> AvatarProperty =
        AvaloniaProperty.Register<ArcSkeleton, bool>(nameof(Avatar));

    public static readonly StyledProperty<ArcSkeletonShape> ShapeProperty =
        AvaloniaProperty.Register<ArcSkeleton, ArcSkeletonShape>(nameof(Shape));

    public static readonly StyledProperty<bool> LoadingProperty =
        AvaloniaProperty.Register<ArcSkeleton, bool>(nameof(Loading), true);

    private StackPanel? _placeholder;
    private ContentPresenter? _content;
    private readonly System.Collections.Generic.List<Control> _blocks = new();
    private DispatcherTimer? _pulse;

    static ArcSkeleton()
    {
        LinesProperty.Changed.AddClassHandler<ArcSkeleton>((skeleton, _) => skeleton.Rebuild());
        AvatarProperty.Changed.AddClassHandler<ArcSkeleton>((skeleton, _) => skeleton.Rebuild());
        ShapeProperty.Changed.AddClassHandler<ArcSkeleton>((skeleton, _) => skeleton.Rebuild());
        LoadingProperty.Changed.AddClassHandler<ArcSkeleton>((skeleton, _) => skeleton.UpdateVisibility());
        LabelProperty.Changed.AddClassHandler<ArcSkeleton>((skeleton, _) => skeleton.UpdateAutomation());
        ContentProperty.Changed.AddClassHandler<ArcSkeleton>((skeleton, _) => skeleton.UpdateVisibility());
    }

    public ArcSkeleton()
    {
        Focusable = false;
    }

    /// <summary>Accessible name of the loading status.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Number of text lines, clamped to 1-6.</summary>
    public int Lines
    {
        get => GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    /// <summary>Adds a round avatar placeholder before the lines.</summary>
    public bool Avatar
    {
        get => GetValue(AvatarProperty);
        set => SetValue(AvatarProperty, value);
    }

    /// <summary>Text lines, a single rectangle, or a circle.</summary>
    public ArcSkeletonShape Shape
    {
        get => GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    /// <summary>Keeps the placeholder visible while true; content crossfades in when false.</summary>
    public bool Loading
    {
        get => GetValue(LoadingProperty);
        set => SetValue(LoadingProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _placeholder = e.NameScope.Find<StackPanel>("PART_Placeholder");
        _content = e.NameScope.Find<ContentPresenter>("PART_Content");
        Rebuild();
        UpdateVisibility();
        UpdateAutomation();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        StartPulse();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _pulse?.Stop();
        _pulse = null;
        _blocks.Clear();
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Label);
        AutomationProperties.SetHelpText(this, Loading ? "Loading" : "Loaded");
    }

    private void UpdateVisibility()
    {
        if (_placeholder is not null)
        {
            _placeholder.IsVisible = Loading;
        }

        if (_content is not null)
        {
            _content.IsVisible = !Loading && Content is not null;
        }

        UpdateAutomation();
    }

    private void Rebuild()
    {
        if (_placeholder is null)
        {
            return;
        }

        _placeholder.Children.Clear();
        _blocks.Clear();

        switch (Shape)
        {
            case ArcSkeletonShape.Rect:
                var rectHeight = double.IsNaN(Height) || Height <= 0 ? 96 : Height;
                _placeholder.Children.Add(Block(rectHeight, 12, stretch: true));
                break;
            case ArcSkeletonShape.Circle:
                var diameter = double.IsNaN(Height) || Height <= 0 ? 48 : Height;
                _placeholder.Children.Add(Block(diameter, diameter / 2, stretch: false, width: diameter));
                break;
            default:
                BuildText();
                break;
        }

        StartPulse();
    }

    private void BuildText()
    {
        if (_placeholder is null)
        {
            return;
        }

        var lines = Math.Clamp(Lines, 1, 6);
        var stack = new StackPanel { Spacing = 10 };
        var fractions = new[] { 1.0, 0.92, 0.78, 0.88, 0.7, 0.6 };

        for (var i = 0; i < lines; i++)
        {
            var fraction = fractions[Math.Min(i, fractions.Length - 1)];
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(fraction, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1 - fraction, GridUnitType.Star));
            var block = Block(12, 6, stretch: true);
            grid.Children.Add(block);
            stack.Children.Add(grid);
        }

        if (Avatar)
        {
            var grid = new Grid { ColumnSpacing = 14 };
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.Children.Add(Block(40, 20, stretch: false, width: 40));
            Grid.SetColumn(stack, 1);
            grid.Children.Add(stack);
            _placeholder.Children.Add(grid);
            return;
        }

        _placeholder.Children.Add(stack);
    }

    private Border Block(double height, double radius, bool stretch, double width = double.NaN)
    {
        var border = new Border
        {
            Height = height,
            CornerRadius = new CornerRadius(radius),
            HorizontalAlignment = stretch ? HorizontalAlignment.Stretch : HorizontalAlignment.Left
        };
        if (!double.IsNaN(width))
        {
            border.Width = width;
        }

        border.Paint(Border.BackgroundProperty, ArcTokens.ControlTrack);
        border.Opacity = ArcThemeManager.ReducedMotion ? 0.9 : 0.75;
        _blocks.Add(border);
        return border;
    }

    private void StartPulse()
    {
        _pulse?.Stop();
        _pulse = null;

        if (!Loading || ArcThemeManager.ReducedMotion || _blocks.Count == 0)
        {
            for (var i = 0; i < _blocks.Count; i++)
            {
                _blocks[i].Opacity = 0.9;
            }

            return;
        }

        var started = DateTime.UtcNow;
        _pulse = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Render, (_, _) =>
        {
            var seconds = (DateTime.UtcNow - started).TotalSeconds;
            for (var i = 0; i < _blocks.Count; i++)
            {
                var phase = seconds / 1.8 - i * 0.12;
                var wave = 0.5 + 0.5 * Math.Sin(phase * Math.PI * 2);
                _blocks[i].Opacity = 0.6 + 0.35 * wave;
            }
        });
        _pulse.Start();
    }
}
