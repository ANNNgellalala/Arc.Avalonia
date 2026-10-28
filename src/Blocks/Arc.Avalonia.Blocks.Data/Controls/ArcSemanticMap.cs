using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace Arc.Avalonia.Blocks.Data;

/// <summary>One node of the company map: a team, a project, a task or a person.</summary>
internal sealed class MapNode
{
    public MapNode(string name, string kind, double value, params MapNode[] children)
    {
        Name = name;
        Kind = kind;
        Value = value;
        Children = children;
    }

    public string Name { get; }

    public string Kind { get; }

    public double Value { get; }

    public IReadOnlyList<MapNode> Children { get; }

    public bool HasChildren => Children.Count > 0;

    public double Rollup => HasChildren ? Children.Sum(child => child.Rollup) : Value;
}

/// <summary>
/// An infinite-zoom map of a company drawn as nested tiles. Each level lays its children out as a grid of
/// cards, the selected card carries an accent outline and its share bar, and Enter flies into the next level
/// while Escape flies out. The camera snaps rather than eases (reduced motion is the default here); there is
/// no canvas transform beyond the level swap.
/// </summary>
internal sealed class ArcSemanticMap : DataStage
{
    private readonly Stack<MapNode> _path = new();
    private int _selected;
    private readonly List<(Rect Rect, int Index)> _hits = new();

    public ArcSemanticMap(MapNode root)
    {
        _path.Push(root);
        Height = 330;
    }

    public MapNode Current => _path.Peek();

    /// <summary>The open path from the company down to the current level.</summary>
    public IReadOnlyList<MapNode> Path => _path.Reverse().ToList();

    /// <summary>Pops to a depth from a breadcrumb, 0 being the company.</summary>
    public void GoToDepth(int depth)
    {
        var target = Math.Max(0, depth);
        while (_path.Count - 1 > target)
        {
            _path.Pop();
        }

        _selected = 0;
        InvalidateVisual();
        Navigated?.Invoke(this, Current);
    }

    public event EventHandler<MapNode>? Navigated;

    public event EventHandler<MapNode?>? SelectionChanged;

    protected override double DefaultHeight => 330;

    /// <summary>Pops back to the root; used by the breadcrumb.</summary>
    public void Reset()
    {
        while (_path.Count > 1)
        {
            _path.Pop();
        }

        _selected = 0;
        InvalidateVisual();
        Navigated?.Invoke(this, Current);
    }

    /// <summary>Pops one level; used by the breadcrumb.</summary>
    public void Up()
    {
        if (_path.Count <= 1)
        {
            return;
        }

        _path.Pop();
        _selected = 0;
        InvalidateVisual();
        Navigated?.Invoke(this, Current);
    }

    public void Enter()
    {
        var children = Current.Children;
        if (children.Count == 0)
        {
            return;
        }

        var index = Math.Clamp(_selected, 0, children.Count - 1);
        var child = children[index];
        if (!child.HasChildren)
        {
            SelectionChanged?.Invoke(this, child);
            return;
        }

        _path.Push(child);
        _selected = 0;
        InvalidateVisual();
        Navigated?.Invoke(this, Current);
    }

    private Rect Plot => new(10, 10, Math.Max(10, Bounds.Width - 20), Math.Max(10, Bounds.Height - 34));

    private List<(MapNode Node, Rect Rect)> Tiles()
    {
        var children = Current.Children;
        var result = new List<(MapNode, Rect)>();
        if (children.Count == 0)
        {
            return result;
        }

        var plot = Plot;
        var columns = children.Count <= 2 ? children.Count : children.Count <= 4 ? 2 : 3;
        var rows = (int)Math.Ceiling(children.Count / (double)columns);
        var gap = 10.0;
        var width = (plot.Width - gap * (columns - 1)) / columns;
        var height = (plot.Height - gap * (rows - 1)) / rows;
        for (var index = 0; index < children.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            result.Add((children[index], new Rect(
                plot.Left + column * (width + gap),
                plot.Top + row * (height + gap),
                width,
                height)));
        }

        return result;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var tiles = Tiles();
        for (var index = 0; index < tiles.Count; index++)
        {
            if (!tiles[index].Rect.Contains(e.GetPosition(this)))
            {
                continue;
            }

            _selected = index;
            InvalidateVisual();
            Focus();
            if (tiles[index].Node.HasChildren)
            {
                Enter();
            }
            else
            {
                SelectionChanged?.Invoke(this, tiles[index].Node);
            }

            e.Handled = true;
            return;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = Current.Children.Count;
        if (count == 0)
        {
            if (e.Key is Key.Escape or Key.Back)
            {
                Up();
                e.Handled = true;
            }

            return;
        }

        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
                _selected = _selected <= 0 ? count - 1 : _selected - 1;
                InvalidateVisual();
                break;
            case Key.Right:
            case Key.Down:
                _selected = _selected + 1 >= count ? 0 : _selected + 1;
                InvalidateVisual();
                break;
            case Key.Enter:
            case Key.Space:
                Enter();
                break;
            case Key.Escape:
            case Key.Back:
                Up();
                break;
            case Key.Home:
                Reset();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var root = _path.Last();
        var level = _path.Count - 1;
        var tiles = Tiles();
        _hits.Clear();
        for (var index = 0; index < tiles.Count; index++)
        {
            var (node, rect) = tiles[index];
            var selected = index == _selected;
            var fill = selected ? Ink.Alpha(Ink.Accent, 0.10) : Ink.SurfaceMuted;
            context.DrawRectangle(fill, selected ? Ink.Pen(Ink.Accent, 1.4) : Ink.Pen(Ink.BorderSubtle, 1), rect,
                14, 14);
            Text(context, node.Kind.ToUpperInvariant(), rect.Left + 12, rect.Top + 16, 9,
                selected ? Ink.Accent : Ink.Muted, FontWeight.Medium);
            Text(context, node.Name, rect.Left + 12, rect.Top + 36, 13, Ink.Foreground, FontWeight.Medium,
                maxWidth: rect.Width - 24);
            Text(context, DataKit.Compact(node.Rollup), rect.Left + 12, rect.Bottom - 46, 15, Ink.Foreground,
                FontWeight.Medium);
            var count = node.HasChildren ? node.Children.Count + " " + node.Children[0].Kind.ToLowerInvariant() + "s" : "person";
            Text(context, count, rect.Left + 12, rect.Bottom - 28, 10, Ink.Muted);

            var share = node.Rollup / Math.Max(1, root.Rollup);
            var bar = new Rect(rect.Left + 12, rect.Bottom - 14, Math.Max(1, rect.Width - 24), 3);
            context.DrawRectangle(Ink.Alpha(Ink.Border, 0.7), null, bar, 2, 2);
            context.DrawRectangle(selected ? Ink.Accent : Ink.Series(0), null,
                new Rect(bar.Left, bar.Top, Math.Max(2, bar.Width * Math.Clamp(share * 3, 0.04, 1)), 3), 2, 2);
        }

        if (tiles.Count == 0)
        {
            Text(context, Current.Name, Bounds.Width / 2, Bounds.Height / 2 - 10, 16, Ink.Foreground,
                FontWeight.Medium, TextAlignment.Center);
            Text(context, Current.Kind + " · " + DataKit.Compact(Current.Value), Bounds.Width / 2, Bounds.Height / 2 + 12,
                11, Ink.Muted, alignment: TextAlignment.Center);
        }

        var hint = level == 0
            ? "Enter a team to fly in · arrows move the cursor"
            : "Enter flies in · Escape flies out · Home fits the company";
        Text(context, hint, 10, Bounds.Height - 12, 10, Ink.Muted);
    }
}
