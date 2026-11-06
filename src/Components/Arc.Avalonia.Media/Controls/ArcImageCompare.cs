using System;
using System.IO;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Media.Controls;

/// <summary>
/// The drawn comparison frame: two media layers stacked, the top one revealed by a clip at the divider,
/// a draggable handle, and caption chips that fade as the divider reaches them. Missing image paths fall
/// back to a mesh and gradient placeholder so the component never depends on the network.
/// </summary>
public sealed class ArcCompareSurface : Control
{
    public static readonly StyledProperty<Bitmap?> BeforeProperty =
        AvaloniaProperty.Register<ArcCompareSurface, Bitmap?>(nameof(Before));

    public static readonly StyledProperty<Bitmap?> AfterProperty =
        AvaloniaProperty.Register<ArcCompareSurface, Bitmap?>(nameof(After));

    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<ArcCompareSurface, double>(nameof(Fraction), 0.5);

    public static readonly StyledProperty<ArcImageCompareOrientation> OrientationProperty =
        AvaloniaProperty.Register<ArcCompareSurface, ArcImageCompareOrientation>(nameof(Orientation));

    public static readonly StyledProperty<string?> BeforeLabelProperty =
        AvaloniaProperty.Register<ArcCompareSurface, string?>(nameof(BeforeLabel), "Before");

    public static readonly StyledProperty<string?> AfterLabelProperty =
        AvaloniaProperty.Register<ArcCompareSurface, string?>(nameof(AfterLabel), "After");

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<ArcCompareSurface, bool>(nameof(ShowLabels), true);

    public static readonly StyledProperty<bool> DraggingProperty =
        AvaloniaProperty.Register<ArcCompareSurface, bool>(nameof(Dragging));

    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<ArcCompareSurface, double>(nameof(AspectRatio), 1.5);

    public static readonly StyledProperty<CornerRadius> FrameRadiusProperty =
        AvaloniaProperty.Register<ArcCompareSurface, CornerRadius>(nameof(FrameRadius), new CornerRadius(25));

    static ArcCompareSurface()
    {
        AffectsRender<ArcCompareSurface>(
            BeforeProperty, AfterProperty, FractionProperty, OrientationProperty,
            BeforeLabelProperty, AfterLabelProperty, ShowLabelsProperty, DraggingProperty, FrameRadiusProperty);
        AffectsMeasure<ArcCompareSurface>(AspectRatioProperty);
    }

    public ArcCompareSurface()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public Bitmap? Before
    {
        get => GetValue(BeforeProperty);
        set => SetValue(BeforeProperty, value);
    }

    public Bitmap? After
    {
        get => GetValue(AfterProperty);
        set => SetValue(AfterProperty, value);
    }

    /// <summary>Divider position from 0 to 1 along the comparison axis.</summary>
    public double Fraction
    {
        get => GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    public ArcImageCompareOrientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public string? BeforeLabel
    {
        get => GetValue(BeforeLabelProperty);
        set => SetValue(BeforeLabelProperty, value);
    }

    public string? AfterLabel
    {
        get => GetValue(AfterLabelProperty);
        set => SetValue(AfterLabelProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    /// <summary>Stretches the handle into a capsule while the divider follows the pointer.</summary>
    public bool Dragging
    {
        get => GetValue(DraggingProperty);
        set => SetValue(DraggingProperty, value);
    }

    /// <summary>Width divided by height; the frame keeps this shape at any column width.</summary>
    public double AspectRatio
    {
        get => GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    public CornerRadius FrameRadius
    {
        get => GetValue(FrameRadiusProperty);
        set => SetValue(FrameRadiusProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) && availableSize.Width > 0 ? availableSize.Width : 420;
        var ratio = AspectRatio <= 0.2 ? 1.5 : AspectRatio;
        return new Size(width, width / ratio);
    }

    /// <summary>The pill around the divider, in surface coordinates; used for the double-click reset.</summary>
    public Rect HandleRect()
    {
        var (x, y) = DividerPoint();
        var width = Dragging ? 38 : 30;
        return new Rect(x - width / 2, y - 24, width, 48);
    }

    private (double X, double Y) DividerPoint()
    {
        var fraction = Math.Clamp(Fraction, 0, 1);
        return Orientation == ArcImageCompareOrientation.Horizontal
            ? (Bounds.Width * fraction, Bounds.Height / 2)
            : (Bounds.Width / 2, Bounds.Height * fraction);
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        var radius = FrameRadius;
        using (context.PushClip(new RoundedRect(rect, radius)))
        {
            var horizontal = Orientation == ArcImageCompareOrientation.Horizontal;
            var fraction = Math.Clamp(Fraction, 0, 1);

            // The result sits underneath; the original is revealed over it by a moving clip.
            DrawMedia(context, rect, After, after: true);
            using (context.PushClip(horizontal
                       ? new Rect(0, 0, rect.Width * fraction, rect.Height)
                       : new Rect(0, 0, rect.Width, rect.Height * fraction)))
            {
                DrawMedia(context, rect, Before, after: false);
            }

            DrawDivider(context, rect, horizontal, fraction);
            if (ShowLabels)
            {
                DrawCaptions(context, rect, horizontal, fraction);
            }
        }
    }

    private void DrawMedia(DrawingContext context, Rect rect, Bitmap? bitmap, bool after)
    {
        if (bitmap is not null)
        {
            var scale = Math.Max(rect.Width / bitmap.PixelSize.Width, rect.Height / bitmap.PixelSize.Height);
            var sourceWidth = rect.Width / scale;
            var sourceHeight = rect.Height / scale;
            var sourceX = (bitmap.PixelSize.Width - sourceWidth) / 2;
            var sourceY = (bitmap.PixelSize.Height - sourceHeight) / 2;
            context.DrawImage(bitmap, new Rect(sourceX, sourceY, sourceWidth, sourceHeight), rect);
            return;
        }

        DrawPlaceholder(context, rect, after);
    }

    /// <summary>A quiet mesh and gradient stand-in: different wash on each side, never a network request.</summary>
    private static void DrawPlaceholder(DrawingContext context, Rect rect, bool after)
    {
        if (after)
        {
            context.DrawRectangle(ArcMediaUi.Brush(ArcTokens.GradientSoft), null, rect);
        }
        else
        {
            context.DrawRectangle(ArcMediaUi.Brush(ArcTokens.SurfaceMuted), null, rect);
        }

        var lineToken = after ? ArcTokens.Series3 : ArcTokens.BorderStrong;
        var lineBrush = new SolidColorBrush(ArcColor.WithAlpha(ArcMediaUi.Color(lineToken), after ? 0.30 : 0.55));
        var pen = new Pen(lineBrush, 1);

        var step = 44.0;
        for (var x = -rect.Height; x < rect.Width; x += step)
        {
            context.DrawLine(pen, new Point(x, rect.Height), new Point(x + rect.Height, 0));
        }

        for (var x = 0.0; x < rect.Width; x += step)
        {
            context.DrawLine(pen, new Point(x, 0), new Point(x, rect.Height));
        }

        if (after)
        {
            var wash1 = new SolidColorBrush(ArcColor.WithAlpha(ArcMediaUi.Color(ArcTokens.Series2), 0.14));
            var wash2 = new SolidColorBrush(ArcColor.WithAlpha(ArcMediaUi.Color(ArcTokens.Series4), 0.12));
            context.DrawEllipse(wash1, null, new Point(rect.Width * 0.72, rect.Height * 0.34), rect.Width * 0.34, rect.Height * 0.42);
            context.DrawEllipse(wash2, null, new Point(rect.Width * 0.42, rect.Height * 0.78), rect.Width * 0.28, rect.Height * 0.30);
        }
        else
        {
            var wash = new SolidColorBrush(ArcColor.WithAlpha(ArcMediaUi.Color(ArcTokens.BorderStrong), 0.16));
            context.DrawEllipse(wash, null, new Point(rect.Width * 0.30, rect.Height * 0.40), rect.Width * 0.32, rect.Height * 0.40);
        }
    }

    private void DrawDivider(DrawingContext context, Rect rect, bool horizontal, double fraction)
    {
        var surface = ArcMediaUi.Brush(ArcTokens.Surface);
        var border = ArcMediaUi.Brush(ArcTokens.BorderStrong);
        var foreground = ArcMediaUi.Brush(ArcTokens.Foreground);
        var linePen = new Pen(new SolidColorBrush(ArcColor.WithAlpha(ArcMediaUi.Color(ArcTokens.Surface), 0.92)), 2);

        if (horizontal)
        {
            var x = rect.Width * fraction;
            context.DrawLine(linePen, new Point(x, 0), new Point(x, rect.Height));
        }
        else
        {
            var y = rect.Height * fraction;
            context.DrawLine(linePen, new Point(0, y), new Point(rect.Width, y));
        }

        var (cx, cy) = DividerPoint();
        var width = Dragging ? 38 : 30;
        var handle = new Rect(cx - width / 2, cy - 24, width, 48);
        context.DrawRectangle(surface, new Pen(border, 1), handle, 16, 16);

        var chevron = new Pen(foreground, 2, null, PenLineCap.Round, PenLineJoin.Round);
        if (horizontal)
        {
            context.DrawLine(chevron, new Point(cx - 5, cy - 4), new Point(cx - 9, cy));
            context.DrawLine(chevron, new Point(cx - 9, cy), new Point(cx - 5, cy + 4));
            context.DrawLine(chevron, new Point(cx + 5, cy - 4), new Point(cx + 9, cy));
            context.DrawLine(chevron, new Point(cx + 9, cy), new Point(cx + 5, cy + 4));
        }
        else
        {
            context.DrawLine(chevron, new Point(cx - 4, cy - 5), new Point(cx, cy - 9));
            context.DrawLine(chevron, new Point(cx, cy - 9), new Point(cx + 4, cy - 5));
            context.DrawLine(chevron, new Point(cx - 4, cy + 5), new Point(cx, cy + 9));
            context.DrawLine(chevron, new Point(cx, cy + 9), new Point(cx + 4, cy + 5));
        }

    }

    private void DrawCaptions(DrawingContext context, Rect rect, bool horizontal, double fraction)
    {
        var size = ArcResources.Double(ArcTokens.TextXs);
        var before = ArcMediaUi.Formatted(BeforeLabel ?? "Before", size, ArcMediaUi.Brush(ArcTokens.Foreground), FontWeight.Medium);
        var after = ArcMediaUi.Formatted(AfterLabel ?? "After", size, ArcMediaUi.Brush(ArcTokens.Foreground), FontWeight.Medium);

        var beforeWidth = before.Width + 20;
        var afterWidth = after.Width + 20;
        var height = before.Height + 10;

        var beforeOrigin = new Point(12, 12);
        var afterOrigin = new Point(rect.Width - afterWidth - 12, 12);
        if (!horizontal)
        {
            beforeOrigin = new Point(12, 12);
            afterOrigin = new Point(12, rect.Height - height - 12);
        }

        var divider = horizontal ? rect.Width * fraction : rect.Height * fraction;
        var beforeAlpha = horizontal
            ? Math.Clamp((divider - (beforeOrigin.X + beforeWidth)) / 48.0, 0, 1)
            : Math.Clamp((divider - (beforeOrigin.Y + height)) / 48.0, 0, 1);
        var afterAlpha = horizontal
            ? Math.Clamp(((afterOrigin.X - divider) / 48.0), 0, 1)
            : Math.Clamp(((afterOrigin.Y - divider) / 48.0), 0, 1);

        DrawChip(context, before, beforeOrigin, beforeWidth, height, beforeAlpha);
        DrawChip(context, after, afterOrigin, afterWidth, height, afterAlpha);
    }

    private static void DrawChip(DrawingContext context, FormattedText text, Point origin, double width, double height, double alpha)
    {
        if (alpha <= 0.01)
        {
            return;
        }

        using (context.PushOpacity(alpha))
        {
            var chip = new Rect(origin.X, origin.Y, width, height);
            var background = new SolidColorBrush(ArcColor.WithAlpha(ArcMediaUi.Color(ArcTokens.Surface), 0.92));
            context.DrawRectangle(background, new Pen(ArcMediaUi.Brush(ArcTokens.Border), 1), chip, 999, 999);
            context.DrawText(text, new Point(origin.X + 10, origin.Y + 5));
        }
    }
}

/// <summary>
/// Arc's image compare: two images stacked under a draggable divider, moved by pointer or by arrow keys,
/// reporting the settled percentage. The handle recenters on a double click.
/// </summary>
[TemplatePart("PART_Surface", typeof(ArcCompareSurface))]
[TemplatePart("PART_Status", typeof(TextBlock))]
public class ArcImageCompare : TemplatedControl
{
    public static readonly StyledProperty<string?> BeforeSourceProperty =
        AvaloniaProperty.Register<ArcImageCompare, string?>(nameof(BeforeSource));

    public static readonly StyledProperty<string?> AfterSourceProperty =
        AvaloniaProperty.Register<ArcImageCompare, string?>(nameof(AfterSource));

    public static readonly StyledProperty<double?> PositionProperty =
        AvaloniaProperty.Register<ArcImageCompare, double?>(nameof(Position));

    public static readonly StyledProperty<double> DefaultPositionProperty =
        AvaloniaProperty.Register<ArcImageCompare, double>(nameof(DefaultPosition), 50d);

    public static readonly StyledProperty<ArcImageCompareOrientation> OrientationProperty =
        AvaloniaProperty.Register<ArcImageCompare, ArcImageCompareOrientation>(nameof(Orientation));

    public static readonly StyledProperty<string?> BeforeLabelProperty =
        AvaloniaProperty.Register<ArcImageCompare, string?>(nameof(BeforeLabel), "Before");

    public static readonly StyledProperty<string?> AfterLabelProperty =
        AvaloniaProperty.Register<ArcImageCompare, string?>(nameof(AfterLabel), "After");

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<ArcImageCompare, bool>(nameof(ShowLabels), true);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcImageCompare, string>(nameof(Label), "Before and after");

    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<ArcImageCompare, double>(nameof(AspectRatio), 1.5);

    private ArcCompareSurface? _surface;
    private TextBlock? _status;
    private Bitmap? _before;
    private Bitmap? _after;
    private double _position;
    private bool _pointerDown;

    static ArcImageCompare()
    {
        BeforeSourceProperty.Changed.AddClassHandler<ArcImageCompare>((compare, _) => compare.Reload());
        AfterSourceProperty.Changed.AddClassHandler<ArcImageCompare>((compare, _) => compare.Reload());
        PositionProperty.Changed.AddClassHandler<ArcImageCompare>((compare, _) => compare.SyncPosition());
        DefaultPositionProperty.Changed.AddClassHandler<ArcImageCompare>((compare, _) =>
        {
            if (compare.Position is null)
            {
                compare.SyncPosition();
            }
        });
        OrientationProperty.Changed.AddClassHandler<ArcImageCompare>((compare, _) => compare.SyncSurface());
        BeforeLabelProperty.Changed.AddClassHandler<ArcImageCompare>((compare, _) => compare.SyncSurface());
        AfterLabelProperty.Changed.AddClassHandler<ArcImageCompare>((compare, _) => compare.SyncSurface());
        ShowLabelsProperty.Changed.AddClassHandler<ArcImageCompare>((compare, _) => compare.SyncSurface());
        AspectRatioProperty.Changed.AddClassHandler<ArcImageCompare>((compare, _) => compare.SyncSurface());
        LabelProperty.Changed.AddClassHandler<ArcImageCompare>((compare, _) => compare.UpdateAutomation());
    }

    public ArcImageCompare()
    {
        Focusable = true;
        IsTabStop = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        DoubleTapped += OnDoubleTapped;
    }

    /// <summary>Path or avares URI for the original; a placeholder mesh is drawn when it cannot be read.</summary>
    public string? BeforeSource
    {
        get => GetValue(BeforeSourceProperty);
        set => SetValue(BeforeSourceProperty, value);
    }

    /// <summary>Path or avares URI for the result.</summary>
    public string? AfterSource
    {
        get => GetValue(AfterSourceProperty);
        set => SetValue(AfterSourceProperty, value);
    }

    /// <summary>Controlled divider position in percent; leave null to let the component manage it.</summary>
    public double? Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <summary>Initial position when uncontrolled.</summary>
    public double DefaultPosition
    {
        get => GetValue(DefaultPositionProperty);
        set => SetValue(DefaultPositionProperty, value);
    }

    public ArcImageCompareOrientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public string? BeforeLabel
    {
        get => GetValue(BeforeLabelProperty);
        set => SetValue(BeforeLabelProperty, value);
    }

    public string? AfterLabel
    {
        get => GetValue(AfterLabelProperty);
        set => SetValue(AfterLabelProperty, value);
    }

    /// <summary>False hides both caption chips.</summary>
    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    /// <summary>Accessible name of the divider.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Width divided by height for the frame.</summary>
    public double AspectRatio
    {
        get => GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    /// <summary>Current divider position in percent.</summary>
    public double Value => _position;

    /// <summary>Raised when the divider settles on a new position.</summary>
    public static readonly RoutedEvent<ArcImageComparePositionChangedRoutedEventArgs> PositionChangedEvent =
        RoutedEvent.Register<ArcImageCompare, ArcImageComparePositionChangedRoutedEventArgs>("PositionChanged", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="PositionChanged"/>.</summary>
    public event EventHandler<ArcImageComparePositionChangedRoutedEventArgs>? PositionChangedRouted
    {
        add => AddHandler(PositionChangedEvent, value);
        remove => RemoveHandler(PositionChangedEvent, value);
    }

    public event EventHandler<double>? PositionChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _surface = e.NameScope.Find<ArcCompareSurface>("PART_Surface");
        _status = e.NameScope.Find<TextBlock>("PART_Status");
        SyncPosition();
        Reload();
        UpdateAutomation();
        SyncSurface();
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Label) ? "Before and after" : Label);
        AutomationProperties.SetHelpText(this,
            "Drag the divider, or use the arrow keys. Hold Shift for ten percent steps. Home and End show one side only.");
    }

    private void SyncPosition()
    {
        _position = Math.Clamp(Position ?? DefaultPosition, 0, 100);
        if (_surface is not null)
        {
            _surface.Fraction = _position / 100.0;
        }

        Announce();
    }

    private void SyncSurface()
    {
        if (_surface is null)
        {
            return;
        }

        _surface.Orientation = Orientation;
        _surface.BeforeLabel = BeforeLabel;
        _surface.AfterLabel = AfterLabel;
        _surface.ShowLabels = ShowLabels;
        _surface.AspectRatio = AspectRatio;
        _surface.Before = _before;
        _surface.After = _after;
        _surface.Fraction = _position / 100.0;
    }

    private void Reload()
    {
        _before?.Dispose();
        _after?.Dispose();
        _before = LoadBitmap(BeforeSource);
        _after = LoadBitmap(AfterSource);
        SyncSurface();
    }

    private static Bitmap? LoadBitmap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        try
        {
            if (source.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = AssetLoader.Open(new Uri(source));
                return new Bitmap(stream);
            }

            return File.Exists(source) ? new Bitmap(source) : null;
        }
        catch
        {
            return null;
        }
    }

    private void Announce()
    {
        if (_status is null)
        {
            return;
        }

        _status.Text = Orientation == ArcImageCompareOrientation.Horizontal
            ? $"{Math.Round(_position)} percent after"
            : $"{Math.Round(_position)} percent after, top to bottom";
    }

    private void SetPosition(double percent, bool settle)
    {
        _position = Math.Clamp(percent, 0, 100);
        if (_surface is not null)
        {
            _surface.Fraction = _position / 100.0;
        }

        Announce();
        if (settle)
        {
            PositionChanged?.Invoke(this, _position);
        RaiseEvent(new ArcImageComparePositionChangedRoutedEventArgs(PositionChangedEvent, _position));
        }
    }

    private double PercentFromPoint(Point point)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return _position;
        }

        return Orientation == ArcImageCompareOrientation.Horizontal
            ? Math.Clamp(point.X / Bounds.Width * 100, 0, 100)
            : Math.Clamp(point.Y / Bounds.Height * 100, 0, 100);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus(NavigationMethod.Pointer);

        if (e.ClickCount == 2 && _surface is not null &&
            _surface.HandleRect().Contains(e.GetPosition(_surface)))
        {
            SetPosition(50, settle: true);
            e.Handled = true;
            return;
        }

        _pointerDown = true;
        if (_surface is not null)
        {
            _surface.Dragging = true;
        }

        e.Pointer.Capture(this);
        SetPosition(PercentFromPoint(e.GetPosition(this)), settle: false);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pointerDown || e.Pointer.Captured != this)
        {
            return;
        }

        SetPosition(PercentFromPoint(e.GetPosition(this)), settle: false);
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
        if (_surface is not null)
        {
            _surface.Dragging = false;
        }

        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        SetPosition(_position, settle: true);
        e.Handled = true;
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_surface is null || PositionsEqual(_position, 50))
        {
            return;
        }

        // The press handler already recentered when the double click landed on the handle; this covers
        // a double click anywhere on the frame, which is the discoverable version of the same reset.
        SetPosition(50, settle: true);
        e.Handled = true;
    }

    private static bool PositionsEqual(double a, double b) => Math.Abs(a - b) < 0.01;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10.0 : 1.0;
        var handled = true;

        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
                SetPosition(_position - step, settle: true);
                break;
            case Key.Right:
            case Key.Down:
                SetPosition(_position + step, settle: true);
                break;
            case Key.PageDown:
                SetPosition(_position - 10, settle: true);
                break;
            case Key.PageUp:
                SetPosition(_position + 10, settle: true);
                break;
            case Key.Home:
                SetPosition(0, settle: true);
                break;
            case Key.End:
                SetPosition(100, settle: true);
                break;
            default:
                handled = false;
                break;
        }

        if (handled)
        {
            e.Handled = true;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _pointerDown = false;
        if (_surface is not null)
        {
            _surface.Dragging = false;
        }
    }
}



