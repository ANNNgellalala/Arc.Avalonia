using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Charts;

/// <summary>
/// A hierarchy in concentric rings: each node is an arc sized by its value, and its children share the
/// parent's angular span one ring further out. Hovering highlights the whole branch path (ancestors and
/// descendants) and dims the rest; clicking an arc drills into it, with a breadcrumb and a Back pill to
/// return. The centre readout follows the pointer and the focus.
/// </summary>
/// <remarks>
/// Approximation: Arc Pro sets ring labels along each arc's curve. Avalonia has no text-on-path here, so
/// labels are set horizontally at the arc's centroid and only where the slice can hold them. Zooming
/// swaps the layout rather than interpolating every angle on one spring; the readout and the arc pop
/// carry the motion instead, which is what reads at card size.
/// </remarks>
public class ArcSunburstChart : ArcChartBase
{
    public static readonly StyledProperty<ArcSunburstNode?> RootProperty =
        AvaloniaProperty.Register<ArcSunburstChart, ArcSunburstNode?>(nameof(Root));

    public static readonly StyledProperty<string> CenterLabelProperty =
        AvaloniaProperty.Register<ArcSunburstChart, string>(nameof(CenterLabel), "Total");

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<ArcSunburstChart, bool>(nameof(ShowLabels), true);

    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<ArcSunburstChart, bool>(nameof(ShowLegend), true);

    public static readonly StyledProperty<int> DepthProperty =
        AvaloniaProperty.Register<ArcSunburstChart, int>(nameof(Depth), 3);

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcSunburstChart, Func<double, string>?>(nameof(ValueFormat));

    private const double RailHeight = 24;
    private const double LegendRowHeight = 22;
    private const int MaxLegendRows = 6;

    private readonly List<ArcItem> _arcs = new();
    private readonly List<(Rect Bounds, ArcSunburstNode Node)> _legendHits = new();
    private readonly Dictionary<ArcSunburstNode, ArcSunburstNode?> _parentOf = new();
    private readonly HashSet<ArcSunburstNode> _activeAncestors = new();
    private Point _center;
    private double _holeRadius;
    private double _outerRadius;
    private double _ringWidth;
    private Rect _backHit;
    private ArcSunburstNode? _focus;
    private ArcSunburstNode? _active;
    private bool _keyboardFocus;
    private bool _backHover;

    static ArcSunburstChart()
    {
        AffectsRender<ArcSunburstChart>(
            RootProperty,
            CenterLabelProperty,
            ShowLabelsProperty,
            ShowLegendProperty,
            DepthProperty,
            ValueFormatProperty);
        RootProperty.Changed.AddClassHandler<ArcSunburstChart>((chart, _) => chart.ResetInteraction());
    }

    public ArcSunburstChart()
    {
        Focusable = true;
    }

    /// <summary>The root of the hierarchy. A branch node's value is the sum of its children.</summary>
    public ArcSunburstNode? Root
    {
        get => GetValue(RootProperty);
        set => SetValue(RootProperty, value);
    }

    /// <summary>Caption above the centre value at rest, such as "Cloud spend".</summary>
    public string CenterLabel
    {
        get => GetValue(CenterLabelProperty);
        set => SetValue(CenterLabelProperty, value);
    }

    /// <summary>Draws node names on the arcs where the slice has room for them.</summary>
    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    /// <summary>Lists the children of the centre with their shares when the chart is wide enough.</summary>
    public bool ShowLegend
    {
        get => GetValue(ShowLegendProperty);
        set => SetValue(ShowLegendProperty, value);
    }

    /// <summary>Rings shown around the centre at once. A shallower branch fills the radius.</summary>
    public int Depth
    {
        get => GetValue(DepthProperty);
        set => SetValue(DepthProperty, value);
    }

    /// <summary>Formats the centre value, the legend values, and the tooltip.</summary>
    public Func<double, string>? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    /// <summary>Raised when an arc, a legend row, the centre, or Back changes the focus or selects a leaf.</summary>
    public event EventHandler<ArcSunburstSelection>? NodeSelected;

    protected override double DefaultHeight => 244;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);

        var overBack = _focus is not null && !ReferenceEquals(_focus, Root) && _backHit.Contains(point);
        if (overBack != _backHover)
        {
            _backHover = overBack;
            InvalidateVisual();
        }

        if (overBack)
        {
            SetActive(null);
            return;
        }

        foreach (var hit in _legendHits)
        {
            if (hit.Bounds.Contains(point))
            {
                SetActive(hit.Node);
                return;
            }
        }

        SetActive(NodeAt(point));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _backHover = false;
        SetActive(null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (_keyboardFocus)
        {
            _keyboardFocus = false;
            InvalidateVisual();
        }

        var root = Root;
        if (root is null)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (!ReferenceEquals(_focus, root) && _backHit.Contains(point))
        {
            ZoomOut();
            e.Handled = true;
            return;
        }

        foreach (var hit in _legendHits)
        {
            if (hit.Bounds.Contains(point))
            {
                ZoomTo(hit.Node);
                e.Handled = true;
                return;
            }
        }

        if (Distance(point) < _holeRadius)
        {
            ZoomOut();
            e.Handled = true;
            return;
        }

        if (NodeAt(point) is { } node)
        {
            ZoomTo(node);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var root = Root;
        if (root is null)
        {
            return;
        }

        if (!_keyboardFocus)
        {
            _keyboardFocus = true;
            InvalidateVisual();
        }

        switch (e.Key)
        {
            case Key.Right:
                MoveSibling(1);
                break;
            case Key.Left:
                MoveSibling(-1);
                break;
            case Key.Down:
                MoveDeeper();
                break;
            case Key.Up:
                MoveShallower();
                break;
            case Key.Enter:
            case Key.Space:
                if (_active is { } active)
                {
                    ZoomTo(active);
                }

                break;
            case Key.Escape:
            case Key.Back:
                if (_active is not null)
                {
                    SetActive(null);
                }
                else
                {
                    ZoomOut();
                }

                break;
            case Key.Home:
                ZoomOut(true);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _arcs.Clear();
        _legendHits.Clear();
        _parentOf.Clear();

        var root = Root;
        if (root is null || root.Total <= 0)
        {
            ChartText.Draw(
                context,
                "No data",
                Bounds.Width / 2,
                Bounds.Height / 2,
                13,
                ChartPalette.Muted,
                alignment: TextAlignment.Center);
            return;
        }

        if (_focus is null || (!ReferenceEquals(_focus, root) && !Contains(root, _focus)))
        {
            _focus = root;
        }

        var focus = _focus!;
        var total = focus.Total;
        var showLegend = ShowLegend && Bounds.Width >= 380 && focus.Children.Count > 0;
        var legendWidth = showLegend ? Math.Min(184, Bounds.Width * 0.36) : 0;
        var ringTop = RailHeight + 6;
        var ringHeight = Math.Max(0, Bounds.Height - ringTop - 4);
        var diameter = Math.Max(48, Math.Min(Bounds.Width - legendWidth - 20, ringHeight));
        _outerRadius = diameter / 2;
        var levels = focus.Children.Count == 0 ? 0 : Math.Max(1, Math.Min(Depth, focus.Height));
        levels = Math.Min(levels, (int)Math.Max(1, (_outerRadius * 0.66) / 15));
        _holeRadius = levels == 0
            ? Math.Max(24, _outerRadius * 0.62)
            : Math.Clamp(_outerRadius * 0.42, 30, _outerRadius - levels * 14);
        _ringWidth = levels == 0 ? 0 : (_outerRadius - _holeRadius) / levels;
        _center = new Point(
            showLegend ? 8 + _outerRadius : Bounds.Width / 2,
            ringTop + ringHeight / 2);

        var path = PathTo(root, focus);
        BuildArcs(focus, path, 1, levels, 0, 360);

        DrawBreadcrumb(context, path);
        DrawBackPill(context, focus, root);

        if (levels == 0)
        {
            // A leaf root has no rings; keep a quiet ring so the readout still reads as a chart.
            context.DrawGeometry(
                ChartPalette.Alpha(ChartPalette.Series(0), 0.9),
                new Pen(ChartPalette.Surface, 1),
                ChartGeometry.DonutSegment(_center, _outerRadius, _holeRadius, -90, 359.9));
        }

        // Rings are painted one level at a time so a deeper ring never covers a shallower arc, then the
        // labels go on last as their own pass.
        foreach (var level in Enumerable.Range(1, Math.Max(1, levels)))
        {
            foreach (var item in _arcs)
            {
                if (item.Level != level)
                {
                    continue;
                }

                var inner = _holeRadius + (item.Level - 1) * _ringWidth;
                var itemOuter = Math.Min(_outerRadius, inner + _ringWidth);
                var gap = Math.Min(1.1, item.Sweep * 0.12);
                var start = item.Start + gap / 2;
                var sweep = item.Sweep - gap;
                if (sweep <= 0.05)
                {
                    continue;
                }

                var brush = item.Brush;
                var isActive = ReferenceEquals(item.Node, _active);
                if (_active is not null && !IsInBranch(item))
                {
                    brush = ChartPalette.Alpha(brush, 0.22);
                }

                var origin = _center;
                if (isActive)
                {
                    var middle = (item.Start + item.Sweep / 2 - 90) * Math.PI / 180;
                    origin = new Point(_center.X + Math.Cos(middle) * 2, _center.Y + Math.Sin(middle) * 2);
                }

                context.DrawGeometry(
                    brush,
                    new Pen(ChartPalette.Surface, 1),
                    ChartGeometry.DonutSegment(origin, itemOuter, inner, start - 90, sweep));
            }
        }

        if (ShowLabels && _arcs.Count > 0)
        {
            // Labels are set along the arc's tangent, so a narrow slice can still hold its name. Only leaves
            // are labelled: branches would sit a few pixels from their children and the two would collide.
            foreach (var item in _arcs)
            {
                if (item.Node.Children.Count > 0)
                {
                    continue;
                }

                var inner = _holeRadius + (item.Level - 1) * _ringWidth;
                var sweep = item.Sweep - Math.Min(1.1, item.Sweep * 0.12);
                DrawArcLabel(context, item, inner, item.Brush, sweep);
            }
        }

        DrawCenter(context, root, total);

        if (showLegend)
        {
            DrawLegend(context, focus, total);
        }

        if (_keyboardFocus && IsFocused)
        {
            context.DrawRectangle(
                null,
                new Pen(ArcResources.Brush(ArcTokens.FocusOutline), 2),
                new RoundedRect(
                    new Rect(1.5, 1.5, Math.Max(0, Bounds.Width - 3), Math.Max(0, Bounds.Height - 3)),
                    12));
        }
    }

    private void DrawArcLabel(DrawingContext context, ArcItem item, double inner, IBrush brush, double sweep)
    {
        if (sweep < 10 || _ringWidth < 15)
        {
            return;
        }

        var size = item.Level == 1 ? 11 : 10;
        var mid = inner + _ringWidth / 2;
        var along = Math.PI * mid * sweep / 180 - 6;
        if (along < 20)
        {
            return;
        }

        var natural = ChartText.Width(item.Node.Label, size, FontWeight.Medium);
        if (natural > along)
        {
            return;
        }

        var angle = (item.Start + item.Sweep / 2 - 90) * Math.PI / 180;
        var at = new Point(_center.X + Math.Cos(angle) * mid, _center.Y + Math.Sin(angle) * mid);

        // Follow the tangent, flipped where needed so the text never reads upside down.
        var degrees = angle * 180 / Math.PI + 90;
        while (degrees > 90)
        {
            degrees -= 180;
        }

        while (degrees < -90)
        {
            degrees += 180;
        }

        var rotation = degrees * Math.PI / 180;
        using (context.PushTransform(
            Matrix.CreateRotation(rotation) * Matrix.CreateTranslation(at.X, at.Y)))
        {
            ChartText.Draw(
                context,
                item.Node.Label,
                0,
                0,
                size,
                AdvancedChartInk.OnFill(brush),
                FontWeight.Medium,
                TextAlignment.Center);
        }
    }

    private void BuildArcs(
        ArcSunburstNode parent,
        IReadOnlyList<ArcSunburstNode> path,
        int level,
        int levels,
        double start,
        double sweep)
    {
        if (level > levels || parent.Children.Count == 0)
        {
            return;
        }

        var sum = parent.Children.Sum(child => child.Total);
        if (sum <= 0)
        {
            return;
        }

        var angle = start;
        foreach (var child in parent.Children)
        {
            var childSweep = sweep * (child.Total / sum);
            var childPath = new List<ArcSunburstNode>(path) { child };
            _parentOf[child] = parent;
            if (childSweep > 0.06 && child.Total > 0)
            {
                _arcs.Add(new ArcItem(child, level, angle, childSweep, childPath, BrushFor(childPath)));
                BuildArcs(child, childPath, level + 1, levels, angle, childSweep);
            }

            angle += childSweep;
        }
    }

    private IBrush BrushFor(IReadOnlyList<ArcSunburstNode> path)
    {
        var root = Root!;
        var top = path.Count > 1 ? path[1] : path[0];
        var index = IndexOf(root.Children, top);
        if (index < 0)
        {
            index = 0;
        }

        var baseBrush = ChartPalette.Series(index);
        return AdvancedChartInk.Step(baseBrush, path.Count - 1);
    }

    private void DrawCenter(DrawingContext context, ArcSunburstNode root, double total)
    {
        var radius = Math.Max(10, _holeRadius - 2);
        context.DrawEllipse(ChartPalette.Surface, new Pen(ChartPalette.BorderSubtle, 1), _center, radius, radius);

        var active = _active;
        var label = active?.Label ?? CenterLabel;
        var value = active?.Total ?? total;
        var detail = active is not null
            ? DetailFor(active, root)
            : ReferenceEquals(_focus, root)
                ? $"{root.Children.Count} groups"
                : ChartFormat.Percent(total / Math.Max(1, root.Total), 0) + " of total";

        var textWidth = Math.Max(30, radius * 1.95);
        ChartText.Draw(
            context,
            label,
            _center.X,
            _center.Y - 19,
            11,
            ChartPalette.Muted,
            alignment: TextAlignment.Center,
            maxWidth: textWidth);
        ChartText.Draw(
            context,
            FormatValue(value),
            _center.X,
            _center.Y,
            18,
            ChartPalette.Foreground,
            FontWeight.Medium,
            TextAlignment.Center,
            maxWidth: textWidth);
        ChartText.Draw(
            context,
            detail,
            _center.X,
            _center.Y + 18,
            10,
            ChartPalette.Muted,
            alignment: TextAlignment.Center,
            maxWidth: textWidth);
    }

    private string DetailFor(ArcSunburstNode node, ArcSunburstNode root)
    {
        var parent = _parentOf.TryGetValue(node, out var found) ? found : null;
        var share = node.Total / Math.Max(1, parent?.Total ?? root.Total);
        var name = parent?.Label ?? root.Label;
        return ChartFormat.Percent(share, 0) + " of " + name;
    }

    private void DrawLegend(DrawingContext context, ArcSunburstNode focus, double total)
    {
        var rows = Math.Min(MaxLegendRows, focus.Children.Count);
        var x = _center.X + _outerRadius + 16;
        var width = Math.Max(60, Bounds.Width - x - 6);
        var y = _center.Y - (rows * LegendRowHeight) / 2 + 1;
        var prefix = PathTo(Root!, focus);
        for (var index = 0; index < rows; index++)
        {
            var child = focus.Children[index];
            var row = new Rect(x - 4, y, width + 4, LegendRowHeight);
            var active = ReferenceEquals(child, _active);
            if (active)
            {
                context.DrawRectangle(ChartPalette.Alpha(ChartPalette.BorderSubtle, 0.7), null, row, 6, 6);
            }

            var childPath = new List<ArcSunburstNode>(prefix) { child };
            context.DrawRectangle(BrushFor(childPath), null, new Rect(x, y + 7, 8, 8), 2, 2);
            var name = ChartText.Create(
                child.Label,
                12,
                active ? ChartPalette.Foreground : ChartPalette.Secondary,
                active ? FontWeight.Medium : FontWeight.Normal,
                Math.Max(30, width - 58));
            context.DrawText(name, new Point(x + 14, y + 11 - name.Height / 2));
            ChartText.Draw(
                context,
                ChartFormat.Percent(child.Total / Math.Max(1, total), 0),
                x + width,
                y + 11,
                11,
                ChartPalette.Muted,
                alignment: TextAlignment.Right);
            _legendHits.Add((row, child));
            y += LegendRowHeight;
        }

        if (focus.Children.Count > rows)
        {
            ChartText.Draw(
                context,
                $"+{focus.Children.Count - rows} more",
                x,
                y + 8,
                11,
                ChartPalette.Muted);
        }
    }

    private void DrawBreadcrumb(DrawingContext context, IReadOnlyList<ArcSunburstNode> path)
    {
        var centerY = RailHeight / 2 + 1;
        var limit = Math.Max(80, Bounds.Width - (ReferenceEquals(_focus, Root) ? 8 : 68));
        var x = 2.0;
        for (var index = 0; index < path.Count; index++)
        {
            if (index > 0)
            {
                var separator = ChartText.Create(" / ", 11, ChartPalette.Muted);
                if (x + separator.Width > limit)
                {
                    return;
                }

                context.DrawText(separator, new Point(x, centerY - separator.Height / 2));
                x += separator.Width;
            }

            var current = index == path.Count - 1;
            var text = ChartText.Create(
                path[index].Label,
                11,
                current ? ChartPalette.Foreground : ChartPalette.Muted,
                current ? FontWeight.Medium : FontWeight.Normal,
                Math.Max(20, limit - x));
            if (x + text.Width > limit - 1)
            {
                return;
            }

            context.DrawText(text, new Point(x, centerY - text.Height / 2));
            x += text.Width;
        }
    }

    private void DrawBackPill(DrawingContext context, ArcSunburstNode focus, ArcSunburstNode root)
    {
        _backHit = default;
        if (ReferenceEquals(focus, root))
        {
            return;
        }

        var rect = new Rect(Bounds.Width - 60, 1, 56, 21);
        _backHit = rect;
        context.DrawRectangle(
            _backHover ? ChartPalette.Alpha(ChartPalette.BorderSubtle, 0.85) : ChartPalette.Surface,
            new Pen(ChartPalette.Border, 1),
            new RoundedRect(rect, 10.5));
        var pen = new Pen(ChartPalette.Secondary, 1.5);
        var midY = rect.Center.Y;
        context.DrawLine(pen, new Point(rect.X + 13, midY - 3.4), new Point(rect.X + 9.8, midY));
        context.DrawLine(pen, new Point(rect.X + 9.8, midY), new Point(rect.X + 13, midY + 3.4));
        ChartText.Draw(context, "Back", rect.X + 20, midY, 11, ChartPalette.Secondary);
    }

    private ArcSunburstNode? NodeAt(Point point)
    {
        if (_arcs.Count == 0)
        {
            return null;
        }

        var distance = Distance(point);
        if (distance < _holeRadius || distance > _outerRadius + 2)
        {
            return null;
        }

        var levels = _arcs.Max(item => item.Level);
        var level = Math.Clamp((int)((distance - _holeRadius) / Math.Max(1, _ringWidth)) + 1, 1, levels);
        var angle = Math.Atan2(point.Y - _center.Y, point.X - _center.X) * 180 / Math.PI + 90;
        if (angle < 0)
        {
            angle += 360;
        }

        foreach (var item in _arcs)
        {
            if (item.Level == level && angle >= item.Start && angle <= item.Start + item.Sweep)
            {
                return item.Node;
            }
        }

        return null;
    }

    private double Distance(Point point)
        => Math.Sqrt((point.X - _center.X) * (point.X - _center.X) + (point.Y - _center.Y) * (point.Y - _center.Y));

    private bool IsInBranch(ArcItem item)
    {
        var active = _active;
        if (active is null)
        {
            return true;
        }

        return ReferenceEquals(item.Node, active) || item.Path.Contains(active) || _activeAncestors.Contains(item.Node);
    }

    private void SetActive(ArcSunburstNode? node)
    {
        if (ReferenceEquals(_active, node))
        {
            return;
        }

        _active = node;
        _activeAncestors.Clear();
        if (node is not null)
        {
            var item = _arcs.FirstOrDefault(candidate => ReferenceEquals(candidate.Node, node));
            if (item is not null)
            {
                foreach (var ancestor in item.Path)
                {
                    if (!ReferenceEquals(ancestor, node))
                    {
                        _activeAncestors.Add(ancestor);
                    }
                }
            }
        }

        InvalidateVisual();
    }

    private void MoveSibling(int direction)
    {
        if (_arcs.Count == 0)
        {
            return;
        }

        if (_active is null)
        {
            SetActive(_arcs[0].Node);
            return;
        }

        var item = _arcs.FirstOrDefault(candidate => ReferenceEquals(candidate.Node, _active));
        if (item is null)
        {
            SetActive(_arcs[0].Node);
            return;
        }

        var siblings = _arcs
            .Where(candidate => candidate.Level == item.Level && ReferenceEquals(candidate.Parent, item.Parent))
            .ToList();
        if (siblings.Count <= 1)
        {
            MoveDeeper();
            return;
        }

        var index = siblings.FindIndex(candidate => ReferenceEquals(candidate.Node, _active));
        index = (index + direction + siblings.Count) % siblings.Count;
        SetActive(siblings[index].Node);
    }

    private void MoveDeeper()
    {
        var node = _active ?? _focus;
        if (node is null || node.Children.Count == 0)
        {
            if (_arcs.Count > 0 && _active is null)
            {
                SetActive(_arcs[0].Node);
            }

            return;
        }

        SetActive(node.Children[0]);
    }

    private void MoveShallower()
    {
        if (_active is null)
        {
            return;
        }

        if (_parentOf.TryGetValue(_active, out var parent) && parent is not null && !ReferenceEquals(parent, _focus))
        {
            SetActive(parent);
            return;
        }

        if (ReferenceEquals(_active, _focus))
        {
            ZoomOut();
            return;
        }

        SetActive(null);
    }

    private void ZoomTo(ArcSunburstNode node)
    {
        var root = Root;
        if (root is null)
        {
            return;
        }

        if (node.IsLeaf)
        {
            SetActive(node);
            NodeSelected?.Invoke(this, new ArcSunburstSelection(node, PathTo(root, node)));
            return;
        }

        _focus = node;
        _active = null;
        _activeAncestors.Clear();
        InvalidateVisual();
        NodeSelected?.Invoke(this, new ArcSunburstSelection(node, PathTo(root, node)));
    }

    private void ZoomOut(bool toRoot = false)
    {
        var root = Root;
        if (root is null || _focus is null || ReferenceEquals(_focus, root))
        {
            return;
        }

        // Walk the real tree rather than the last layout: the focused node is the parent of that layout.
        var path = PathTo(root, _focus);
        var parent = toRoot || path.Count < 2 ? root : path[^2];
        _focus = parent;
        _active = null;
        _activeAncestors.Clear();
        InvalidateVisual();
        NodeSelected?.Invoke(this, new ArcSunburstSelection(_focus, PathTo(root, _focus)));
    }

    private void ResetInteraction()
    {
        _focus = Root;
        _active = null;
        _activeAncestors.Clear();
        _backHover = false;
        InvalidateVisual();
    }

    private static List<ArcSunburstNode> PathTo(ArcSunburstNode root, ArcSunburstNode node)
    {
        var path = new List<ArcSunburstNode>();
        return Walk(root, node, path) ? path : new List<ArcSunburstNode> { root };
    }

    private static bool Walk(ArcSunburstNode current, ArcSunburstNode target, List<ArcSunburstNode> path)
    {
        path.Add(current);
        if (ReferenceEquals(current, target))
        {
            return true;
        }

        foreach (var child in current.Children)
        {
            if (Walk(child, target, path))
            {
                return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }

    private static bool Contains(ArcSunburstNode root, ArcSunburstNode node)
    {
        if (ReferenceEquals(root, node))
        {
            return true;
        }

        foreach (var child in root.Children)
        {
            if (Contains(child, node))
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexOf(IReadOnlyList<ArcSunburstNode> nodes, ArcSunburstNode node)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            if (ReferenceEquals(nodes[index], node))
            {
                return index;
            }
        }

        return -1;
    }

    private string FormatValue(double value)
        => ValueFormat?.Invoke(value) ?? ChartFormat.Compact(value);

    private sealed record ArcItem(
        ArcSunburstNode Node,
        int Level,
        double Start,
        double Sweep,
        IReadOnlyList<ArcSunburstNode> Path,
        IBrush Brush)
    {
        public ArcSunburstNode? Parent => Path.Count > 1 ? Path[^2] : null;
    }
}
