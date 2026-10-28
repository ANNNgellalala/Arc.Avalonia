using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>Ink colour choices; they map to semantic tokens so the pad follows the theme.</summary>
public enum ArcInkColor
{
    Black,
    Blue,
    Violet
}

/// <summary>Nib width choices.</summary>
public enum ArcInkWidth
{
    Fine,
    Medium,
    Bold
}

/// <summary>One sampled point of ink in the pad's 600 by 260 coordinate space.</summary>
public readonly record struct ArcInkPoint(double X, double Y, double Width);

/// <summary>A single ink stroke. Store the list from <c>StrokesChanged</c> and replay it later.</summary>
public sealed class ArcInkStroke
{
    public ArcInkStroke()
    {
    }

    public ArcInkStroke(ArcInkColor color, IEnumerable<ArcInkPoint> points)
    {
        Color = color;
        Points = points.ToList();
    }

    public ArcInkColor Color { get; set; } = ArcInkColor.Black;

    public List<ArcInkPoint> Points { get; set; } = new();
}

/// <summary>
/// The drawing surface: pointer capture, speed-thinned ink, the signature baseline and the resting hint.
/// Coordinates are stored in a 600 by 260 space so ink scales with the control and can be exported.
/// </summary>
public sealed class ArcSignatureSurface : Control
{
    public const double PadWidth = 600;
    public const double PadHeight = 260;
    private const double BaselineY = 198;

    public static readonly StyledProperty<IReadOnlyList<ArcInkStroke>?> StrokesProperty =
        AvaloniaProperty.Register<ArcSignatureSurface, IReadOnlyList<ArcInkStroke>?>(nameof(Strokes));

    public static readonly StyledProperty<ArcInkStroke?> LiveStrokeProperty =
        AvaloniaProperty.Register<ArcSignatureSurface, ArcInkStroke?>(nameof(LiveStroke));

    public static readonly StyledProperty<string?> SignerProperty =
        AvaloniaProperty.Register<ArcSignatureSurface, string?>(nameof(Signer));

    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<ArcSignatureSurface, string?>(nameof(Hint), "Sign here");

    public static readonly StyledProperty<ArcInkColor> InkColorProperty =
        AvaloniaProperty.Register<ArcSignatureSurface, ArcInkColor>(nameof(InkColor));

    public static readonly StyledProperty<ArcInkWidth> InkWidthProperty =
        AvaloniaProperty.Register<ArcSignatureSurface, ArcInkWidth>(nameof(InkWidth), ArcInkWidth.Medium);

    private ArcInkStroke? _drawing;
    private Point _last;
    private bool _showFocusRing;
    private bool _pointerFocus;

    static ArcSignatureSurface()
    {
        AffectsRender<ArcSignatureSurface>(StrokesProperty, LiveStrokeProperty, SignerProperty, HintProperty);
        FocusableProperty.OverrideDefaultValue<ArcSignatureSurface>(true);
        IsTabStopProperty.OverrideDefaultValue<ArcSignatureSurface>(true);
    }

    public ArcSignatureSurface()
    {
        Cursor = new Cursor(StandardCursorType.Cross);
        AutomationProperties.SetName(this, "Signature pad");
    }

    public IReadOnlyList<ArcInkStroke>? Strokes
    {
        get => GetValue(StrokesProperty);
        set => SetValue(StrokesProperty, value);
    }

    public ArcInkStroke? LiveStroke
    {
        get => GetValue(LiveStrokeProperty);
        set => SetValue(LiveStrokeProperty, value);
    }

    /// <summary>Printed name under the baseline.</summary>
    public string? Signer
    {
        get => GetValue(SignerProperty);
        set => SetValue(SignerProperty, value);
    }

    /// <summary>Resting hint on the baseline before the first stroke.</summary>
    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public ArcInkColor InkColor
    {
        get => GetValue(InkColorProperty);
        set => SetValue(InkColorProperty, value);
    }

    public ArcInkWidth InkWidth
    {
        get => GetValue(InkWidthProperty);
        set => SetValue(InkWidthProperty, value);
    }

    internal event EventHandler<ArcInkStroke>? StrokeCompleted;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) && availableSize.Width > 0
            ? Math.Min(availableSize.Width, PadWidth)
            : PadWidth;
        return new Size(width, width * PadHeight / PadWidth);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        if (width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var scale = width / PadWidth;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            var border = ArcResources.Brush(ArcTokens.BorderStrong);
            var muted = ArcResources.Brush(ArcTokens.TextMuted);
            context.DrawLine(new Pen(border, 1), new Point(24, BaselineY), new Point(PadWidth - 24, BaselineY));

            var strokes = Strokes;
            var hasInk = strokes is { Count: > 0 } || LiveStroke is { Points.Count: > 0 };

            if (!hasInk && !string.IsNullOrWhiteSpace(Hint))
            {
                var hint = Text(Hint!, 20, muted, FontWeight.Normal, PadWidth - 48);
                context.DrawText(hint, new Point(24, BaselineY - 34));
            }

            if (strokes is not null)
            {
                foreach (var stroke in strokes)
                {
                    DrawStroke(context, stroke);
                }
            }

            if (LiveStroke is { } live)
            {
                DrawStroke(context, live);
            }

            if (!string.IsNullOrWhiteSpace(Signer))
            {
                var signer = Text(Signer!, 13, muted, FontWeight.Medium, PadWidth - 48);
                signer.TextAlignment = TextAlignment.Right;
                context.DrawText(signer, new Point(24, BaselineY + 14));
            }
        }

        if (_showFocusRing)
        {
            var ring = new Rect(1, 1, Math.Max(0, Bounds.Width - 2), Math.Max(0, Bounds.Height - 2));
            context.DrawRectangle(null, new Pen(ArcResources.Brush(ArcTokens.FocusOutline), 2), ring, 20);
        }
    }

    private static FormattedText Text(string value, double size, IBrush brush, FontWeight weight, double maxWidth) =>
        new(value, CultureInfo.GetCultureInfo("en-US"), FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, weight), size, brush)
        {
            MaxTextWidth = maxWidth
        };

    internal static Color Ink(ArcInkColor color)
    {
        var dark = ArcThemeManager.Scheme == ArcColorScheme.Dark;

        // Ink is content, not chrome: the three nib colours keep their hue in both themes and only
        // shift lightness so the stroke stays legible on the pad surface.
        return color switch
        {
            ArcInkColor.Blue => ArcColor.Oklch(dark ? 0.74 : 0.50, 0.15, 258),
            ArcInkColor.Violet => ArcColor.Oklch(dark ? 0.76 : 0.52, 0.17, 300),
            _ => ArcResources.Color(ArcTokens.Foreground)
        };
    }

    internal static void DrawStroke(DrawingContext context, ArcInkStroke stroke)
    {
        var points = stroke.Points;
        if (points.Count == 0)
        {
            return;
        }

        var brush = new SolidColorBrush(Ink(stroke.Color));
        if (points.Count == 1)
        {
            var only = points[0];
            context.DrawEllipse(brush, null, new Point(only.X, only.Y), only.Width / 2, only.Width / 2);
            return;
        }

        // Widths are smoothed so the speed-based thinning reads as a taper, not as noise.
        var widths = new double[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            double sum = 0;
            for (var offset = -2; offset <= 2; offset++)
            {
                sum += points[Math.Clamp(index + offset, 0, points.Count - 1)].Width;
            }

            widths[index] = Math.Max(0.35, sum / 5);
        }

        // Offset the centreline by half the nib width on each side to build one filled outline.
        var left = new Point[points.Count];
        var right = new Point[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            var previous = points[Math.Max(index - 1, 0)];
            var next = points[Math.Min(index + 1, points.Count - 1)];
            var dx = next.X - previous.X;
            var dy = next.Y - previous.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.0001)
            {
                dx = 1;
                dy = 0;
                length = 1;
            }

            var normalX = -dy / length;
            var normalY = dx / length;
            var half = widths[index] / 2;
            left[index] = new Point(points[index].X + normalX * half, points[index].Y + normalY * half);
            right[index] = new Point(points[index].X - normalX * half, points[index].Y - normalY * half);
        }

        var outline = new StreamGeometry();
        using (var path = outline.Open())
        {
            path.SetFillRule(FillRule.NonZero);
            path.BeginFigure(left[0], true);
            for (var index = 1; index < left.Length; index++)
            {
                path.LineTo(left[index]);
            }

            for (var index = right.Length - 1; index >= 0; index--)
            {
                path.LineTo(right[index]);
            }

            path.EndFigure(true);
        }

        context.DrawGeometry(brush, null, outline);

        // Round caps close the two ends.
        var start = points[0];
        context.DrawEllipse(brush, null, new Point(start.X, start.Y), widths[0] / 2, widths[0] / 2);
        var end = points[^1];
        context.DrawEllipse(brush, null, new Point(end.X, end.Y), widths[^1] / 2, widths[^1] / 2);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Bounds.Width <= 0)
        {
            return;
        }

        Focus();
        _pointerFocus = true;
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => _pointerFocus = false);
        e.Pointer.Capture(this);
        var point = ToPad(e.GetPosition(this));
        _last = point;
        _drawing = new ArcInkStroke { Color = InkColor };
        _drawing.Points.Add(new ArcInkPoint(point.X, point.Y, BaseWidth()));
        SetCurrentValue(LiveStrokeProperty, _drawing);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Captured != this || _drawing is null)
        {
            return;
        }

        var point = ToPad(e.GetPosition(this));
        var dx = point.X - _last.X;
        var dy = point.Y - _last.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < 0.6)
        {
            return;
        }

        // Fast strokes thin out, matching Arc's speed-sensitive nib.
        var factor = Math.Clamp(1.2 - distance / 42.0, 0.45, 1.15);
        _drawing.Points.Add(new ArcInkPoint(point.X, point.Y, BaseWidth() * factor));
        _last = point;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        if (_drawing is not null)
        {
            var completed = _drawing;
            _drawing = null;
            SetCurrentValue(LiveStrokeProperty, (ArcInkStroke?)null);
            StrokeCompleted?.Invoke(this, completed);
            InvalidateVisual();
        }
    }

    private double BaseWidth() => InkWidth switch
    {
        ArcInkWidth.Fine => 1.8,
        ArcInkWidth.Bold => 4.0,
        _ => 2.7
    };

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _showFocusRing = !_pointerFocus;
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _showFocusRing = false;
        InvalidateVisual();
    }

    private Point ToPad(Point point)
    {
        var scale = PadWidth / Bounds.Width;
        return new Point(
            Math.Clamp(point.X * scale, 0, PadWidth),
            Math.Clamp(point.Y * PadWidth / Bounds.Width, 0, PadHeight));
    }
}

/// <summary>
/// Arc's signature pad: smooth ink that thins with speed, undo and clear, and a transparent PNG export at
/// three times the pad resolution. Strokes are exposed so a form can persist and replay them.
/// </summary>
public class ArcSignaturePad : TemplatedControl
{
    private readonly List<List<ArcInkStroke>> _history = new() { new List<ArcInkStroke>() };
    private int _index;
    private readonly List<(Button Button, ArcInkColor Color)> _colorButtons = new();
    private readonly List<(Button Button, ArcInkWidth Width)> _widthButtons = new();
    private ArcSignatureSurface? _surface;
    private Button? _undo;
    private Button? _clear;
    private StackPanel? _colors;
    private StackPanel? _widths;

    public static readonly StyledProperty<string?> SignerProperty =
        AvaloniaProperty.Register<ArcSignaturePad, string?>(nameof(Signer));

    public static readonly StyledProperty<string> HintProperty =
        AvaloniaProperty.Register<ArcSignaturePad, string>(nameof(Hint), "Sign here");

    public static readonly StyledProperty<ArcInkColor> InkColorProperty =
        AvaloniaProperty.Register<ArcSignaturePad, ArcInkColor>(nameof(InkColor));

    public static readonly StyledProperty<ArcInkWidth> InkWidthProperty =
        AvaloniaProperty.Register<ArcSignaturePad, ArcInkWidth>(nameof(InkWidth), ArcInkWidth.Medium);

    public static readonly StyledProperty<string> FileNameProperty =
        AvaloniaProperty.Register<ArcSignaturePad, string>(nameof(FileName), "signature");

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcSignaturePad, string>(nameof(Label), "Signature pad");

    static ArcSignaturePad()
    {
        InkColorProperty.Changed.AddClassHandler<ArcSignaturePad>((pad, _) => pad.UpdateToolbar());
        InkWidthProperty.Changed.AddClassHandler<ArcSignaturePad>((pad, _) => pad.UpdateToolbar());
        SignerProperty.Changed.AddClassHandler<ArcSignaturePad>((pad, _) => pad.SyncSurface());
        HintProperty.Changed.AddClassHandler<ArcSignaturePad>((pad, _) => pad.SyncSurface());
    }

    /// <summary>Printed name under the baseline.</summary>
    public string? Signer
    {
        get => GetValue(SignerProperty);
        set => SetValue(SignerProperty, value);
    }

    /// <summary>Resting hint before the first stroke.</summary>
    public string Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    /// <summary>Current ink colour.</summary>
    public ArcInkColor InkColor
    {
        get => GetValue(InkColorProperty);
        set => SetValue(InkColorProperty, value);
    }

    /// <summary>Current nib width.</summary>
    public ArcInkWidth InkWidth
    {
        get => GetValue(InkWidthProperty);
        set => SetValue(InkWidthProperty, value);
    }

    /// <summary>File name for exports, without extension.</summary>
    public string FileName
    {
        get => GetValue(FileNameProperty);
        set => SetValue(FileNameProperty, value);
    }

    /// <summary>Accessible name of the drawing surface.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Current strokes, in pad coordinates.</summary>
    public IReadOnlyList<ArcInkStroke> Strokes => _history[_index];

    /// <summary>True when the pad holds ink.</summary>
    public bool HasInk => Strokes.Count > 0;

    /// <summary>Raised after every change, including undo, redo and clear.</summary>
    public event EventHandler<IReadOnlyList<ArcInkStroke>>? StrokesChanged;

    /// <summary>Raised after <see cref="SavePng"/> wrote a file.</summary>
    public event EventHandler<string>? PngSaved;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_surface is not null)
        {
            _surface.StrokeCompleted -= OnStrokeCompleted;
        }

        if (_undo is not null)
        {
            _undo.Click -= OnUndoClick;
        }

        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }

        _surface = e.NameScope.Find<ArcSignatureSurface>("PART_Surface");
        _undo = e.NameScope.Find<Button>("PART_Undo");
        _clear = e.NameScope.Find<Button>("PART_Clear");
        _colors = e.NameScope.Find<StackPanel>("PART_Colors");
        _widths = e.NameScope.Find<StackPanel>("PART_Widths");

        if (_surface is not null)
        {
            _surface.StrokeCompleted += OnStrokeCompleted;
            AutomationProperties.SetName(_surface, Label);
            AutomationProperties.SetHelpText(_surface, "Control Z undoes, Control Shift Z redoes, Delete clears.");
        }

        if (_undo is not null)
        {
            _undo.Click += OnUndoClick;
            AutomationProperties.SetName(_undo, "Undo stroke");
            ToolTip.SetTip(_undo, "Undo");
        }

        if (_clear is not null)
        {
            _clear.Click += OnClearClick;
            AutomationProperties.SetName(_clear, "Clear signature");
            ToolTip.SetTip(_clear, "Clear");
        }

        BuildToolbar();
        SyncSurface();
    }

    /// <summary>Undoes the last stroke or the clear.</summary>
    public void Undo()
    {
        if (_index <= 0)
        {
            return;
        }

        _index--;
        SyncSurface();
        Publish();
    }

    /// <summary>Replays an undone change.</summary>
    public void Redo()
    {
        if (_index >= _history.Count - 1)
        {
            return;
        }

        _index++;
        SyncSurface();
        Publish();
    }

    /// <summary>Clears the pad; undo brings the ink back.</summary>
    public void Clear()
    {
        if (Strokes.Count == 0)
        {
            return;
        }

        Push(new List<ArcInkStroke>());
        SyncSurface();
        Publish();
    }

    /// <summary>Replaces the ink, for reopening a stored signature.</summary>
    public void LoadStrokes(IEnumerable<ArcInkStroke> strokes)
    {
        _history.Clear();
        _history.Add(strokes.Select(Clone).ToList());
        _index = 0;
        SyncSurface();
        Publish();
    }

    /// <summary>
    /// Writes the ink to a transparent PNG cropped to the strokes at three times the pad resolution,
    /// matching Arc's signatureToPng. Does nothing when the pad is empty.
    /// </summary>
    public void SavePng(string path)
    {
        if (Strokes.Count == 0)
        {
            return;
        }

        const int scale = 3;
        const double padding = 18;

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var point in Strokes.SelectMany(stroke => stroke.Points))
        {
            minX = Math.Min(minX, point.X - point.Width / 2);
            minY = Math.Min(minY, point.Y - point.Width / 2);
            maxX = Math.Max(maxX, point.X + point.Width / 2);
            maxY = Math.Max(maxY, point.Y + point.Width / 2);
        }

        var width = Math.Max(1, maxX - minX + padding * 2);
        var height = Math.Max(1, maxY - minY + padding * 2);
        var pixels = new PixelSize((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale));

        using var bitmap = new RenderTargetBitmap(pixels, new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            using (context.PushTransform(
                       Matrix.CreateTranslation(padding - minX, padding - minY) * Matrix.CreateScale(scale, scale)))
            {
                foreach (var stroke in Strokes)
                {
                    ArcSignatureSurface.DrawStroke(context, stroke);
                }
            }
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        bitmap.Save(path, new PngBitmapEncoderOptions());
        PngSaved?.Invoke(this, path);
    }

    /// <summary>The default export path for this pad, in the user's pictures folder.</summary>
    public string DefaultExportPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), $"{FileName}.png");

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (control && e.Key == Key.Z)
        {
            if (shift)
            {
                Redo();
            }
            else
            {
                Undo();
            }

            e.Handled = true;
        }
        else if (control && e.Key == Key.Y)
        {
            Redo();
            e.Handled = true;
        }
        else if (!control && e.Key is Key.Delete or Key.Back)
        {
            Clear();
            e.Handled = true;
        }
    }

    private void OnStrokeCompleted(object? sender, ArcInkStroke stroke)
    {
        var next = _history[_index].Select(Clone).ToList();
        next.Add(stroke);
        Push(next);
        Publish();
    }

    private void Push(List<ArcInkStroke> strokes)
    {
        if (_index < _history.Count - 1)
        {
            _history.RemoveRange(_index + 1, _history.Count - _index - 1);
        }

        _history.Add(strokes);
        if (_history.Count > 61)
        {
            _history.RemoveAt(0);
        }

        _index = _history.Count - 1;
    }

    private void Publish()
    {
        StrokesChanged?.Invoke(this, Strokes);
    }

    private void SyncSurface()
    {
        if (_surface is null)
        {
            return;
        }

        _surface.Strokes = Strokes;
        _surface.Signer = Signer;
        _surface.Hint = Hint;
        _surface.InkColor = InkColor;
        _surface.InkWidth = InkWidth;
        AutomationProperties.SetName(_surface, $"{Label}, {Strokes.Count} strokes");

        if (_undo is not null)
        {
            _undo.IsEnabled = _index > 0;
        }

        if (_clear is not null)
        {
            _clear.IsEnabled = Strokes.Count > 0;
        }
    }

    private static ArcInkStroke Clone(ArcInkStroke stroke) => new(stroke.Color, stroke.Points);

    private void BuildToolbar()
    {
        if (_colors is null || _widths is null)
        {
            return;
        }

        _colors.Children.Clear();
        _colorButtons.Clear();
        _widths.Children.Clear();
        _widthButtons.Clear();

        var swatchTheme = this.TryFindResource("ArcPickerSwatchTheme", out var theme) ? theme as ControlTheme : null;

        foreach (var color in new[] { ArcInkColor.Black, ArcInkColor.Blue, ArcInkColor.Violet })
        {
            var button = new Button
            {
                Theme = swatchTheme,
                Background = new SolidColorBrush(ArcSignatureSurface.Ink(color))
            };
            AutomationProperties.SetName(button, $"{color} ink");
            ToolTip.SetTip(button, $"{color} ink");
            button.Click += (_, e) =>
            {
                SetCurrentValue(InkColorProperty, color);
                e.Handled = true;
            };
            _colorButtons.Add((button, color));
            _colors.Children.Add(button);
        }

        foreach (var width in new[] { ArcInkWidth.Fine, ArcInkWidth.Medium, ArcInkWidth.Bold })
        {
            var dot = new Border
            {
                Width = width switch { ArcInkWidth.Fine => 4, ArcInkWidth.Bold => 10, _ => 7 },
                Height = width switch { ArcInkWidth.Fine => 4, ArcInkWidth.Bold => 10, _ => 7 },
                CornerRadius = new CornerRadius(99),
                Background = ArcResources.Brush(ArcTokens.Foreground),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var button = new Button
            {
                Theme = swatchTheme,
                Background = ArcResources.Brush(ArcTokens.SurfaceMuted),
                Content = dot
            };
            AutomationProperties.SetName(button, $"{width} nib");
            ToolTip.SetTip(button, $"{width} nib");
            button.Click += (_, e) =>
            {
                SetCurrentValue(InkWidthProperty, width);
                e.Handled = true;
            };
            _widthButtons.Add((button, width));
            _widths.Children.Add(button);
        }

        UpdateToolbar();
    }

    private void UpdateToolbar()
    {
        foreach (var (button, color) in _colorButtons)
        {
            button.Classes.Set("selected", color == InkColor);
        }

        foreach (var (button, width) in _widthButtons)
        {
            button.Classes.Set("selected", width == InkWidth);
        }

        SyncSurface();
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        Undo();
        e.Handled = true;
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        Clear();
        e.Handled = true;
    }
}
