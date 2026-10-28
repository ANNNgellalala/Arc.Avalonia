using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Media.Controls;

/// <summary>A page dot; the active one stretches into a short pill track.</summary>
public sealed class ArcCarouselDot : Button
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<ArcCarouselDot, bool>(nameof(IsActive));

    static ArcCarouselDot()
    {
        IsActiveProperty.Changed.AddClassHandler<ArcCarouselDot>((dot, _) => dot.Classes.Set("active", dot.IsActive));
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }
}

/// <summary>
/// Places the slides along one axis around the current fractional index, so a drag can move the row
/// continuously and the neighbours shrink and dim with distance. Inactive slides are not hit-testable.
/// </summary>
public sealed class ArcCarouselPanel : Panel
{
    private double _progress;
    private double _slideWidth = 340;
    private double _spacing = 16;

    /// <summary>Fractional slide index; 1.5 means halfway between slides one and two.</summary>
    public double Progress
    {
        get => _progress;
        set
        {
            if (Math.Abs(value - _progress) < 0.00001)
            {
                return;
            }

            _progress = value;
            InvalidateArrange();
        }
    }

    public double SlideWidth
    {
        get => _slideWidth;
        set
        {
            if (Math.Abs(value - _slideWidth) < 0.5)
            {
                return;
            }

            _slideWidth = value;
            InvalidateMeasure();
        }
    }

    public double SlideSpacing
    {
        get => _spacing;
        set
        {
            if (Math.Abs(value - _spacing) < 0.5)
            {
                return;
            }

            _spacing = value;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = EffectiveWidth(availableSize.Width);
        var height = double.IsFinite(availableSize.Height) && availableSize.Height > 0 ? availableSize.Height : double.PositiveInfinity;
        var tallest = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(width, height));
            tallest = Math.Max(tallest, child.DesiredSize.Height);
        }

        var total = double.IsFinite(availableSize.Width) && availableSize.Width > 0 ? availableSize.Width : width + _spacing * 2;
        return new Size(total, tallest);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = EffectiveWidth(finalSize.Width);
        var step = width + _spacing;
        var center = finalSize.Width / 2;

        for (var index = 0; index < Children.Count; index++)
        {
            var child = Children[index];
            var x = center + (index - _progress) * step - width / 2;
            child.Arrange(new Rect(x, 0, width, finalSize.Height));

            var distance = Math.Min(1, Math.Abs(index - _progress));
            var scale = 1 - 0.08 * distance;
            if (child.RenderTransform is not ScaleTransform transform)
            {
                transform = new ScaleTransform(1, 1);
                child.RenderTransformOrigin = RelativePoint.Center;
                child.RenderTransform = transform;
            }

            transform.ScaleX = scale;
            transform.ScaleY = scale;
            child.Opacity = 1 - 0.45 * distance;
            child.ZIndex = 100 - (int)Math.Round(distance * 10);
        }

        return finalSize;
    }

    private double EffectiveWidth(double available) =>
        double.IsFinite(available) && available > 0
            ? Math.Min(_slideWidth, Math.Max(160, available - _spacing * 2))
            : _slideWidth;
}

/// <summary>
/// Arc's carousel: a centered row of slides that follows a drag, snaps on release, and can be browsed
/// with the arrow keys, the arrows and the dot indicators. An optional interval rotates the row with a
/// visible pause control; rotation pauses on hover and focus and is off entirely under reduced motion.
/// </summary>
public class ArcCarousel : TemplatedControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcCarousel, string>(nameof(Label), "Carousel");

    public static readonly StyledProperty<IList<ArcCarouselItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcCarousel, IList<ArcCarouselItem>?>(nameof(Items));

    public static readonly StyledProperty<int?> IndexProperty =
        AvaloniaProperty.Register<ArcCarousel, int?>(nameof(Index));

    public static readonly StyledProperty<int> DefaultIndexProperty =
        AvaloniaProperty.Register<ArcCarousel, int>(nameof(DefaultIndex));

    public static readonly StyledProperty<double> SlideSizeProperty =
        AvaloniaProperty.Register<ArcCarousel, double>(nameof(SlideSize), 340d);

    public static readonly StyledProperty<double> IntervalProperty =
        AvaloniaProperty.Register<ArcCarousel, double>(nameof(Interval));

    public static readonly StyledProperty<bool> AutoplayProperty =
        AvaloniaProperty.Register<ArcCarousel, bool>(nameof(Autoplay));

    private readonly ArcMediaTween _tween = new();
    private readonly List<ArcCarouselDot> _dots = new();
    private readonly List<Border> _slides = new();
    private ArcCarouselPanel? _stage;
    private StackPanel? _dotHost;
    private Button? _previous;
    private Button? _next;
    private Button? _play;
    private TextBlock? _status;
    private DispatcherTimer? _timer;
    private int _index;
    private double _progress;
    private bool _paused;
    private bool _hovered;
    private bool _pointerDown;
    private bool _dragging;
    private Point _pressPoint;
    private double _pressProgress;

    static ArcCarousel()
    {
        ItemsProperty.Changed.AddClassHandler<ArcCarousel>((carousel, _) => carousel.Rebuild());
        IndexProperty.Changed.AddClassHandler<ArcCarousel>((carousel, _) => carousel.SyncIndex(animate: false));
        DefaultIndexProperty.Changed.AddClassHandler<ArcCarousel>((carousel, _) =>
        {
            if (carousel.Index is null)
            {
                carousel.SyncIndex(animate: false);
            }
        });
        SlideSizeProperty.Changed.AddClassHandler<ArcCarousel>((carousel, _) => carousel.ApplySlideSize());
        IntervalProperty.Changed.AddClassHandler<ArcCarousel>((carousel, _) => carousel.UpdateRotation());
        AutoplayProperty.Changed.AddClassHandler<ArcCarousel>((carousel, _) => carousel.UpdateRotation());
        LabelProperty.Changed.AddClassHandler<ArcCarousel>((carousel, _) => carousel.UpdateAutomation());
    }

    public ArcCarousel()
    {
        Focusable = true;
        IsTabStop = true;
        PointerEntered += (_, _) =>
        {
            _hovered = true;
            UpdateRotation();
        };
        PointerExited += (_, _) =>
        {
            _hovered = false;
            UpdateRotation();
        };
        GotFocus += (_, _) => UpdateRotation();
        LostFocus += (_, _) => UpdateRotation();
    }

    /// <summary>Names the carousel for assistive technology.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>One item per slide.</summary>
    public IList<ArcCarouselItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Controlled active slide; null lets the carousel manage it.</summary>
    public int? Index
    {
        get => GetValue(IndexProperty);
        set => SetValue(IndexProperty, value);
    }

    /// <summary>Initial slide when uncontrolled.</summary>
    public int DefaultIndex
    {
        get => GetValue(DefaultIndexProperty);
        set => SetValue(DefaultIndexProperty, value);
    }

    /// <summary>Width of one slide in px; it shrinks to fit a narrower container.</summary>
    public double SlideSize
    {
        get => GetValue(SlideSizeProperty);
        set => SetValue(SlideSizeProperty, value);
    }

    /// <summary>Milliseconds per slide; 0 turns rotation off.</summary>
    public double Interval
    {
        get => GetValue(IntervalProperty);
        set => SetValue(IntervalProperty, value);
    }

    /// <summary>Starts rotating on attach; a visible play control always accompanies it.</summary>
    public bool Autoplay
    {
        get => GetValue(AutoplayProperty);
        set => SetValue(AutoplayProperty, value);
    }

    /// <summary>Current slide index, zero based.</summary>
    public int CurrentIndex => _index;

    /// <summary>Raised when the active slide changes.</summary>
    public event EventHandler<int>? IndexChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _stage = e.NameScope.Find<ArcCarouselPanel>("PART_Stage");
        _dotHost = e.NameScope.Find<StackPanel>("PART_Dots");
        _previous = e.NameScope.Find<Button>("PART_Prev");
        _next = e.NameScope.Find<Button>("PART_Next");
        _play = e.NameScope.Find<Button>("PART_Play");
        _status = e.NameScope.Find<TextBlock>("Status");

        if (_previous is not null)
        {
            _previous.Content = ArcMediaUi.Icon("chevron-left", 18);
            AutomationProperties.SetName(_previous, "Previous slide");
            _previous.Click += (_, _) => GoTo(_index - 1, animate: true);
        }

        if (_next is not null)
        {
            _next.Content = ArcMediaUi.Icon("chevron-right", 18);
            AutomationProperties.SetName(_next, "Next slide");
            _next.Click += (_, _) => GoTo(_index + 1, animate: true);
        }

        if (_play is not null)
        {
            _play.Click += (_, _) =>
            {
                _paused = !_paused;
                UpdateRotation();
                UpdatePlayIcon();
            };
        }

        ApplySlideSize();
        Rebuild();
        UpdateAutomation();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateRotation();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
        _tween.Stop();
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Label) ? "Carousel" : Label);
        AutomationProperties.SetHelpText(this, "Use the left and right arrow keys to move between slides.");
    }

    private void ApplySlideSize()
    {
        if (_stage is not null)
        {
            _stage.SlideWidth = Math.Max(160, SlideSize);
        }
    }

    private void Rebuild()
    {
        if (_stage is null || _dotHost is null)
        {
            return;
        }

        _stage.Children.Clear();
        _slides.Clear();
        _dotHost.Children.Clear();
        _dots.Clear();

        var items = Items?.ToList() ?? new List<ArcCarouselItem>();
        for (var index = 0; index < items.Count; index++)
        {
            var slide = BuildSlide(items[index], index, items.Count);
            _slides.Add(slide);
            _stage.Children.Add(slide);
        }

        for (var index = 0; index < items.Count; index++)
        {
            var dot = new ArcCarouselDot
            {
                IsTabStop = false,
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };
            var target = index;
            dot.Click += (_, _) => GoTo(target, animate: true);
            AutomationProperties.SetName(dot, $"Slide {index + 1} of {items.Count}");
            _dots.Add(dot);
            _dotHost.Children.Add(dot);
        }

        SyncIndex(animate: false);
        UpdatePlayVisibility();
        UpdateRotation();
    }

    private Border BuildSlide(ArcCarouselItem item, int index, int count)
    {
        var stack = new StackPanel { Spacing = 10 };

        Control media = item.Content as Control ?? new Border
        {
            Height = 80,
            CornerRadius = new CornerRadius(18),
            Background = ArcMediaUi.Brush(ArcTokens.GradientSoft),
            Child = new ArcIcon
            {
                Icon = index % 2 == 0 ? "image" : "sparkles",
                Size = 22,
                Foreground = ArcMediaUi.Brush(ArcTokens.TextMuted),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        stack.Children.Add(media);

        stack.Children.Add(new TextBlock
        {
            Text = item.Title,
            Classes = { "arc-body", "arc-medium" },
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            stack.Children.Add(new TextBlock
            {
                Text = item.Description,
                Classes = { "arc-caption" },
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        var slide = new Border
        {
            Classes = { "carousel-slide" },
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Child = stack
        };
        slide.Paint(Border.BackgroundProperty, ArcTokens.Surface);
        slide.Paint(Border.BorderBrushProperty, ArcTokens.Border);
        slide.Bind(Border.CornerRadiusProperty, slide.GetResourceObservable(ArcTokens.RadiusPanel));
        AutomationProperties.SetName(slide, $"{index + 1} of {count}: {item.Title}");
        return slide;
    }

    private void UpdatePlayVisibility()
    {
        var show = Interval > 0;
        if (_play is not null)
        {
            _play.IsVisible = show;
        }

        UpdatePlayIcon();
    }

    private void UpdatePlayIcon()
    {
        if (_play is null)
        {
            return;
        }

        var playing = IsRotating;
        _play.Content = ArcMediaUi.Icon(playing ? "pause" : "play", 16);
        AutomationProperties.SetName(_play, playing ? "Pause rotation" : "Play rotation");
        ToolTip.SetTip(_play, playing ? "Pause" : "Play");
    }

    private bool IsRotating => Autoplay && Interval > 0 && !_paused && !_hovered && !IsKeyboardFocusWithin && !_dragging;

    private void UpdateRotation()
    {
        var wanted = Interval > 0 && Autoplay && !ArcThemeManager.ReducedMotion && IsRotating && _slides.Count > 1;
        if (!wanted)
        {
            _timer?.Stop();
            _timer = null;
            UpdatePlayIcon();
            return;
        }

        if (_timer is not null)
        {
            return;
        }

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(Math.Max(800, Interval)), DispatcherPriority.Background, (_, _) =>
        {
            if (IsRotating && _slides.Count > 1)
            {
                GoTo((_index + 1) % _slides.Count, animate: true);
            }
        });
        _timer.Start();
        UpdatePlayIcon();
    }

    /// <summary>Moves to a slide by index, wrapping at both ends.</summary>
    public void GoTo(int index, bool animate)
    {
        if (_slides.Count == 0)
        {
            return;
        }

        var count = _slides.Count;
        var target = ((index % count) + count) % count;
        var changed = target != _index;

        // A wrap would have to travel across the whole row, so those jumps are instant instead of a
        // long scroll past every slide.
        var travel = Math.Abs(target - _index);
        var crossFade = travel > count / 2;

        if (animate && !crossFade)
        {
            _tween.Run(_progress, target, ArcMediaUi.Duration(ArcTokens.DurationConsidered), value =>
            {
                _progress = value;
                if (_stage is not null)
                {
                    _stage.Progress = value;
                }
            }, () =>
            {
                _progress = target;
                if (_stage is not null)
                {
                    _stage.Progress = target;
                }
            }, Arc.Avalonia.Motion.ArcMotion.SpringSmooth.Ease);
        }
        else
        {
            _tween.Stop();
            _progress = target;
            if (_stage is not null)
            {
                _stage.Progress = target;
            }
        }

        _index = target;
        SyncIndexVisuals();
        Announce();
        if (changed)
        {
            IndexChanged?.Invoke(this, _index);
        }
    }

    private void SyncIndex(bool animate)
    {
        if (_slides.Count == 0)
        {
            return;
        }

        var target = Math.Clamp(Index ?? DefaultIndex, 0, _slides.Count - 1);
        GoTo(target, animate && Math.Abs(target - _index) > 0);
    }

    private void SyncIndexVisuals()
    {
        for (var index = 0; index < _dots.Count; index++)
        {
            var active = index == _index;
            _dots[index].Classes.Set("active", active);
            _dots[index].IsTabStop = active;
        }

        for (var index = 0; index < _slides.Count; index++)
        {
            _slides[index].IsHitTestVisible = index == _index;
        }

        AutomationProperties.SetItemStatus(this, _dots.Count == 0
            ? null
            : $"Slide {_index + 1} of {_dots.Count}");
    }

    private void Announce()
    {
        if (_status is null)
        {
            return;
        }

        var title = Items is not null && _index >= 0 && _index < Items.Count ? Items[_index].Title : string.Empty;
        _status.Text = string.IsNullOrWhiteSpace(title)
            ? $"Slide {_index + 1} of {_slides.Count}"
            : $"Slide {_index + 1} of {_slides.Count}: {title}";
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || ArcMediaUi.InsideButton(e.Source))
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        _tween.Stop();
        _pointerDown = true;
        _dragging = false;
        _pressPoint = e.GetPosition(this);
        _pressProgress = _progress;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pointerDown || e.Pointer.Captured != this || _stage is null || _slides.Count == 0)
        {
            return;
        }

        var dx = e.GetPosition(this).X - _pressPoint.X;
        if (!_dragging && Math.Abs(dx) < 4)
        {
            return;
        }

        _dragging = true;
        UpdateRotation();
        var step = Math.Max(1, _stage.SlideWidth + _stage.SlideSpacing);
        var raw = _pressProgress - dx / step;
        var max = _slides.Count - 1;
        var clamped = raw < 0 ? raw * 0.28 : raw > max ? max + (raw - max) * 0.28 : raw;
        _progress = clamped;
        _stage.Progress = clamped;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pointerDown)
        {
            return;
        }

        _pointerDown = false;
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        if (!_dragging || _slides.Count == 0)
        {
            _dragging = false;
            return;
        }

        _dragging = false;
        var moved = _progress - _pressProgress;
        var target = Math.Abs(moved) > 0.16
            ? _index + Math.Sign(moved)
            : (int)Math.Round(_progress);
        GoTo(target, animate: true);
        UpdateRotation();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Left:
                GoTo(_index - 1, animate: true);
                e.Handled = true;
                break;
            case Key.Right:
                GoTo(_index + 1, animate: true);
                e.Handled = true;
                break;
            case Key.Home:
                GoTo(0, animate: true);
                e.Handled = true;
                break;
            case Key.End:
                GoTo(_slides.Count - 1, animate: true);
                e.Handled = true;
                break;
        }
    }
}



