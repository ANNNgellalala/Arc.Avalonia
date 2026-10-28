using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// A flow diagram: nodes sit in columns and links are ribbons whose thickness is the flow. The layered
/// layout is computed here without overlaps, each column's scale is shared so a ribbon keeps one
/// thickness end to end, and hovering a node or ribbon pours its share through the journey while
/// everything else dims. Nodes can be dragged up and down inside their column to reorder them.
/// </summary>
/// <remarks>
/// Approximation: Arc Pro drifts particles along each ribbon and morphs the layout between datasets.
/// Particles are omitted (they are pure decoration and the shot harness renders with reduced motion);
/// the layout is recomputed on data change and settles without interpolation.
/// </remarks>
public class ArcSankeyFlow : ArcChartBase
{
    public static readonly StyledProperty<IReadOnlyList<ArcSankeyNode>?> NodesProperty =
        AvaloniaProperty.Register<ArcSankeyFlow, IReadOnlyList<ArcSankeyNode>?>(nameof(Nodes));

    public static readonly StyledProperty<IReadOnlyList<ArcSankeyLink>?> LinksProperty =
        AvaloniaProperty.Register<ArcSankeyFlow, IReadOnlyList<ArcSankeyLink>?>(nameof(Links));

    public static readonly StyledProperty<string?> ValueSuffixProperty =
        AvaloniaProperty.Register<ArcSankeyFlow, string?>(nameof(ValueSuffix));

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<ArcSankeyFlow, bool>(nameof(ShowLabels), true);

    public static readonly StyledProperty<bool> AllowNodeDragProperty =
        AvaloniaProperty.Register<ArcSankeyFlow, bool>(nameof(AllowNodeDrag), true);

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcSankeyFlow, Func<double, string>?>(nameof(ValueFormat));

    private const double NodeWidth = 12;
    private const double NodeGap = 9;
    private const double LabelGutter = 98;

    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);
    private static readonly Cursor DragCursor = new(StandardCursorType.SizeNorthSouth);

    private readonly List<NodeBox> _boxes = new();
    private readonly List<LinkBand> _bands = new();
    private readonly Dictionary<string, NodeBox> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<int, List<string>> _orders = new();
    private readonly Dictionary<string, double> _valueById = new(StringComparer.Ordinal);
    private Rect _plot;
    private double _scale;
    private string? _activeNode;
    private int _activeLink = -1;
    private string? _dragNode;
    private bool _keyboardFocus;

    static ArcSankeyFlow()
    {
        AffectsRender<ArcSankeyFlow>(
            NodesProperty,
            LinksProperty,
            ValueSuffixProperty,
            ShowLabelsProperty,
            AllowNodeDragProperty,
            ValueFormatProperty);
        NodesProperty.Changed.AddClassHandler<ArcSankeyFlow>((flow, _) => flow.ResetLayout());
        LinksProperty.Changed.AddClassHandler<ArcSankeyFlow>((flow, _) => flow.ResetLayout());
    }

    public ArcSankeyFlow()
    {
        Focusable = true;
    }

    /// <summary>Nodes in the order they stack inside each column.</summary>
    public IReadOnlyList<ArcSankeyNode>? Nodes
    {
        get => GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    /// <summary>Flows between node ids. A node's value is the larger of what flows in and out.</summary>
    public IReadOnlyList<ArcSankeyLink>? Links
    {
        get => GetValue(LinksProperty);
        set => SetValue(LinksProperty, value);
    }

    /// <summary>Unit after values, such as " people".</summary>
    public string? ValueSuffix
    {
        get => GetValue(ValueSuffixProperty);
        set => SetValue(ValueSuffixProperty, value);
    }

    /// <summary>Prints each node's name and value beside it.</summary>
    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    /// <summary>Lets a node be dragged up or down to reorder its column.</summary>
    public bool AllowNodeDrag
    {
        get => GetValue(AllowNodeDragProperty);
        set => SetValue(AllowNodeDragProperty, value);
    }

    /// <summary>Optional custom format for node values and the link tooltip.</summary>
    public Func<double, string>? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    protected override double DefaultHeight => 244;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);

        if (_dragNode is not null)
        {
            var box = _byId.TryGetValue(_dragNode, out var found) ? found : null;
            if (box is not null && AllowNodeDrag)
            {
                ReorderTo(box, point.Y);
                Cursor = DragCursor;
            }

            return;
        }

        var node = NodeAt(point);
        if (node is not null)
        {
            SetActiveNode(node.Id);
            Cursor = AllowNodeDrag ? HandCursor : null;
            return;
        }

        Cursor = null;
        var link = LinkAt(point);
        if (link >= 0)
        {
            SetActiveLink(link);
            return;
        }

        SetActiveNode(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_dragNode is null)
        {
            SetActiveNode(null);
            Cursor = null;
        }
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

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var point = e.GetPosition(this);
        var node = NodeAt(point);
        if (node is not null)
        {
            SetActiveNode(node.Id);
            if (AllowNodeDrag)
            {
                _dragNode = node.Id;
                e.Pointer.Capture(this);
                Cursor = DragCursor;
            }

            e.Handled = true;
            return;
        }

        var link = LinkAt(point);
        if (link >= 0)
        {
            SetActiveLink(link);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragNode is null)
        {
            return;
        }

        _dragNode = null;
        e.Pointer.Capture(null);
        Cursor = null;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragNode = null;
        Cursor = null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_boxes.Count == 0)
        {
            return;
        }

        if (!_keyboardFocus)
        {
            _keyboardFocus = true;
            InvalidateVisual();
        }

        var columns = _orders.Keys.OrderBy(column => column).ToList();
        var current = _activeNode is not null && _byId.TryGetValue(_activeNode, out var box)
            ? box
            : _boxes[0];
        var column = current.Column;
        var columnOrder = _orders.TryGetValue(column, out var list) ? list : new List<string>();
        var index = Math.Max(0, columnOrder.IndexOf(current.Id));

        switch (e.Key)
        {
            case Key.Up:
                index = Math.Max(0, index - 1);
                break;
            case Key.Down:
                index = Math.Min(columnOrder.Count - 1, index + 1);
                break;
            case Key.Left:
            case Key.Right:
            {
                var next = columns.IndexOf(column) + (e.Key == Key.Right ? 1 : -1);
                if (next < 0 || next >= columns.Count)
                {
                    return;
                }

                var targetColumn = columns[next];
                var targetOrder = _orders[targetColumn];
                if (targetOrder.Count == 0)
                {
                    return;
                }

                var targetId = targetOrder[Math.Min(index, targetOrder.Count - 1)];
                SetActiveNode(targetId);
                e.Handled = true;
                return;
            }

            case Key.Home:
                SetActiveNode(_orders[columns[0]][0]);
                e.Handled = true;
                return;
            case Key.End:
                SetActiveNode(_orders[columns[^1]][^1]);
                e.Handled = true;
                return;
            case Key.Escape:
                SetActiveNode(null);
                e.Handled = true;
                return;
            default:
                return;
        }

        if (columnOrder.Count > 0)
        {
            SetActiveNode(columnOrder[index]);
        }

        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _boxes.Clear();
        _bands.Clear();
        _byId.Clear();
        _valueById.Clear();

        var nodes = Nodes;
        var links = Links;
        if (nodes is null || nodes.Count == 0 || links is null ||
            links.All(link => link.Value <= 0))
        {
            ChartText.Draw(
                context,
                "No flows",
                Bounds.Width / 2,
                Bounds.Height / 2,
                13,
                ChartPalette.Muted,
                alignment: TextAlignment.Center);
            return;
        }

        var labels = ShowLabels && Bounds.Width >= 360;
        var gutter = labels ? Math.Min(LabelGutter, Bounds.Width * 0.2) : 6;
        _plot = new Rect(
            gutter,
            10,
            Math.Max(20, Bounds.Width - gutter * 2),
            Math.Max(20, Bounds.Height - 20));
        if (_plot.Width < 40 || _plot.Height < 40)
        {
            return;
        }

        var columns = BuildColumns(nodes, links);
        if (columns.Count == 0)
        {
            return;
        }

        // Values: a node is as thick as the larger of what flows in and out.
        var inflow = new Dictionary<string, double>(StringComparer.Ordinal);
        var outflow = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            inflow[node.Id] = 0;
            outflow[node.Id] = 0;
        }

        foreach (var link in links)
        {
            if (link.Value <= 0 || !inflow.ContainsKey(link.From) || !inflow.ContainsKey(link.To))
            {
                continue;
            }

            outflow[link.From] += link.Value;
            inflow[link.To] += link.Value;
        }

        foreach (var node in nodes)
        {
            _valueById[node.Id] = Math.Max(inflow[node.Id], outflow[node.Id]);
        }

        // A shared scale keeps one ribbon the same thickness at both ends; the tightest column wins.
        var totalsByColumn = new Dictionary<int, double>();
        var countsByColumn = new Dictionary<int, int>();
        foreach (var node in nodes)
        {
            var column = columns[node.Id];
            var inOut = _valueById[node.Id];
            totalsByColumn[column] = totalsByColumn.TryGetValue(column, out var sum) ? sum + inOut : inOut;
            countsByColumn[column] = countsByColumn.TryGetValue(column, out var count) ? count + 1 : 1;
        }

        _scale = double.MaxValue;
        foreach (var pair in totalsByColumn)
        {
            var available = _plot.Height - NodeGap * Math.Max(0, countsByColumn[pair.Key] - 1);
            if (pair.Value > 0)
            {
                _scale = Math.Min(_scale, available / pair.Value);
            }
        }

        if (_scale is double.MaxValue or <= 0)
        {
            return;
        }

        // Orders: the declaration order, then whatever the last drag left behind.
        var known = columns.Keys.ToHashSet(StringComparer.Ordinal);
        var stale = _orders.Count == 0 ||
                    !_orders.Values.SelectMany(ids => ids).All(known.Contains) ||
                    _orders.Values.Sum(ids => ids.Count) != known.Count ||
                    !_orders.Keys.OrderBy(column => column).SequenceEqual(columns.Values.Distinct().OrderBy(value => value));
        if (stale)
        {
            _orders.Clear();
            foreach (var column in columns.Values.Distinct().OrderBy(value => value))
            {
                _orders[column] = nodes
                    .Where(node => columns[node.Id] == column)
                    .Select(node => node.Id)
                    .ToList();
            }
        }

        // Positions.
        var columnCount = columns.Values.Distinct().Count();
        var step = columnCount <= 1 ? 0 : (_plot.Width - NodeWidth) / (columnCount - 1);
        foreach (var pair in _orders.OrderBy(pair => pair.Key))
        {
            var column = pair.Key;
            var ids = pair.Value;
            var total = ids.Sum(id => Math.Max(0, _valueById[id])) * _scale +
                        NodeGap * Math.Max(0, ids.Count - 1);
            var y = _plot.Top + Math.Max(0, (_plot.Height - total) / 2);
            var x = columnCount <= 1 ? _plot.Center.X - NodeWidth / 2 : _plot.Left + column * step;
            for (var index = 0; index < ids.Count; index++)
            {
                var id = ids[index];
                var value = _valueById[id];
                var height = Math.Max(1.5, value * _scale);
                var node = nodes.First(item => string.Equals(item.Id, id, StringComparison.Ordinal));
                var box = new NodeBox(
                    id,
                    node,
                    column,
                    x,
                    y,
                    NodeWidth,
                    height,
                    value,
                    BrushFor(node, index, column));
                _boxes.Add(box);
                _byId[id] = box;
                y += height + NodeGap;
            }
        }

        // Link envelopes: stack outgoing and incoming by the other end's position so ribbons do not cross.
        var incoming = new Dictionary<string, double>(StringComparer.Ordinal);
        var outgoing = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var box in _boxes)
        {
            incoming[box.Id] = box.Y;
            outgoing[box.Id] = box.Y;
        }

        var sorted = links
            .Where(link => link.Value > 0 && _byId.ContainsKey(link.From) && _byId.ContainsKey(link.To))
            .OrderBy(link => _byId[link.To].Y)
            .ThenBy(link => _byId[link.From].Y)
            .ToList();

        foreach (var link in sorted)
        {
            var source = _byId[link.From];
            var target = _byId[link.To];
            var thickness = Math.Max(1.2, link.Value * _scale);
            var y0 = outgoing[link.From];
            var y1 = incoming[link.To];
            outgoing[link.From] = y0 + thickness;
            incoming[link.To] = y1 + thickness;
            _bands.Add(new LinkBand(link, source, target, thickness, y0, y1, source.Brush));
        }

        // Ribbons.
        var idle = _activeNode is null && _activeLink < 0;
        var ribbonAlpha = AdvancedChartInk.OnDarkSurface ? 0.42 : 0.22;
        var litAlpha = AdvancedChartInk.OnDarkSurface ? 0.78 : 0.52;
        foreach (var band in _bands)
        {
            var alpha = idle ? ribbonAlpha : IsBandActive(band) ? litAlpha : 0.07;
            context.DrawGeometry(
                ChartPalette.Alpha(band.Brush, alpha),
                null,
                Ribbon(band));
        }

        // Nodes.
        foreach (var box in _boxes)
        {
            var active = IsBoxActive(box);
            var brush = active ? box.Brush : ChartPalette.Alpha(box.Brush, 0.4);
            context.DrawRectangle(
                brush,
                new Pen(ChartPalette.Surface, 1),
                new RoundedRect(new Rect(box.X, box.Y, box.Width, box.Height), 3));
        }

        if (labels)
        {
            var firstColumn = _orders.Keys.Min();
            var lastColumn = _orders.Keys.Max();
            foreach (var box in _boxes)
            {
                DrawNodeLabel(context, box, firstColumn, lastColumn);
            }
        }

        if (_activeLink >= 0 && _activeLink < _bands.Count)
        {
            var band = _bands[_activeLink];
            ChartTooltip.Draw(
                context,
                Bounds,
                new Point((band.Source.X + band.Target.X) / 2, (band.Y0 + band.Y1) / 2),
                band.Source.Node.Label + " to " + band.Target.Node.Label,
                new[]
                {
                    ("Flow", FormatValue(band.Link.Value), (IBrush?)band.Brush),
                    ("Share of source", ShareOfSource(band), null)
                });
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

    private void DrawNodeLabel(DrawingContext context, NodeBox box, int firstColumn, int lastColumn)
    {
        var active = string.Equals(box.Id, _activeNode, StringComparison.Ordinal) ||
                     _activeLink >= 0 && _activeLink < _bands.Count &&
                     (string.Equals(_bands[_activeLink].Link.From, box.Id, StringComparison.Ordinal) ||
                      string.Equals(_bands[_activeLink].Link.To, box.Id, StringComparison.Ordinal));
        var centerY = box.Y + box.Height / 2;
        var nameBrush = active ? ChartPalette.Foreground : ChartPalette.Secondary;
        var weight = active ? FontWeight.Medium : FontWeight.Normal;

        if (box.Column == firstColumn || box.Column == lastColumn)
        {
            // The outer columns own a full gutter, so name and value stack without crowding a ribbon.
            var room = box.Column == firstColumn ? box.X - 11 : Bounds.Width - box.X - box.Width - 11;
            var name = ChartText.Create(box.Node.Label, 12, nameBrush, weight, Math.Max(30, room));
            var value = ChartText.Create(FormatValue(box.Value), 11, ChartPalette.Muted, maxWidth: Math.Max(30, room));
            var stack = box.Height >= 26;
            if (box.Column == firstColumn)
            {
                var right = box.X - 7;
                ChartText.Draw(context, name, right, stack ? centerY - 7 : centerY, TextAlignment.Right);
                if (stack)
                {
                    ChartText.Draw(context, value, right, centerY + 7, TextAlignment.Right);
                }
            }
            else
            {
                var left = box.X + box.Width + 7;
                ChartText.Draw(context, name, left, stack ? centerY - 7 : centerY);
                if (stack)
                {
                    ChartText.Draw(context, value, left, centerY + 7);
                }
            }

            return;
        }

        // A middle column sits between the ribbons: a surface chip keeps the label readable.
        var roomForChip = Math.Max(48, Math.Min(Bounds.Width - 8, NextColumnLeft(box.Column) - box.X - box.Width - 12));
        var nameText = ChartText.Create(box.Node.Label, 12, nameBrush, weight, Math.Max(28, roomForChip - 8));
        var valueText = ChartText.Create(FormatValue(box.Value), 11, ChartPalette.Muted, maxWidth: Math.Max(28, roomForChip - 8));
        var width = Math.Min(roomForChip, Math.Max(nameText.Width, valueText.Width) + 10);
        var chip = new Rect(box.X + box.Width + 6, centerY - 14, width, 28);
        context.DrawRectangle(ChartPalette.Alpha(ChartPalette.Surface, 0.94), null, new RoundedRect(chip, 5));
        ChartText.Draw(context, nameText, chip.X + 5, centerY - 6, TextAlignment.Left);
        ChartText.Draw(context, valueText, chip.X + 5, centerY + 6, TextAlignment.Left);
    }

    private string ShareOfSource(LinkBand band)
    {
        var source = band.Source.Value;
        return source <= 0 ? "0%" : ChartFormat.Percent(band.Link.Value / source, 0);
    }

    private static StreamGeometry Ribbon(LinkBand band)
    {
        var geometry = new StreamGeometry();
        var x0 = band.Source.X + band.Source.Width;
        var x1 = band.Target.X;
        var y0Top = band.Y0;
        var y0Bottom = band.Y0 + band.Thickness;
        var y1Top = band.Y1;
        var y1Bottom = band.Y1 + band.Thickness;
        var control = Math.Max(6, (x1 - x0) * 0.45);
        using var context = geometry.Open();
        context.BeginFigure(new Point(x0, y0Top), true);
        context.CubicBezierTo(new Point(x0 + control, y0Top), new Point(x1 - control, y1Top), new Point(x1, y1Top));
        context.LineTo(new Point(x1, y1Bottom));
        context.CubicBezierTo(new Point(x1 - control, y1Bottom), new Point(x0 + control, y0Bottom), new Point(x0, y0Bottom));
        context.EndFigure(true);
        return geometry;
    }

    private Dictionary<string, int> BuildColumns(IReadOnlyList<ArcSankeyNode> nodes, IReadOnlyList<ArcSankeyLink> links)
    {
        var raw = new Dictionary<string, int>(StringComparer.Ordinal);
        var fixedColumn = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (node.Column is { } column)
            {
                raw[node.Id] = Math.Max(0, column);
                fixedColumn.Add(node.Id);
            }
        }

        // Sources seed the first column, then every link pushes its target one stage right. Nodes with an
        // explicit column keep it, so a hand-placed stage is never overwritten.
        var hasIncoming = new HashSet<string>(
            links.Where(link => link.Value > 0).Select(link => link.To),
            StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (!raw.ContainsKey(node.Id) && !hasIncoming.Contains(node.Id))
            {
                raw[node.Id] = 0;
            }
        }

        for (var pass = 0; pass < nodes.Count + 1; pass++)
        {
            var changed = false;
            foreach (var link in links)
            {
                if (link.Value <= 0 || !raw.TryGetValue(link.From, out var from))
                {
                    continue;
                }

                var next = from + 1;
                if (!raw.TryGetValue(link.To, out var current))
                {
                    raw[link.To] = next;
                    changed = true;
                }
                else if (!fixedColumn.Contains(link.To) && current < next)
                {
                    raw[link.To] = next;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        // A node no source reaches still gets a stage, one step left of anything it feeds.
        for (var pass = 0; pass < nodes.Count + 1; pass++)
        {
            var changed = false;
            foreach (var link in links)
            {
                if (link.Value <= 0 || raw.ContainsKey(link.From) || !raw.TryGetValue(link.To, out var to))
                {
                    continue;
                }

                raw[link.From] = Math.Max(0, to - 1);
                changed = true;
            }

            if (!changed)
            {
                break;
            }
        }

        foreach (var node in nodes)
        {
            raw.TryAdd(node.Id, 0);
        }

        // Normalise to consecutive columns so an empty stage does not leave a gap.
        var ordered = raw.Values.Distinct().OrderBy(value => value).ToList();
        var map = ordered.Select((value, index) => (value, index)).ToDictionary(pair => pair.value, pair => pair.index);
        return raw.ToDictionary(pair => pair.Key, pair => map[pair.Value], StringComparer.Ordinal);
    }

    private IBrush BrushFor(ArcSankeyNode node, int index, int column)
    {
        var brush = ChartPalette.Series(index);
        return column == 0 ? brush : AdvancedChartInk.Step(brush, 2);
    }

    private bool IsBandActive(LinkBand band)
        => _activeNode is not null
            ? string.Equals(band.Link.From, _activeNode, StringComparison.Ordinal) ||
              string.Equals(band.Link.To, _activeNode, StringComparison.Ordinal)
            : _activeLink < 0 || ReferenceEquals(band, _bands[_activeLink]);

    private bool IsBoxActive(NodeBox box)
    {
        if (_activeNode is not null)
        {
            if (string.Equals(box.Id, _activeNode, StringComparison.Ordinal))
            {
                return true;
            }

            return _bands.Any(band =>
                string.Equals(band.Link.From, _activeNode, StringComparison.Ordinal) &&
                string.Equals(band.Link.To, box.Id, StringComparison.Ordinal) ||
                string.Equals(band.Link.To, _activeNode, StringComparison.Ordinal) &&
                string.Equals(band.Link.From, box.Id, StringComparison.Ordinal));
        }

        return _activeLink < 0 || _activeLink >= _bands.Count ||
               ReferenceEquals(box, _bands[_activeLink].Source) ||
               ReferenceEquals(box, _bands[_activeLink].Target);
    }

    private double NextColumnLeft(int column)
    {
        var next = _boxes.Where(box => box.Column > column).OrderBy(box => box.Column).FirstOrDefault();
        return next is null ? Bounds.Width : next.X;
    }

    private NodeBox? NodeAt(Point point)
    {
        foreach (var box in _boxes)
        {
            if (new Rect(box.X - 3, box.Y - 1, box.Width + 6, box.Height + 2).Contains(point))
            {
                return box;
            }
        }

        return null;
    }

    private int LinkAt(Point point)
    {
        for (var index = 0; index < _bands.Count; index++)
        {
            var band = _bands[index];
            var x0 = band.Source.X + band.Source.Width;
            var x1 = band.Target.X;
            if (point.X < x0 || point.X > x1)
            {
                continue;
            }

            var t = x1 - x0 < 1 ? 0.5 : (point.X - x0) / (x1 - x0);
            var eased = t * t * (3 - 2 * t);
            var top = band.Y0 + (band.Y1 - band.Y0) * eased;
            if (point.Y >= top && point.Y <= top + band.Thickness)
            {
                return index;
            }
        }

        return -1;
    }

    private void ReorderTo(NodeBox box, double y)
    {
        if (!_orders.TryGetValue(box.Column, out var order))
        {
            return;
        }

        var index = order.IndexOf(box.Id);
        if (index < 0)
        {
            return;
        }

        var columnBoxes = _boxes.Where(item => item.Column == box.Column).OrderBy(item => item.Y).ToList();
        var position = columnBoxes.FindIndex(item => string.Equals(item.Id, box.Id, StringComparison.Ordinal));
        if (position < 0)
        {
            return;
        }

        if (y > box.Y + box.Height + NodeGap / 2 && position < columnBoxes.Count - 1)
        {
            order.RemoveAt(index);
            order.Insert(Math.Min(order.Count, index + 1), box.Id);
            ResetNodeDrag(box.Id);
        }
        else if (y < box.Y - NodeGap / 2 && position > 0)
        {
            order.RemoveAt(index);
            order.Insert(Math.Max(0, index - 1), box.Id);
            ResetNodeDrag(box.Id);
        }
    }

    private void ResetNodeDrag(string id)
    {
        SetActiveNode(id);
        InvalidateVisual();
    }

    private void SetActiveNode(string? id)
    {
        if (string.Equals(_activeNode, id, StringComparison.Ordinal) && _activeLink < 0)
        {
            return;
        }

        _activeNode = id;
        _activeLink = -1;
        InvalidateVisual();
    }

    private void SetActiveLink(int index)
    {
        if (_activeLink == index && _activeNode is null)
        {
            return;
        }

        _activeLink = index;
        _activeNode = null;
        InvalidateVisual();
    }

    private void ResetLayout()
    {
        _orders.Clear();
        _boxes.Clear();
        _bands.Clear();
        _byId.Clear();
        _activeNode = null;
        _activeLink = -1;
        _dragNode = null;
        InvalidateVisual();
    }

    private string FormatValue(double value)
    {
        var formatted = ValueFormat?.Invoke(value) ?? ChartFormat.Compact(value);
        return string.IsNullOrEmpty(ValueSuffix) ? formatted : formatted + ValueSuffix;
    }

    private sealed record NodeBox(
        string Id,
        ArcSankeyNode Node,
        int Column,
        double X,
        double Y,
        double Width,
        double Height,
        double Value,
        IBrush Brush);

    private sealed record LinkBand(
        ArcSankeyLink Link,
        NodeBox Source,
        NodeBox Target,
        double Thickness,
        double Y0,
        double Y1,
        IBrush Brush);
}
