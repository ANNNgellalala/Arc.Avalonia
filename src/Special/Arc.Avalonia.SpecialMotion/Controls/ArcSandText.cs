using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Platform;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMotion.Controls;

/// <summary>
/// A headline made of sand. Grains pour into the letters bottom first like jars, the pointer digs through
/// them and throws grains that tumble away, and at rest the missing grains pour back in. Reducing motion
/// shows the letters formed and turns digging off.
/// </summary>
public class ArcSandText : ArcMotionCanvas
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ArcSandText, string?>(nameof(Text), "DUNES");

    public static readonly StyledProperty<double> GrainProperty =
        AvaloniaProperty.Register<ArcSandText, double>(nameof(Grain), 2.4);

    public static readonly StyledProperty<double> DigRadiusProperty =
        AvaloniaProperty.Register<ArcSandText, double>(nameof(DigRadius), 18);

    private readonly List<Particle> _grains = new();
    private int _dugCount;
    private bool[] _mask = Array.Empty<bool>();
    private float[] _fillAt = Array.Empty<float>();
    private uint[] _colors = Array.Empty<uint>();
    private double[] _dugAt = Array.Empty<double>();
    private WriteableBitmap? _bitmap;
    private int _cols;
    private int _rows;
    private double _builtWidth;
    private double _builtHeight;
    private string _builtText = string.Empty;
    private Color _builtInk;
    private double _pour;
    private double _now;
    private bool _frameStarted;
    private Point _lastPointer;

    static ArcSandText()
    {
        TextProperty.Changed.AddClassHandler<ArcSandText>((sand, _) => sand.Rebuild());
    }

    public ArcSandText()
    {
        Focusable = false;
        IsTabStop = false;
        Cursor = new Cursor(StandardCursorType.Arrow);
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    /// <summary>One short line of text. Changing it collapses the old word and pours the new one.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Grain size in px; the grid is derived from it and the box size.</summary>
    public double Grain
    {
        get => GetValue(GrainProperty);
        set => SetValue(GrainProperty, value);
    }

    /// <summary>Radius the pointer digs through, in px.</summary>
    public double DigRadius
    {
        get => GetValue(DigRadiusProperty);
        set => SetValue(DigRadiusProperty, value);
    }

    protected override double DefaultWidth => 960;

    protected override double DefaultHeight => 320;

    protected override bool NeedsFrame =>
        base.NeedsFrame || (!Sm.Reduced && (_pour < 1.2 || _grains.Count > 0 || _dugCount > 0 || _pointerInside));

    private bool _pointerInside;

    private void Rebuild()
    {
        _builtWidth = 0;
        _builtHeight = 0;
        AutomationProperties.SetName(this, Text ?? string.Empty);
        InvalidateMeasure();
        InvalidateVisual();
        if (!Sm.Reduced)
        {
            EnsureFrame();
        }
    }

    /// <summary>Pours the current word in again from the top, as if it had just changed.</summary>
    public void Reveal()
    {
        _pour = Sm.Reduced ? 2 : 0;
        _grains.Clear();
        for (var index = 0; index < _dugAt.Length; index++)
        {
            _dugAt[index] = -1;
        }

        _dugCount = 0;

        _frameStarted = false;
        EnsureFrame();
        InvalidateVisual();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        if (_cols <= 0 || Math.Abs(finalSize.Width - _builtWidth) > 0.5 || Math.Abs(finalSize.Height - _builtHeight) > 0.5 ||
            !string.Equals(_builtText, Text ?? string.Empty, StringComparison.Ordinal))
        {
            Build(finalSize);
        }

        return result;
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        var ink = Sm.Color(ArcTokens.Foreground);
        var bitmap = _bitmap;
        if (bitmap is null || _cols <= 0)
        {
            return;
        }

        if (ink != _builtInk)
        {
            BuildColors(ink);
        }

        Paint();
        context.DrawImage(bitmap, new Rect(0, 0, _cols, _rows), new Rect(0, 0, size.Width, size.Height));

        foreach (var grain in _grains)
        {
            var alpha = Sm.Clamp01(grain.Life / grain.MaxLife);
            var color = ArcColor.WithAlpha(ArcColor.Mix(ink, Sm.Color(ArcTokens.Accent), 0.22), alpha);
            context.DrawRectangle(Sm.Solid(color), null, new Rect(grain.X, grain.Y, Grain, Grain));
        }

        if (!Sm.Reduced && _pour < 1.2 && !_frameStarted)
        {
            _frameStarted = true;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(EnsureFrame, DispatcherPriority.Background);
        }
    }

    private void Build(Size size)
    {
        _builtWidth = size.Width;
        _builtHeight = size.Height;
        _builtText = Text ?? string.Empty;
        var text = string.IsNullOrWhiteSpace(_builtText) ? "A" : _builtText;

        _cols = Math.Max(24, (int)(size.Width / Math.Max(1.5, Grain)));
        _rows = Math.Max(12, (int)(size.Height / Math.Max(1.5, Grain)));

        var cell = new PixelSize(_cols, _rows);
        var formatted = Sm.Text(text, 240, Brushes.White, FontWeight.Medium, family: Sm.Font(ArcTokens.FontDisplay));
        var geometry = formatted.BuildGeometry(new Point(0, 0))!;
        var bounds = geometry.Bounds;

        _mask = new bool[_cols * _rows];
        if (bounds.Width > 0.5 && bounds.Height > 0.5)
        {
            var scale = Math.Min((_cols - 30) / bounds.Width, (_rows * 0.68) / bounds.Height);
            var offsetX = (_cols - bounds.Width * scale) / 2 - bounds.X * scale;
            var offsetY = _rows * 0.82 - bounds.Height * scale - bounds.Y * scale;
            var visual = new SandMaskVisual(geometry, scale, offsetX, offsetY, new Size(_cols, _rows));
            visual.Measure(new Size(_cols, _rows));
            visual.Arrange(new Rect(0, 0, _cols, _rows));

            using var raster = new RenderTargetBitmap(cell);
            raster.Render(visual);
            var pixels = new uint[_cols * _rows];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                raster.CopyPixels(new PixelRect(0, 0, _cols, _rows), handle.AddrOfPinnedObject(), pixels.Length * 4, _cols * 4);
            }
            finally
            {
                handle.Free();
            }

            for (var index = 0; index < _mask.Length; index++)
            {
                _mask[index] = (pixels[index] >> 24) > 110 || (pixels[index] & 0x00FF0000) > 0x00600000;
            }
        }

        // Fill order: each column fills from the bottom up, with a small left to right stagger.
        _fillAt = new float[_mask.Length];
        for (var x = 0; x < _cols; x++)
        {
            var count = 0;
            for (var y = 0; y < _rows; y++)
            {
                if (_mask[y * _cols + x])
                {
                    count++;
                }
            }

            var rank = 0;
            for (var y = _rows - 1; y >= 0; y--)
            {
                var index = y * _cols + x;
                if (!_mask[index])
                {
                    _fillAt[index] = float.MaxValue;
                    continue;
                }

                var stagger = 0.30 * (x / (double)Math.Max(1, _cols - 1));
                var rise = count <= 1 ? 0.0 : 0.62 * (rank / (double)(count - 1));
                _fillAt[index] = (float)(0.12 + stagger + rise * 0.55 + 0.05 * Sm.Noise(index));
                rank++;
            }
        }

        _dugAt = new double[_mask.Length];
        for (var index = 0; index < _dugAt.Length; index++)
        {
            _dugAt[index] = -1;
        }

        BuildColors(Sm.Color(ArcTokens.Foreground));

        _bitmap?.Dispose();
        _bitmap = new WriteableBitmap(cell, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        _pour = Sm.Reduced ? 2 : 0;
        _grains.Clear();
        _frameStarted = false;
    }

    private void BuildColors(Color ink)
    {
        _builtInk = ink;
        var accent = Sm.Color(ArcTokens.Accent);
        var background = Sm.Color(ArcTokens.Background);
        _colors = new uint[Math.Max(1, _mask.Length)];
        for (var index = 0; index < _colors.Length; index++)
        {
            var seed = Sm.Noise(index * 7 + 11);
            var warmth = Sm.Noise(index * 13 + 5);
            var grain = ArcColor.Mix(ink, accent, 0.16 * warmth);
            grain = ArcColor.Mix(grain, background, 0.10 + 0.16 * seed);
            _colors[index] = Pack(grain);
        }
    }

    private static uint Pack(Color color) =>
        0xFF000000u | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

    private unsafe void Paint()
    {
        if (_bitmap is null)
        {
            return;
        }

        using var buffer = _bitmap.Lock();
        var address = (byte*)buffer.Address;
        for (var y = 0; y < _rows; y++)
        {
            var row = (uint*)(address + y * buffer.RowBytes);
            for (var x = 0; x < _cols; x++)
            {
                var index = y * _cols + x;
                if (!_mask[index] || _pour < _fillAt[index])
                {
                    row[x] = 0;
                    continue;
                }

                var dug = _dugAt[index];
                if (dug >= 0)
                {
                    var heal = 1.15 + 0.35 * Sm.Noise(index * 3 + 7);
                    var alpha = Sm.Clamp01((_now - dug - heal) / 0.30);
                    if (alpha <= 0)
                    {
                        row[x] = 0;
                        continue;
                    }

                    var color = _colors[index];
                    var scale = (uint)(alpha * 255);
                    var r = (uint)((color >> 16) & 0xFF);
                    var g = (uint)((color >> 8) & 0xFF);
                    var b = (uint)(color & 0xFF);
                    row[x] = (scale << 24) | (r * scale / 255 << 16) | (g * scale / 255 << 8) | (b * scale / 255);
                    continue;
                }

                row[x] = _colors[index];
            }
        }
    }

    protected override void OnFrame(double dt)
    {
        _pour += dt;

        for (var index = _grains.Count - 1; index >= 0; index--)
        {
            var grain = _grains[index];
            grain.Vy += 1500 * dt;
            grain.X += grain.Vx * dt;
            grain.Y += grain.Vy * dt;
            grain.Life -= dt;
            if (grain.Y > Bounds.Height - 4)
            {
                grain.Y = Bounds.Height - 4;
                grain.Vy *= -0.24;
                grain.Vx *= 0.7;
            }

            if (grain.Life <= 0 || grain.X < -8 || grain.X > Bounds.Width + 8)
            {
                _grains.RemoveAt(index);
            }
        }

        // A little dust falls through the letters while they are pouring, so the fill reads as sand.
        if (_pour < 1.15 && _grains.Count < 260)
        {
            for (var spawn = 0; spawn < 3; spawn++)
            {
                var seed = (int)(_now * 1000) + spawn * 37 + _grains.Count * 11;
                var noise = Sm.Noise(seed);
                var x = noise * Bounds.Width;
                var column = Math.Clamp((int)(x / Bounds.Width * _cols), 0, _cols - 1);
                var front = Front(column);
                if (front <= 0)
                {
                    continue;
                }

                _grains.Add(new Particle
                {
                    X = x,
                    Y = Math.Max(0, front - 26 - noise * 24),
                    Vx = (Sm.Noise(seed + 3) - 0.5) * 30,
                    Vy = 90 + 120 * Sm.Noise(seed + 9),
                    Life = 0.5,
                    MaxLife = 0.5
                });
            }
        }

        if (_dugCount > 0)
        {
            for (var index = 0; index < _dugAt.Length; index++)
            {
                if (_dugAt[index] >= 0 && _now - _dugAt[index] > 1.15 + 0.35 * Sm.Noise(index * 3 + 7))
                {
                    _dugAt[index] = -1;
                    _dugCount--;
                }
            }
        }

        _now += dt;
    }

    /// <summary>The pixel row the sand in a column has reached, for the pour dust.</summary>
    private double Front(int column)
    {
        var level = 0.0;
        for (var y = _rows - 1; y >= 0; y--)
        {
            var index = y * _cols + column;
            if (_mask[index] && _pour >= _fillAt[index])
            {
                level = y * Grain;
                break;
            }
        }

        return level;
    }

    private void Dig(Point point, Point velocity)
    {
        if (Sm.Reduced || _mask.Length == 0)
        {
            return;
        }

        var radius = DigRadius / Math.Max(1.5, Grain);
        var cx = point.X / Grain;
        var cy = point.Y / Grain;
        var limit = radius * radius;
        var threw = 0;
        for (var y = Math.Max(0, (int)(cy - radius)); y <= Math.Min(_rows - 1, (int)(cy + radius)); y++)
        {
            for (var x = Math.Max(0, (int)(cx - radius)); x <= Math.Min(_cols - 1, (int)(cx + radius)); x++)
            {
                var dx = x - cx;
                var dy = y - cy;
                if (dx * dx + dy * dy > limit)
                {
                    continue;
                }

                var index = y * _cols + x;
                if (!_mask[index] || _pour < _fillAt[index] || _dugAt[index] >= 0)
                {
                    continue;
                }

                _dugAt[index] = _now;
                _dugCount++;
                if (threw < 3 && _grains.Count < 520)
                {
                    var seed = index * 31 + 17;
                    _grains.Add(new Particle
                    {
                        X = x * Grain,
                        Y = y * Grain,
                        Vx = velocity.X * 0.45 + (Sm.Noise(seed) - 0.5) * 220,
                        Vy = velocity.Y * 0.35 - 40 - 120 * Sm.Noise(seed + 1),
                        Life = 1.1,
                        MaxLife = 1.1
                    });
                    threw++;
                }
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Sm.Reduced)
        {
            return;
        }

        _pointerInside = true;
        var point = e.GetPosition(this);
        var velocity = new Point((point.X - _lastPointer.X) * 18, (point.Y - _lastPointer.Y) * 18);
        _lastPointer = point;
        Dig(point, velocity);
        EnsureFrame();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerInside = false;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _bitmap?.Dispose();
        _bitmap = null;
    }

    private sealed class Particle
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double Life;
        public double MaxLife;
    }

    /// <summary>Rasterises the letter shapes once at cell resolution to derive the grain mask.</summary>
    private sealed class SandMaskVisual : Control
    {
        private readonly Geometry _geometry;
        private readonly double _scale;
        private readonly double _offsetX;
        private readonly double _offsetY;

        public SandMaskVisual(Geometry geometry, double scale, double offsetX, double offsetY, Size size)
        {
            _geometry = geometry;
            _scale = scale;
            _offsetX = offsetX;
            _offsetY = offsetY;
            Width = size.Width;
            Height = size.Height;
        }

        public override void Render(DrawingContext context)
        {
            using (context.PushTransform(Matrix.CreateScale(_scale, _scale) * Matrix.CreateTranslation(_offsetX, _offsetY)))
            {
                context.DrawGeometry(Brushes.White, null, _geometry);
            }
        }
    }
}
