using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace Arc.Avalonia.DataDisplay.Controls;

/// <summary>
/// Window arithmetic for the windowed tree table. The control flattens the visible tree into an
/// indexable list and only realises the rows inside the scroll viewport plus an overscan margin, so a
/// 10,000 node tree instantiates a viewport-sized set of controls instead of 10,000.
/// </summary>
internal static class ArcTreeTableWindow
{
    /// <summary>Rows realised above and below the viewport so a small scroll never shows a hole.</summary>
    public const int DefaultOverscan = 6;

    /// <summary>
    /// Returns the inclusive index range to realise for a viewport. Never returns more than the row
    /// count, and always covers every row intersecting the viewport.
    /// </summary>
    public static (int First, int Last) Compute(
        int totalRows,
        double rowHeight,
        double viewportHeight,
        double offset,
        int overscan)
    {
        if (totalRows <= 0)
        {
            return (0, -1);
        }

        if (rowHeight <= 0 || double.IsNaN(rowHeight) || double.IsInfinity(rowHeight))
        {
            return (0, totalRows - 1);
        }

        overscan = Math.Max(0, overscan);
        var top = Math.Max(0, offset);
        var height = viewportHeight > 0 && !double.IsNaN(viewportHeight) && !double.IsInfinity(viewportHeight)
            ? viewportHeight
            : rowHeight;

        var first = (int)Math.Floor(top / rowHeight) - overscan;
        var last = (int)Math.Ceiling((top + height) / rowHeight) + overscan;
        first = Math.Clamp(first, 0, totalRows - 1);
        last = Math.Clamp(last, first, totalRows - 1);
        return (first, last);
    }
}

/// <summary>
/// A simple bounded cache of detached row controls. Rows that scroll out of the window are kept so
/// scrolling back reuses the same visual instance instead of rebuilding it; once the cache is full the
/// oldest entry is dropped and that row is rebuilt on its next appearance.
/// </summary>
internal sealed class ArcRowWindowCache
{
    private readonly Dictionary<int, Control> _entries = new();
    private readonly Queue<int> _order = new();

    public ArcRowWindowCache(int capacity)
    {
        Capacity = Math.Max(0, capacity);
    }

    public int Capacity { get; }

    public int Count => _entries.Count;

    public bool TryTake(int index, out Control control)
    {
        if (_entries.TryGetValue(index, out control!))
        {
            _entries.Remove(index);
            return true;
        }

        control = null!;
        return false;
    }

    public void Return(int index, Control control)
    {
        if (Capacity == 0 || _entries.ContainsKey(index))
        {
            return;
        }

        _entries[index] = control;
        _order.Enqueue(index);
        while (_entries.Count > Capacity && _order.Count > 0)
        {
            var oldest = _order.Dequeue();
            _entries.Remove(oldest);
        }
    }

    public void Clear()
    {
        _entries.Clear();
        _order.Clear();
    }
}

/// <summary>
/// The panel that hosts the realised rows. It measures to the full row extent (so the scrollbar
/// reflects the whole flattened tree) and arranges each realised child at its flattened row slot.
/// </summary>
internal sealed class ArcVirtualRowsPanel : Panel
{
    private readonly Dictionary<Control, int> _rowOfChild = new();

    public double RowHeight { get; set; } = 44;

    public int TotalRows { get; set; }

    public int RealisedRows => _rowOfChild.Count;

    public void SetRow(Control child, int index)
    {
        _rowOfChild[child] = index;
        if (!Children.Contains(child))
        {
            Children.Add(child);
        }
    }

    public void RemoveRow(Control child)
    {
        _rowOfChild.Remove(child);
        Children.Remove(child);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var infinite = double.IsNaN(width) || double.IsInfinity(width) || width < 0;
        var slot = new Size(infinite ? double.PositiveInfinity : width, RowHeight);
        var desiredWidth = 0d;
        foreach (var child in Children)
        {
            child.Measure(slot);
            desiredWidth = Math.Max(desiredWidth, child.DesiredSize.Width);
        }

        return new Size(infinite ? desiredWidth : width, TotalRows * RowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (_rowOfChild.TryGetValue(child, out var index))
            {
                child.Arrange(new Rect(0, index * RowHeight, finalSize.Width, RowHeight));
            }
        }

        return finalSize;
    }
}
