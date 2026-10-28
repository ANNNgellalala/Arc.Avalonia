using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data.Controls;

/// <summary>One payment origin on the globe.</summary>
internal sealed class GlobeCity
{
    public GlobeCity(string name, string region, double latitude, double longitude, double weight)
    {
        Name = name;
        Region = region;
        Latitude = latitude;
        Longitude = longitude;
        Weight = weight;
    }

    public string Name { get; }

    public string Region { get; }

    public double Latitude { get; }

    public double Longitude { get; }

    public double Weight { get; }
}

/// <summary>
/// A stylised dotted globe drawn in two dimensions: a latitude and longitude dot grid on an orthographic
/// projection, payment arcs from each city to headquarters, and a replay clock that reveals one hour of
/// payments at a time. Drag to spin, click a city to select it, Escape clears the selection. There is no
/// WebGL, no texture and no real geography beyond a coordinate grid; the notion of "live" is the clock.
/// </summary>
internal sealed class ArcDotGlobe : DataStage
{
    private readonly IReadOnlyList<GlobeCity> _cities;
    private readonly double _hqLatitude;
    private readonly double _hqLongitude;
    private double _yaw = 0.27;
    private double _dragging;
    private double _dragOrigin;
    private int _hour;
    private string? _selected;
    private string? _hover;
    private readonly List<(string Name, Point Point)> _hits = new();

    public ArcDotGlobe(IReadOnlyList<GlobeCity> cities, int hour, double hqLatitude = 30.27,
        double hqLongitude = -97.74)
    {
        _cities = cities;
        _hour = hour;
        _hqLatitude = hqLatitude;
        _hqLongitude = hqLongitude;
        Height = 330;
    }

    public int Hour
    {
        get => _hour;
        set
        {
            var next = Math.Clamp(value, 0, 23);
            if (next == _hour)
            {
                return;
            }

            _hour = next;
            InvalidateVisual();
        }
    }

    public string? Selected => _selected;

    public event EventHandler<string?>? SelectionChanged;

    protected override double DefaultHeight => 330;

    private Point Center => new(Bounds.Width / 2, Bounds.Height / 2);

    private double Radius => Math.Min(Bounds.Width, Bounds.Height) * 0.46;

    private (Point Point, double Depth) Project(double latitude, double longitude)
    {
        var lat = latitude * Math.PI / 180;
        var lon = (longitude + _yaw * 360) * Math.PI / 180;
        var x = Math.Cos(lat) * Math.Sin(lon);
        var y = -Math.Sin(lat);
        var z = Math.Cos(lat) * Math.Cos(lon);
        var center = Center;
        return (new Point(center.X + x * Radius, center.Y + y * Radius), z);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        Focus();
        var hit = _hits.FirstOrDefault(item =>
            Math.Sqrt(Math.Pow(item.Point.X - point.X, 2) + Math.Pow(item.Point.Y - point.Y, 2)) < 12);
        if (hit.Name is { Length: > 0 })
        {
            Select(hit.Name);
            return;
        }

        _dragging = 1;
        _dragOrigin = point.X;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_dragging > 0)
        {
            _yaw -= (point.X - _dragOrigin) / Math.Max(1, Radius) * 1.4;
            _dragOrigin = point.X;
            InvalidateVisual();
            return;
        }

        var hover = _hits.FirstOrDefault(item =>
            Math.Sqrt(Math.Pow(item.Point.X - point.X, 2) + Math.Pow(item.Point.Y - point.Y, 2)) < 12).Name;
        if (hover != _hover)
        {
            _hover = hover;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = 0;
        e.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Left:
                _yaw -= 0.05;
                break;
            case Key.Right:
                _yaw += 0.05;
                break;
            case Key.Up:
                Hour += 1;
                return;
            case Key.Down:
                Hour -= 1;
                return;
            case Key.Home:
                Hour = 0;
                return;
            case Key.End:
                Hour = 23;
                return;
            case Key.Escape:
                Select(null);
                return;
            default:
                return;
        }

        InvalidateVisual();
        e.Handled = true;
    }

    private void Select(string? name)
    {
        if (name == _selected)
        {
            return;
        }

        _selected = name;
        SelectionChanged?.Invoke(this, name);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var center = Center;
        var radius = Radius;
        if (radius < 20)
        {
            return;
        }

        context.DrawEllipse(Ink.Alpha(Ink.SurfaceMuted, 0.8), Ink.Pen(Ink.Border, 1), center, radius, radius);
        context.DrawEllipse(null, Ink.Pen(Ink.BorderSubtle, 1), center, radius * 1.06, radius * 1.06);

        // Dot grid: a latitude and longitude lattice, drawn only on the visible hemisphere.
        for (var lat = -78.0; lat <= 78; lat += 6)
        {
            for (var lon = -180.0; lon < 180; lon += 6)
            {
                var (point, depth) = Project(lat, lon);
                if (depth <= 0.02)
                {
                    continue;
                }

                var alpha = 0.16 + depth * 0.30;
                context.DrawEllipse(Ink.Alpha(Ink.Secondary, alpha), null, point, 1.1, 1.1);
            }
        }

        // The replay clock reveals payments up to the current hour.
        var visible = _cities.Where(city => city.Weight > 0).ToList();
        _hits.Clear();
        var globeClip = new RoundedRect(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2), radius, radius);
        var clip = context.PushClip(globeClip);
        foreach (var city in visible)
        {
            var (start, startDepth) = Project(city.Latitude, city.Longitude);
            var (hq, hqDepth) = Project(_hqLatitude, _hqLongitude);
            if (startDepth <= 0.12 || hqDepth <= 0.12)
            {
                continue;
            }

            var selected = city.Name == _selected || city.Name == _hover;
            var localHour = (_hour + city.Longitude / 15 + 24) % 24;
            var shown = localHour >= 8 && localHour <= 20;
            if (!shown)
            {
                continue;
            }

            // Arc: a quadratic curve lifted away from the centre so it reads as an air route.
            var mid = new Point((start.X + hq.X) / 2, (start.Y + hq.Y) / 2);
            var lift = new Vector(mid.X - center.X, mid.Y - center.Y);
            var length = Math.Max(1, Math.Sqrt(lift.X * lift.X + lift.Y * lift.Y));
            var control = new Point(
                center.X + lift.X / length * radius * 1.12,
                center.Y + lift.Y / length * radius * 1.12);
            var curve = new StreamGeometry();
            using (var geometry = curve.Open())
            {
                geometry.BeginFigure(start, false);
                geometry.QuadraticBezierTo(control, hq);
                geometry.EndFigure(false);
            }

            var brush = selected ? Ink.Series(0) : Ink.Alpha(Ink.Series(1), 0.5);
            context.DrawGeometry(null, Ink.Pen(brush, selected ? 1.8 : 1, !selected), curve);
            context.DrawEllipse(selected ? Ink.Series(0) : Ink.Alpha(Ink.Series(1), 0.7), null, start,
                selected ? 3.6 : 2.4, selected ? 3.6 : 2.4);
            _hits.Add((city.Name, start));
        }

        clip.Dispose();

        var (hqPoint, hqVisible) = Project(_hqLatitude, _hqLongitude);
        if (hqVisible > 0)
        {
            context.DrawEllipse(Ink.Accent, Ink.Pen(Ink.Surface, 1.5), hqPoint, 4.2, 4.2);
            context.DrawEllipse(null, Ink.Pen(Ink.Alpha(Ink.Accent, 0.4), 1), hqPoint, 8.5, 8.5);
            Text(context, "HQ", hqPoint.X + 10, hqPoint.Y - 6, 10, Ink.Accent, FontWeight.Medium);
        }

        if (_hover is { Length: > 0 })
        {
            var city = visible.FirstOrDefault(item => item.Name == _hover);
            if (city is not null)
            {
                var (point, _) = Project(city.Latitude, city.Longitude);
                Tooltip(context, Bounds.Size, point, city.Name, new[]
                {
                    ("Region", city.Region, (IBrush?)null),
                    ("Share", DataKit.Percent(city.Weight), (IBrush?)null)
                });
            }
        }
    }
}
