using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Blocks.Data;

/// <summary>One account in the galaxy, kept deliberately small so 1,200 of them stay cheap.</summary>
internal sealed class GalaxyAccount
{
    public GalaxyAccount(string name, string plan, string region, string health, double mrr, double jitter)
    {
        Name = name;
        Plan = plan;
        Region = region;
        Health = health;
        Mrr = mrr;
        Jitter = jitter;
    }

    public string Name { get; }

    public string Plan { get; }

    public string Region { get; }

    public string Health { get; }

    public double Mrr { get; }

    /// <summary>A stable 0..1 offset, so a cluster reads as a scattered cloud rather than a grid.</summary>
    public double Jitter { get; }

    public string GroupKey(string grouping) => grouping switch
    {
        "region" => Region,
        "health" => Health,
        _ => Plan
    };
}

/// <summary>
/// A canvas galaxy of 1,200 accounts. Accounts spiral into clusters by plan, region or health, sized by MRR,
/// with a dimmed outside when a cluster is selected. Points are seeded, so the cloud is identical on every
/// run. This is a 2D canvas scatter, not a physics engine: changing the grouping lays the clusters out
/// again instantly (the reference springs them into place, which reduced motion also skips).
/// </summary>
internal sealed class ArcGalaxyStage : DataStage
{
    private readonly IReadOnlyList<GalaxyAccount> _accounts;
    private readonly List<(GalaxyAccount Account, Point Point, double Radius)> _points = new();
    private string _grouping = "plan";
    private string? _selectedCluster;
    private GalaxyAccount? _hover;
    private double _zoom = 1;
    private double _panX;
    private double _panY;
    private Point _dragOrigin;
    private bool _dragging;

    public ArcGalaxyStage(IReadOnlyList<GalaxyAccount> accounts)
    {
        _accounts = accounts;
        Height = 340;
    }

    public string Grouping
    {
        get => _grouping;
        set
        {
            if (value == _grouping)
            {
                return;
            }

            _grouping = value;
            _selectedCluster = null;
            _hover = null;
            InvalidateVisual();
        }
    }

    public string? SelectedCluster => _selectedCluster;

    public event EventHandler<string?>? ClusterSelected;

    protected override double DefaultHeight => 340;

    private Point Center => new(Bounds.Width / 2, Bounds.Height / 2);

    private double Radius => Math.Min(Bounds.Width, Bounds.Height) * 0.42;

    private IReadOnlyList<(string Name, double X, double Y)> Clusters()
    {
        var keys = _accounts.Select(account => account.GroupKey(_grouping)).Distinct().OrderBy(key => key).ToList();
        var result = new List<(string, double, double)>();
        for (var index = 0; index < keys.Count; index++)
        {
            var angle = index / (double)Math.Max(1, keys.Count) * Math.PI * 2 - Math.PI / 2;
            var ring = keys.Count <= 1 ? 0 : 0.5;
            result.Add((keys[index], Math.Cos(angle) * ring, Math.Sin(angle) * ring));
        }

        return result;
    }

    private void Layout()
    {
        _points.Clear();
        var clusters = Clusters();
        var center = Center;
        var radius = Radius * _zoom;
        var lookup = clusters.ToDictionary(cluster => cluster.Name, cluster => cluster);
        foreach (var account in _accounts)
        {
            if (!lookup.TryGetValue(account.GroupKey(_grouping), out var cluster))
            {
                continue;
            }

            var spread = 0.19 * radius;
            var angle = account.Jitter * Math.PI * 2 * 3.1;
            var distance = account.Jitter * spread;
            var x = center.X + _panX + cluster.X * radius + Math.Cos(angle) * distance;
            var y = center.Y + _panY + cluster.Y * radius + Math.Sin(angle) * distance * 0.82;
            var pointRadius = 1.1 + Math.Min(3.2, account.Mrr / 9_000);
            _points.Add((account, new Point(x, y), pointRadius));
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        if (_dragging)
        {
            _panX += position.X - _dragOrigin.X;
            _panY += position.Y - _dragOrigin.Y;
            _dragOrigin = position;
            InvalidateVisual();
            return;
        }

        Layout();
        GalaxyAccount? nearest = null;
        var best = 10.0;
        foreach (var point in _points)
        {
            if (_selectedCluster is not null && point.Account.GroupKey(_grouping) != _selectedCluster)
            {
                continue;
            }

            var distance = Math.Sqrt(Math.Pow(point.Point.X - position.X, 2) + Math.Pow(point.Point.Y - position.Y, 2));
            if (distance < best)
            {
                best = distance;
                nearest = point.Account;
            }
        }

        if (!ReferenceEquals(nearest, _hover))
        {
            _hover = nearest;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var position = e.GetPosition(this);
        Layout();
        if (_hover is not null)
        {
            SelectCluster(_hover.GroupKey(_grouping));
            return;
        }

        _dragging = true;
        _dragOrigin = position;
        Focus();
        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var clusters = Clusters();
        var index = _selectedCluster is null
            ? -1
            : clusters.ToList().FindIndex(cluster => cluster.Name == _selectedCluster);
        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
                index = index <= 0 ? clusters.Count - 1 : index - 1;
                break;
            case Key.Right:
            case Key.Down:
                index = index + 1 >= clusters.Count ? 0 : index + 1;
                break;
            case Key.Add:
            case Key.OemPlus:
                _zoom = Math.Min(2.2, _zoom + 0.12);
                InvalidateVisual();
                e.Handled = true;
                return;
            case Key.Subtract:
            case Key.OemMinus:
                _zoom = Math.Max(0.7, _zoom - 0.12);
                InvalidateVisual();
                e.Handled = true;
                return;
            case Key.Escape:
                _panX = 0;
                _panY = 0;
                _zoom = 1;
                SelectCluster(null);
                return;
            case Key.Home:
                _panX = 0;
                _panY = 0;
                _zoom = 1;
                SelectCluster(null);
                return;
            default:
                return;
        }

        SelectCluster(clusters[index].Name);
        e.Handled = true;
    }

    private void SelectCluster(string? name)
    {
        if (name == _selectedCluster)
        {
            return;
        }

        _selectedCluster = name;
        ClusterSelected?.Invoke(this, name);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        Layout();
        var clusters = Clusters();
        var center = Center;
        var radius = Radius * _zoom;

        // Orbit rings: a quiet frame for the clusters.
        context.DrawEllipse(null, Ink.Pen(Ink.BorderSubtle, 1), new Point(center.X + _panX, center.Y + _panY),
            radius * 0.62, radius * 0.62);
        context.DrawEllipse(null, Ink.Pen(Ink.BorderSubtle, 1, true), new Point(center.X + _panX, center.Y + _panY),
            radius, radius);

        var dim = _selectedCluster is not null;
        foreach (var point in _points)
        {
            var cluster = point.Account.GroupKey(_grouping);
            var isSelected = _selectedCluster == cluster;
            if (dim && !isSelected)
            {
                context.DrawEllipse(Ink.Alpha(Ink.Muted, 0.20), null, point.Point, point.Radius * 0.8, point.Radius * 0.8);
                continue;
            }

            var brush = point.Account.Health switch
            {
                "At risk" => Ink.Danger,
                "Watch" => Ink.Warning,
                _ => Ink.Series(0)
            };
            var alpha = ReferenceEquals(point.Account, _hover) ? 1 : dim ? 0.85 : 0.62;
            context.DrawEllipse(Ink.Alpha(brush, alpha), null, point.Point, point.Radius, point.Radius);
        }

        foreach (var cluster in clusters)
        {
            var x = center.X + _panX + cluster.X * radius;
            var y = center.Y + _panY + cluster.Y * radius;
            var label = cluster.Name;
            var amount = _accounts.Where(account => account.GroupKey(_grouping) == cluster.Name).Sum(account => account.Mrr);
            Text(context, label, x, y - 26, 11, Ink.Secondary, FontWeight.Medium, TextAlignment.Center);
            Text(context, DataKit.Compact(amount), x, y - 14, 10, Ink.Muted, alignment: TextAlignment.Center);
        }

        if (_hover is not null)
        {
            var point = _points.First(item => ReferenceEquals(item.Account, _hover)).Point;
            Tooltip(context, Bounds.Size, point, _hover.Name, new[]
            {
                ("Plan", _hover.Plan, (IBrush?)null),
                ("Region", _hover.Region, (IBrush?)null),
                ("MRR", DataKit.Money(_hover.Mrr), (IBrush?)null),
                ("Health", _hover.Health, (IBrush?)null)
            });
        }

        Text(context, $"{_accounts.Count:N0} accounts · zoom {_zoom:0.0}×", 8, Bounds.Height - 12, 10, Ink.Muted);
    }
}
