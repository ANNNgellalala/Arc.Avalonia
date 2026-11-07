using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Team.Controls;

/// <summary>Raised when the cohort cursor moves, with the cell's cohort, month and rate.</summary>
public sealed class CohortCursorEventArgs : EventArgs
{
    public CohortCursorEventArgs(int row, int column, double rate, string cohort)
    {
        Row = row;
        Column = column;
        Rate = rate;
        Cohort = cohort;
    }

    public int Row { get; }

    public int Column { get; }

    public double Rate { get; }

    public string Cohort { get; }
}

/// <summary>
/// The cohort retention chart: a triangular cell grid that can switch to retention curves without changing the
/// data, with a keyboard and pointer cell cursor. Values are deterministic sample cohorts (8 cohorts by 8
/// months) for three segments; cell colour is an accent tint scaled by the rate, so light, dark and every accent
/// work without a second palette. All geometry follows the control width.
/// </summary>
public sealed class ArcCohortChart : Control
{
    private static readonly string[] Cohorts = { "Nov 2025", "Dec 2025", "Jan 2026", "Feb 2026", "Mar 2026", "Apr 2026", "May 2026", "Jun 2026" };

    private static readonly double[] AllBase = { 100, 82, 74, 68, 63, 59, 56, 54 };
    private static readonly double[] ProBase = { 100, 88, 82, 78, 75, 72, 70, 68 };
    private static readonly double[] TeamBase = { 100, 79, 70, 63, 58, 53, 50, 47 };

    public static readonly StyledProperty<string> ModeProperty =
        AvaloniaProperty.Register<ArcCohortChart, string>(nameof(Mode), "grid");

    public static readonly StyledProperty<string> SegmentProperty =
        AvaloniaProperty.Register<ArcCohortChart, string>(nameof(Segment), "all");

    private readonly List<(Rect Rect, int Row, int Column)> _cells = new();
    private Rect _plot;
    private bool _keyboardFocus;

    static ArcCohortChart()
    {
        AffectsRender<ArcCohortChart>(ModeProperty, SegmentProperty);
    }

    public ArcCohortChart()
    {
        Focusable = true;
        ClipToBounds = false;
    }

    public event EventHandler<CohortCursorEventArgs>? CursorChanged;

    /// <summary>"grid" for the triangle, "curves" for the retention curves.</summary>
    public string Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>"all", "pro" or "team"; rewashes the chart without changing geometry.</summary>
    public string Segment
    {
        get => GetValue(SegmentProperty);
        set => SetValue(SegmentProperty, value);
    }

    public int CursorRow { get; private set; }

    public int CursorColumn { get; private set; }

    public double CursorRate => Rate(CursorRow, CursorColumn, Segment);

    protected override Size MeasureOverride(Size availableSize) => new(860, 316);

    private static double[] BaseFor(string segment) => segment switch
    {
        "pro" => ProBase,
        "team" => TeamBase,
        _ => AllBase
    };

    private double Rate(int row, int column, string segment)
    {
        if (column == 0)
        {
            return 100;
        }

        var baseValue = BaseFor(segment)[column];
        var variation = ((row * 3 + column * 7) % 5 - 2) * 0.7 - row * 0.6;
        return Math.Round(Math.Clamp(baseValue + variation, 24, 100), 1);
    }

    private int VisibleColumns(int row) => Cohorts.Length - row;

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width < 120 || height < 100)
        {
            return;
        }

        var mode = string.Equals(Mode, "curves", StringComparison.OrdinalIgnoreCase) ? "curves" : "grid";
        var segment = string.Equals(Segment, "pro", StringComparison.OrdinalIgnoreCase)
            ? "pro"
            : string.Equals(Segment, "team", StringComparison.OrdinalIgnoreCase)
                ? "team"
                : "all";

        var left = 86d;
        var top = 42d;
        var right = 18d;
        var bottom = 34d;
        _plot = new Rect(left, top, Math.Max(80, width - left - right), Math.Max(60, height - top - bottom));

        var accent = ArcResources.Brush(ArcTokens.Accent);
        var muted = ArcResources.Brush(ArcTokens.TextMuted);
        var border = ArcResources.Brush(ArcTokens.BorderSubtle);
        var typeface = ChartTypeface();
        _cells.Clear();

        if (mode == "grid")
        {
            DrawGrid(context, segment, accent, muted, border, typeface);
        }
        else
        {
            DrawCurves(context, segment, accent, muted, border, typeface);
        }

        if (_keyboardFocus)
        {
            context.DrawRectangle(null, new Pen(ArcResources.Brush(ArcTokens.FocusOutline), 2), _plot.Inflate(4), 10, 10);
        }
    }

    private void DrawGrid(DrawingContext context, string segment, IBrush accent, IBrush muted, IBrush border,
        Typeface typeface)
    {
        var rows = Cohorts.Length;
        var columns = Cohorts.Length;
        var cellWidth = _plot.Width / columns;
        var cellHeight = _plot.Height / rows;

        for (var column = 0; column < columns; column++)
        {
            var label = Label($"M{column}", 10.5, muted, typeface);
            context.DrawText(label, new Point(_plot.X + column * cellWidth + (cellWidth - label.Width) / 2, _plot.Y - 24));
        }

        for (var row = 0; row < rows; row++)
        {
            var label = Label(Cohorts[row], 11, muted, typeface);
            context.DrawText(label, new Point(0, _plot.Y + row * cellHeight + (cellHeight - label.Height) / 2));

            var size = $"{(int)(520 - row * 28 + (row % 3) * 12)} users";
            var sizeText = Label(size, 9.5, muted, typeface);
            context.DrawText(sizeText, new Point(0, _plot.Y + row * cellHeight + (cellHeight - sizeText.Height) / 2 + 13));

            var visible = VisibleColumns(row);
            for (var column = 0; column < visible; column++)
            {
                var rate = Rate(row, column, segment);
                var rect = new Rect(
                    _plot.X + column * cellWidth + 2,
                    _plot.Y + row * cellHeight + 2,
                    cellWidth - 4,
                    cellHeight - 4);

                var alpha = 0.05 + rate / 100d * 0.38;
                var accentColor = ArcResources.Color(ArcTokens.Accent);
                var fill = new SolidColorBrush(accentColor, alpha);
                var selected = row == CursorRow && column == CursorColumn;
                var pen = selected ? new Pen(accent, 2) : null;
                context.DrawRectangle(fill, pen, rect, 7, 7);

                var blended = Blend(ArcResources.Color(ArcTokens.SurfaceMuted), accentColor, alpha);
                var textBrush = Contrast(blended, ArcResources.Color(ArcTokens.Foreground)) >=
                                Contrast(blended, ArcResources.Color(ArcTokens.Background))
                    ? ArcResources.Brush(ArcTokens.Foreground)
                    : ArcResources.Brush(ArcTokens.Background);
                var text = Label($"{rate:0}%", 11, textBrush, typeface);
                context.DrawText(text, new Point(rect.X + (rect.Width - text.Width) / 2, rect.Y + (rect.Height - text.Height) / 2));
                _cells.Add((rect, row, column));
            }
        }
    }

    private void DrawCurves(DrawingContext context, string segment, IBrush accent, IBrush muted, IBrush border,
        Typeface typeface)
    {
        for (var line = 0; line <= 4; line++)
        {
            var value = 100 - line * 25;
            var y = _plot.Y + _plot.Height * line / 4d;
            context.DrawLine(new Pen(border, 1), new Point(_plot.X, y), new Point(_plot.Right, y));
            var label = Label($"{value}%", 10, muted, typeface);
            context.DrawText(label, new Point(0, y - label.Height / 2));
        }

        var columns = Cohorts.Length;
        for (var column = 0; column < columns; column++)
        {
            var x = _plot.X + _plot.Width * column / (columns - 1);
            var label = Label($"M{column}", 10.5, muted, typeface);
            context.DrawText(label, new Point(x - label.Width / 2, _plot.Bottom + 8));
        }

        // Every cohort as a quiet line, the average as the one strong line.
        for (var row = 0; row < Cohorts.Length; row++)
        {
            var geometry = new StreamGeometry();
            using (var stream = geometry.Open())
            {
                var visible = VisibleColumns(row);
                for (var column = 0; column < visible; column++)
                {
                    var point = new Point(
                        _plot.X + _plot.Width * column / (columns - 1),
                        _plot.Y + _plot.Height * (1 - Rate(row, column, segment) / 100d));
                    if (column == 0)
                    {
                        stream.BeginFigure(point, false);
                    }
                    else
                    {
                        stream.LineTo(point);
                    }
                }

                stream.EndFigure(false);
            }

            context.DrawGeometry(null, new Pen(new SolidColorBrush(ArcResources.Color(ArcTokens.Accent), 0.18), 1.2), geometry);
        }

        var average = new StreamGeometry();
        using (var stream = average.Open())
        {
            for (var column = 0; column < columns; column++)
            {
                var total = 0d;
                var count = 0;
                for (var row = 0; row < Cohorts.Length; row++)
                {
                    if (column < VisibleColumns(row))
                    {
                        total += Rate(row, column, segment);
                        count++;
                    }
                }

                var value = count == 0 ? 0 : total / count;
                var point = new Point(
                    _plot.X + _plot.Width * column / (columns - 1),
                    _plot.Y + _plot.Height * (1 - value / 100d));
                if (column == 0)
                {
                    stream.BeginFigure(point, false);
                }
                else
                {
                    stream.LineTo(point);
                }
            }

            stream.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(accent, 2.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), average);

        var legend = Label("Average curve", 10.5, muted, typeface);
        context.DrawText(legend, new Point(_plot.Right - legend.Width, _plot.Y - 26));
    }

    private static FormattedText Label(string text, double size, IBrush brush, Typeface typeface) => new(
        text,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        typeface,
        size,
        brush);

    private static Color Blend(Color background, Color foreground, double alpha) => Color.FromArgb(255,
        (byte)Math.Clamp(background.R + (foreground.R - background.R) * alpha, 0, 255),
        (byte)Math.Clamp(background.G + (foreground.G - background.G) * alpha, 0, 255),
        (byte)Math.Clamp(background.B + (foreground.B - background.B) * alpha, 0, 255));

    private static double Contrast(Color first, Color second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        var lighter = Math.Max(a, b);
        var darker = Math.Min(a, b);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var channel = value / 255d;
            return channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static Typeface ChartTypeface()
    {
        var family = ArcResources.Font(ArcTokens.FontBody);
        return new Typeface(family ?? FontFamily.Default);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        _keyboardFocus = false;
        var point = e.GetPosition(this);
        foreach (var cell in _cells)
        {
            if (cell.Rect.Contains(point))
            {
                MoveCursor(cell.Row, cell.Column);
                e.Handled = true;
                return;
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _keyboardFocus = true;
        var row = CursorRow;
        var column = CursorColumn;
        switch (e.Key)
        {
            case Key.Left:
                column = Math.Max(0, column - 1);
                break;
            case Key.Right:
                column = Math.Min(VisibleColumns(row) - 1, column + 1);
                break;
            case Key.Up:
                row = Math.Max(0, row - 1);
                column = Math.Min(column, VisibleColumns(row) - 1);
                break;
            case Key.Down:
                row = Math.Min(Cohorts.Length - 1, row + 1);
                column = Math.Min(column, VisibleColumns(row) - 1);
                break;
            case Key.Home:
                column = 0;
                break;
            case Key.End:
                column = VisibleColumns(row) - 1;
                break;
            case Key.Escape:
                return;
            default:
                return;
        }

        MoveCursor(row, column);
        e.Handled = true;
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _keyboardFocus = false;
        InvalidateVisual();
    }
    private void MoveCursor(int row, int column)
    {
        CursorRow = Math.Clamp(row, 0, Cohorts.Length - 1);
        CursorColumn = Math.Clamp(column, 0, VisibleColumns(CursorRow) - 1);
        InvalidateVisual();
        CursorChanged?.Invoke(this, new CohortCursorEventArgs(CursorRow, CursorColumn,
            Rate(CursorRow, CursorColumn, Segment), Cohorts[CursorRow]));
    }

    /// <summary>Average rate for a month, used by the page KPIs.</summary>
    public double AverageAt(int column)
    {
        var segment = Segment;
        var total = 0d;
        var count = 0;
        for (var row = 0; row < Cohorts.Length; row++)
        {
            if (column < VisibleColumns(row))
            {
                total += Rate(row, column, segment);
                count++;
            }
        }

        return count == 0 ? 0 : total / count;
    }

    /// <summary>Best rate in a month, used by the page KPIs.</summary>
    public double BestAt(int column)
    {
        var best = 0d;
        for (var row = 0; row < Cohorts.Length; row++)
        {
            if (column < VisibleColumns(row))
            {
                best = Math.Max(best, Rate(row, column, Segment));
            }
        }

        return best;
    }
}
