using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Media.Controls;

/// <summary>
/// The thin scrollbar overlay. It draws only the two tracks and thumbs Arc uses (a quiet pill on a
/// barely-there track), and exposes the thumb rectangles so the scroll area can drag them. Hit testing
/// is handled by the owner, which keeps the overlay out of the pointer path of the content.
/// </summary>
public sealed class ArcScrollOverlay : Control
{
    public static readonly StyledProperty<Vector> OffsetProperty =
        AvaloniaProperty.Register<ArcScrollOverlay, Vector>(nameof(Offset));

    public static readonly StyledProperty<Size> ExtentProperty =
        AvaloniaProperty.Register<ArcScrollOverlay, Size>(nameof(Extent));

    public static readonly StyledProperty<Size> ViewportSizeProperty =
        AvaloniaProperty.Register<ArcScrollOverlay, Size>(nameof(ViewportSize));

    public static readonly StyledProperty<bool> ShowVerticalProperty =
        AvaloniaProperty.Register<ArcScrollOverlay, bool>(nameof(ShowVertical));

    public static readonly StyledProperty<bool> ShowHorizontalProperty =
        AvaloniaProperty.Register<ArcScrollOverlay, bool>(nameof(ShowHorizontal));

    public static readonly StyledProperty<bool> HoveredProperty =
        AvaloniaProperty.Register<ArcScrollOverlay, bool>(nameof(Hovered));

    public static readonly StyledProperty<double> TrackWidthProperty =
        AvaloniaProperty.Register<ArcScrollOverlay, double>(nameof(TrackWidth), 8d);

    public static readonly StyledProperty<double> TrackInsetProperty =
        AvaloniaProperty.Register<ArcScrollOverlay, double>(nameof(TrackInset), 4d);

    static ArcScrollOverlay()
    {
        AffectsRender<ArcScrollOverlay>(
            OffsetProperty, ExtentProperty, ViewportSizeProperty, ShowVerticalProperty,
            ShowHorizontalProperty, HoveredProperty, TrackWidthProperty, TrackInsetProperty);
    }

    public ArcScrollOverlay()
    {
        IsHitTestVisible = false;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public Vector Offset
    {
        get => GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    public Size Extent
    {
        get => GetValue(ExtentProperty);
        set => SetValue(ExtentProperty, value);
    }

    public Size ViewportSize
    {
        get => GetValue(ViewportSizeProperty);
        set => SetValue(ViewportSizeProperty, value);
    }

    public bool ShowVertical
    {
        get => GetValue(ShowVerticalProperty);
        set => SetValue(ShowVerticalProperty, value);
    }

    public bool ShowHorizontal
    {
        get => GetValue(ShowHorizontalProperty);
        set => SetValue(ShowHorizontalProperty, value);
    }

    public bool Hovered
    {
        get => GetValue(HoveredProperty);
        set => SetValue(HoveredProperty, value);
    }

    public double TrackWidth
    {
        get => GetValue(TrackWidthProperty);
        set => SetValue(TrackWidthProperty, value);
    }

    public double TrackInset
    {
        get => GetValue(TrackInsetProperty);
        set => SetValue(TrackInsetProperty, value);
    }

    public Rect VerticalTrackRect() => new(
        Math.Max(0, Bounds.Width - TrackInset - TrackWidth),
        TrackInset,
        TrackWidth,
        Math.Max(0, Bounds.Height - TrackInset * 2));

    public Rect HorizontalTrackRect() => new(
        TrackInset,
        Math.Max(0, Bounds.Height - TrackInset - TrackWidth),
        Math.Max(0, Bounds.Width - TrackInset * 2),
        TrackWidth);

    public Rect VerticalThumbRect() => Thumb(VerticalTrackRect(), ViewportSize.Height, Extent.Height, Offset.Y);

    public Rect HorizontalThumbRect() => Thumb(HorizontalTrackRect(), ViewportSize.Width, Extent.Width, Offset.X);

    private static Rect Thumb(Rect track, double viewport, double extent, double offset)
    {
        if (extent <= 0 || track.Height <= 0)
        {
            return default;
        }

        var vertical = track.Height >= track.Width;
        var length = vertical ? track.Height : track.Width;
        var fraction = Math.Clamp(viewport / extent, 0, 1);
        var thumbLength = Math.Max(24, length * fraction);
        thumbLength = Math.Min(thumbLength, length);
        var maxOffset = Math.Max(0.001, extent - viewport);
        var position = Math.Clamp(offset / maxOffset, 0, 1) * (length - thumbLength);
        return vertical
            ? new Rect(track.X, track.Y + position, track.Width, thumbLength)
            : new Rect(track.X + position, track.Y, thumbLength, track.Height);
    }

    public bool HitsVerticalThumb(Point point) =>
        ShowVertical && VerticalThumbRect().Inflate(5).Contains(point);

    public bool HitsHorizontalThumb(Point point) =>
        ShowHorizontal && HorizontalThumbRect().Inflate(5).Contains(point);

    public bool HitsVerticalTrack(Point point) => ShowVertical && VerticalTrackRect().Contains(point);

    public bool HitsHorizontalTrack(Point point) => ShowHorizontal && HorizontalTrackRect().Contains(point);

    public override void Render(DrawingContext context)
    {
        if (!ShowVertical && !ShowHorizontal)
        {
            return;
        }

        var trackBrush = new SolidColorBrush(ArcColor.WithAlpha(ArcMediaUi.Color(ArcTokens.Border), Hovered ? 0.9 : 0.45));
        var thumbBrush = new SolidColorBrush(ArcColor.WithAlpha(
            ArcMediaUi.Color(ArcTokens.TextMuted),
            Hovered ? 0.85 : 0.55));
        var radius = TrackWidth / 2;

        if (ShowVertical)
        {
            var track = VerticalTrackRect();
            var thumb = VerticalThumbRect();
            if (Hovered)
            {
                context.DrawRectangle(trackBrush, null, track, radius, radius);
            }

            context.DrawRectangle(thumbBrush, null, thumb, radius, radius);
        }

        if (ShowHorizontal)
        {
            var track = HorizontalTrackRect();
            var thumb = HorizontalThumbRect();
            if (Hovered)
            {
                context.DrawRectangle(trackBrush, null, track, radius, radius);
            }

            context.DrawRectangle(thumbBrush, null, thumb, radius, radius);
        }
    }
}

/// <summary>
/// Arc's scroll area: native scrolling with the platform chrome hidden, a thin overlay scrollbar that
/// fades in while scrolling or hovering, and edge fades that appear only when content overflows. Keyboard
/// scrolling stays native to the viewport, and vertical wheel movement drives horizontal strips.
/// </summary>
[TemplatePart("PART_Viewport", typeof(ScrollViewer))]
[TemplatePart("PART_FadeX", typeof(Border))]
[TemplatePart("PART_Overlay", typeof(ArcScrollOverlay))]
public class ArcScrollArea : ContentControl
{
    public static readonly StyledProperty<ArcScrollOrientation> OrientationProperty =
        AvaloniaProperty.Register<ArcScrollArea, ArcScrollOrientation>(nameof(Orientation));

    public static readonly StyledProperty<double> FadeProperty =
        AvaloniaProperty.Register<ArcScrollArea, double>(nameof(Fade), 28d);

    public static readonly StyledProperty<ArcScrollbarsMode> ScrollbarsProperty =
        AvaloniaProperty.Register<ArcScrollArea, ArcScrollbarsMode>(nameof(Scrollbars));

    public static readonly StyledProperty<double> HideDelayProperty =
        AvaloniaProperty.Register<ArcScrollArea, double>(nameof(HideDelay), 900d);

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcScrollArea, string?>(nameof(Label));

    public static readonly StyledProperty<bool> WheelToHorizontalProperty =
        AvaloniaProperty.Register<ArcScrollArea, bool>(nameof(WheelToHorizontal), true);

    private ScrollViewer? _viewport;
    private Border? _fadeX;
    private ArcScrollOverlay? _overlay;
    private DispatcherTimer? _hideTimer;
    private bool _draggingVertical;
    private bool _draggingHorizontal;
    private double _dragStartOffset;
    private double _dragStartPointer;
    private bool _hovered;
    private ArcScrollAreaEdges _edges;

    static ArcScrollArea()
    {
        OrientationProperty.Changed.AddClassHandler<ArcScrollArea>((area, _) => area.UpdateAxes());
        FadeProperty.Changed.AddClassHandler<ArcScrollArea>((area, _) => area.UpdateEdges());
        ScrollbarsProperty.Changed.AddClassHandler<ArcScrollArea>((area, _) => area.UpdateThumbVisibility());
        MaxHeightProperty.Changed.AddClassHandler<ArcScrollArea>((area, _) => area.UpdateAxes());
        LabelProperty.Changed.AddClassHandler<ArcScrollArea>((area, _) => area.UpdateAutomation());
    }

    public ArcScrollArea()
    {
        AddHandler(InputElement.PointerWheelChangedEvent, new EventHandler<PointerWheelEventArgs>(OnWheelTunnel), RoutingStrategies.Tunnel);
    }

    /// <summary>Axes that scroll.</summary>
    public ArcScrollOrientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Length of the edge fade in px; 0 turns fades off.</summary>
    public double Fade
    {
        get => GetValue(FadeProperty);
        set => SetValue(FadeProperty, value);
    }

    /// <summary>Auto shows the bars while scrolling or hovering; Always keeps them when content overflows.</summary>
    public ArcScrollbarsMode Scrollbars
    {
        get => GetValue(ScrollbarsProperty);
        set => SetValue(ScrollbarsProperty, value);
    }

    /// <summary>Milliseconds the bars linger after scrolling stops.</summary>
    public double HideDelay
    {
        get => GetValue(HideDelayProperty);
        set => SetValue(HideDelayProperty, value);
    }

    /// <summary>Accessible name for the scrolling region.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Turns vertical wheel movement into horizontal scrolling for horizontal areas.</summary>
    public bool WheelToHorizontal
    {
        get => GetValue(WheelToHorizontalProperty);
        set => SetValue(WheelToHorizontalProperty, value);
    }

    /// <summary>True when content extends past an edge.</summary>
    public ArcScrollAreaEdges Edges => _edges;

    /// <summary>Raised when content starts or stops extending past an edge.</summary>
    public event EventHandler<ArcScrollAreaEdges>? EdgeChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _viewport = e.NameScope.Find<ScrollViewer>("PART_Viewport");
        _fadeX = e.NameScope.Find<Border>("PART_FadeX");
        _overlay = e.NameScope.Find<ArcScrollOverlay>("PART_Overlay");

        if (_viewport is not null)
        {
            _viewport.ScrollChanged += (_, _) =>
            {
                UpdateEdges();
                ShowThumbs();
            };
            _viewport.SizeChanged += (_, _) => UpdateEdges();
            _viewport.KeyDown += OnViewportKeyDown;
            _viewport.GotFocus += (_, args) =>
            {
                if (args.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
                {
                    Classes.Set("keyboard-focus", true);
                }
            };
            _viewport.LostFocus += (_, _) => Classes.Set("keyboard-focus", false);
        }

        if (_overlay is not null)
        {
            if (!ArcThemeManager.ReducedMotion)
            {
                _overlay.Transitions = new Transitions
                {
                    new DoubleTransition { Property = OpacityProperty, Duration = ArcMotion.Fast }
                };
            }

            _overlay.Opacity = Scrollbars == ArcScrollbarsMode.Always ? 1 : 0;
        }

        UpdateAxes();
        UpdateAutomation();
        UpdateEdges();
        UpdateThumbVisibility();
    }

    private void UpdateAutomation()
    {
        var name = string.IsNullOrWhiteSpace(Label) ? "Scrollable content" : Label;
        AutomationProperties.SetName(this, name);
        if (_viewport is not null)
        {
            AutomationProperties.SetName(_viewport, name);
            AutomationProperties.SetHelpText(_viewport, "Scroll with the wheel, the arrow keys, Page Up and Page Down.");
        }
    }

    private void UpdateAxes()
    {
        if (_viewport is null)
        {
            return;
        }

        _viewport.VerticalScrollBarVisibility = Orientation == ArcScrollOrientation.Horizontal
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Hidden;
        _viewport.HorizontalScrollBarVisibility = Orientation == ArcScrollOrientation.Vertical
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Hidden;
        _viewport.MaxHeight = MaxHeight > 0 ? MaxHeight : double.PositiveInfinity;

        UpdateEdges();
        UpdateThumbVisibility();
    }

    private double MaxHorizontal => Math.Max(0, (_viewport?.Extent.Width ?? 0) - (_viewport?.Viewport.Width ?? 0));

    private double MaxVertical => Math.Max(0, (_viewport?.Extent.Height ?? 0) - (_viewport?.Viewport.Height ?? 0));

    private void UpdateEdges()
    {
        if (_viewport is null)
        {
            return;
        }

        var offset = _viewport.Offset;
        var maxX = MaxHorizontal;
        var maxY = MaxVertical;
        var top = Orientation != ArcScrollOrientation.Horizontal && offset.Y > 0.5;
        var bottom = Orientation != ArcScrollOrientation.Horizontal && offset.Y < maxY - 0.5;
        var left = Orientation != ArcScrollOrientation.Vertical && offset.X > 0.5;
        var right = Orientation != ArcScrollOrientation.Vertical && offset.X < maxX - 0.5;
        var edges = new ArcScrollAreaEdges(top, bottom, left, right);

        UpdateMasks(top, bottom, left, right);
        SyncOverlay();

        if (edges != _edges)
        {
            _edges = edges;
            EdgeChanged?.Invoke(this, edges);
        }
    }

    /// <summary>
    /// Fades are opacity masks so the wash matches whatever surface the area sits on: the scroll viewer
    /// carries the vertical fade, its wrapper border carries the horizontal one, and the two multiply.
    /// </summary>
    private void UpdateMasks(bool top, bool bottom, bool left, bool right)
    {
        if (_viewport is not null)
        {
            _viewport.OpacityMask = Fade <= 0 ? null : VerticalMask(top, bottom, _viewport.Bounds.Height);
        }

        if (_fadeX is not null)
        {
            _fadeX.OpacityMask = Fade <= 0 ? null : HorizontalMask(left, right, _fadeX.Bounds.Width);
        }
    }

    private IBrush? VerticalMask(bool start, bool end, double length)
    {
        if ((!start && !end) || length <= 1)
        {
            return null;
        }

        var stops = new GradientStops();
        AddFadeStops(stops, start, end, length);
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(0, length, RelativeUnit.Absolute),
            GradientStops = stops
        };
    }

    private IBrush? HorizontalMask(bool start, bool end, double length)
    {
        if ((!start && !end) || length <= 1)
        {
            return null;
        }

        var stops = new GradientStops();
        AddFadeStops(stops, start, end, length);
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(length, 0, RelativeUnit.Absolute),
            GradientStops = stops
        };
    }

    private void AddFadeStops(GradientStops stops, bool start, bool end, double length)
    {
        var ratio = Math.Clamp(Fade / Math.Max(1, length), 0, 0.5);
        var transparent = Color.FromArgb(0, 255, 255, 255);
        var opaque = Color.FromArgb(255, 255, 255, 255);

        if (start)
        {
            stops.Add(new GradientStop(transparent, 0));
            stops.Add(new GradientStop(opaque, ratio));
        }
        else
        {
            stops.Add(new GradientStop(opaque, 0));
        }

        if (end)
        {
            stops.Add(new GradientStop(opaque, 1 - ratio));
            stops.Add(new GradientStop(transparent, 1));
        }
        else
        {
            stops.Add(new GradientStop(opaque, 1));
        }
    }

    private void UpdateThumbVisibility()
    {
        if (_overlay is null || _viewport is null)
        {
            return;
        }

        var allowsVertical = Orientation != ArcScrollOrientation.Horizontal;
        var allowsHorizontal = Orientation != ArcScrollOrientation.Vertical;
        var overflowY = allowsVertical && MaxVertical > 0.5;
        var overflowX = allowsHorizontal && MaxHorizontal > 0.5;

        SyncOverlay();
        _overlay.Hovered = _hovered;

        if (Scrollbars == ArcScrollbarsMode.Always && (overflowY || overflowX))
        {
            _overlay.Opacity = 1;
            return;
        }

        if (Scrollbars == ArcScrollbarsMode.Always)
        {
            _overlay.Opacity = 0;
        }
    }

    /// <summary>Pushes the viewport's geometry into the drawing overlay.</summary>
    private void SyncOverlay()
    {
        if (_overlay is null || _viewport is null)
        {
            return;
        }

        _overlay.ShowVertical = Orientation != ArcScrollOrientation.Horizontal && MaxVertical > 0.5;
        _overlay.ShowHorizontal = Orientation != ArcScrollOrientation.Vertical && MaxHorizontal > 0.5;
        _overlay.TrackWidth = 8;
        _overlay.TrackInset = 4;
        _overlay.Offset = _viewport.Offset;
        _overlay.Extent = _viewport.Extent;
        _overlay.ViewportSize = _viewport.Viewport;
    }

    private void ShowThumbs()
    {
        if (_overlay is null)
        {
            return;
        }

        _overlay.Hovered = _hovered;
        if (Scrollbars == ArcScrollbarsMode.Auto)
        {
            _overlay.Opacity = 1;
            HideThumbsSoon();
        }
        else
        {
            UpdateThumbVisibility();
        }
    }

    private void HideThumbsSoon()
    {
        if (_overlay is null || Scrollbars == ArcScrollbarsMode.Always || _draggingVertical || _draggingHorizontal)
        {
            UpdateThumbVisibility();
            return;
        }

        _hideTimer?.Stop();
        _hideTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(Math.Max(100, HideDelay)), DispatcherPriority.Background, (_, _) =>
        {
            _hideTimer?.Stop();
            _hideTimer = null;
            if (_overlay is not null && !_hovered && !_draggingVertical && !_draggingHorizontal)
            {
                _overlay.Opacity = 0;
            }
        });
        _hideTimer.Start();
    }

    private void OnWheelTunnel(object? sender, PointerWheelEventArgs e)
    {
        if (_viewport is null || !WheelToHorizontal)
        {
            return;
        }

        if (Orientation == ArcScrollOrientation.Vertical)
        {
            return;
        }

        var delta = e.Delta;
        if (Math.Abs(delta.Y) <= Math.Abs(delta.X))
        {
            return;
        }

        if (Orientation == ArcScrollOrientation.Both && MaxVertical > 0.5)
        {
            return;
        }

        var max = MaxHorizontal;
        if (max <= 0.5)
        {
            return;
        }

        var offset = _viewport.Offset;
        var next = Math.Clamp(offset.X - delta.Y * 56, 0, max);
        if (Math.Abs(next - offset.X) < 0.5)
        {
            return;
        }

        _viewport.Offset = new Vector(next, offset.Y);
        e.Handled = true;
    }

    private void OnViewportKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewport is null)
        {
            return;
        }

        var offset = _viewport.Offset;
        var horizontal = Orientation != ArcScrollOrientation.Vertical;
        var vertical = Orientation != ArcScrollOrientation.Horizontal;
        const double step = 48;
        var pageX = Math.Max(step, _viewport.Viewport.Width * 0.9);
        var pageY = Math.Max(step, _viewport.Viewport.Height * 0.9);
        var next = offset;

        switch (e.Key)
        {
            case Key.Left when horizontal:
                next = new Vector(Math.Max(0, offset.X - step), offset.Y);
                break;
            case Key.Right when horizontal:
                next = new Vector(Math.Min(MaxHorizontal, offset.X + step), offset.Y);
                break;
            case Key.Up when vertical:
                next = new Vector(offset.X, Math.Max(0, offset.Y - step));
                break;
            case Key.Down when vertical:
                next = new Vector(offset.X, Math.Min(MaxVertical, offset.Y + step));
                break;
            case Key.PageUp when vertical:
                next = new Vector(offset.X, Math.Max(0, offset.Y - pageY));
                break;
            case Key.PageDown when vertical:
                next = new Vector(offset.X, Math.Min(MaxVertical, offset.Y + pageY));
                break;
            case Key.Space when vertical:
                next = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
                    ? new Vector(offset.X, Math.Max(0, offset.Y - pageY))
                    : new Vector(offset.X, Math.Min(MaxVertical, offset.Y + pageY));
                break;
            case Key.Home:
                next = new Vector(0, 0);
                break;
            case Key.End:
                next = new Vector(MaxHorizontal, MaxVertical);
                break;
            default:
                return;
        }

        _viewport.Offset = next;
        ShowThumbs();
        e.Handled = true;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hovered = true;
        ShowThumbs();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        HideThumbsSoon();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_viewport is null || _overlay is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var point = e.GetPosition(_overlay);
        if (_overlay.HitsVerticalThumb(point))
        {
            _draggingVertical = true;
            _dragStartOffset = _viewport.Offset.Y;
            _dragStartPointer = point.Y;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (_overlay.HitsHorizontalThumb(point))
        {
            _draggingHorizontal = true;
            _dragStartOffset = _viewport.Offset.X;
            _dragStartPointer = point.X;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (_overlay.HitsVerticalTrack(point))
        {
            _viewport.Offset = new Vector(_viewport.Offset.X,
                Math.Clamp(_viewport.Offset.Y + (point.Y < _overlay.VerticalThumbRect().Y ? -1 : 1) * _viewport.Viewport.Height * 0.9, 0, MaxVertical));
            ShowThumbs();
            e.Handled = true;
            return;
        }

        if (_overlay.HitsHorizontalTrack(point))
        {
            _viewport.Offset = new Vector(
                Math.Clamp(_viewport.Offset.X + (point.X < _overlay.HorizontalThumbRect().X ? -1 : 1) * _viewport.Viewport.Width * 0.9, 0, MaxHorizontal),
                _viewport.Offset.Y);
            ShowThumbs();
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_viewport is null || _overlay is null || e.Pointer.Captured != this)
        {
            return;
        }

        var point = e.GetPosition(_overlay);
        if (_draggingVertical)
        {
            var thumb = _overlay.VerticalThumbRect();
            var track = _overlay.VerticalTrackRect();
            var travel = Math.Max(1, track.Height - thumb.Height);
            var delta = point.Y - _dragStartPointer;
            var next = Math.Clamp(_dragStartOffset + delta / travel * MaxVertical, 0, MaxVertical);
            _viewport.Offset = new Vector(_viewport.Offset.X, next);
            e.Handled = true;
        }
        else if (_draggingHorizontal)
        {
            var thumb = _overlay.HorizontalThumbRect();
            var track = _overlay.HorizontalTrackRect();
            var travel = Math.Max(1, track.Width - thumb.Width);
            var delta = point.X - _dragStartPointer;
            var next = Math.Clamp(_dragStartOffset + delta / travel * MaxHorizontal, 0, MaxHorizontal);
            _viewport.Offset = new Vector(next, _viewport.Offset.Y);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_draggingVertical && !_draggingHorizontal)
        {
            return;
        }

        _draggingVertical = false;
        _draggingHorizontal = false;
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        HideThumbsSoon();
        e.Handled = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _hideTimer?.Stop();
        _hideTimer = null;
    }
}






