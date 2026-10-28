using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMedia.Controls;

/// <summary>
/// Arc's photo grid: a library grid with zoom levels between column counts, favourites, and a full viewer
/// that grows from the tapped cell with a filmstrip. Pinch is approximated on the desktop with Ctrl+wheel
/// and the plus and minus keys, which change the column count around the focused photo.
/// </summary>
public sealed class ArcPhotoGrid : ArcSpecialCanvas
{
    private sealed record Shot(int Seed, string Title, string Detail, double Ratio);

    private static readonly Shot[] Photos =
    {
        new(1, "Harbor", "May 2", 1.0),
        new(7, "Night ferry", "May 3", 1.0),
        new(4, "Headland", "May 4", 0.75),
        new(2, "Tile court", "May 6", 1.0),
        new(0, "Dunes", "May 8", 1.33),
        new(3, "Garden", "May 9", 1.0),
        new(6, "Studio", "May 11", 1.0),
        new(5, "Salt road", "May 12", 1.33),
        new(8, "Lanterns", "May 14", 1.0),
        new(9, "Paper", "May 15", 0.75),
        new(1, "Low tide", "May 18", 1.0),
        new(4, "Arches", "May 20", 1.0)
    };

    private static readonly int[] ColumnLevels = { 6, 4, 3 };

    private readonly Rect[] _cells = new Rect[Photos.Length];
    private readonly Rect[] _film = new Rect[Photos.Length];
    private readonly HashSet<int> _favorites = new() { 3, 7 };
    private Size _layoutSize;
    private int _level;
    private int _focus;
    private int _index;
    private double _scroll;
    private Spring _viewer = Spring.At(0, 200, 28);
    private bool _open;
    private Rect _origin;
    private Rect _stage;
    private Rect _close;
    private Rect _heart;
    private Rect _minus;
    private Rect _plus;
    private double _dragY;

    public ArcPhotoGrid()
    {
        SpecialUi.Name(
            this,
            "Photo grid",
            "A photo library with zoom levels. Arrow keys move between photos, plus and minus change the column count, Enter opens the viewer and Escape closes it.");
    }

    protected override double DefaultWidth => 940;

    protected override double DefaultHeight => 450;

    private int Columns => ColumnLevels[_level];

    private void BuildLayout(Size size)
    {
        if (_layoutSize == size)
        {
            return;
        }

        _layoutSize = size;
        _minus = new Rect(size.Width - 206, 4, 32, 32);
        _plus = new Rect(size.Width - 38, 4, 32, 32);

        var gap = 6.0;
        var cell = (size.Width - (Columns - 1) * gap) / Columns;
        for (var index = 0; index < Photos.Length; index++)
        {
            var column = index % Columns;
            var row = index / Columns;
            _cells[index] = new Rect(column * (cell + gap), 54 + row * (cell + gap) - _scroll, cell, cell);
        }

        var filmWidth = 40.0;
        var filmGap = 6.0;
        var total = Photos.Length * filmWidth + (Photos.Length - 1) * filmGap;
        var filmStart = (size.Width - total) / 2;
        for (var index = 0; index < Photos.Length; index++)
        {
            _film[index] = new Rect(filmStart + index * (filmWidth + filmGap), size.Height - 50, filmWidth, 34);
        }

        UpdateStage(size);
    }

    private void UpdateStage(Size size)
    {
        var shot = Photos[Math.Clamp(_index, 0, Photos.Length - 1)];
        var maxWidth = Math.Min(size.Width - 260, 540);
        var maxHeight = Math.Max(160, size.Height - 190);
        var height = maxHeight;
        var width = height * shot.Ratio;
        if (width > maxWidth)
        {
            width = maxWidth;
            height = width / shot.Ratio;
        }

        _stage = new Rect((size.Width - width) / 2, (size.Height - height) / 2 - 36, width, height);
        _close = new Rect(_stage.Right - 4, _stage.Y + 12, 32, 32);
        _heart = new Rect(_stage.X + 12, _stage.Y + 12, 34, 34);
    }

    private void SetLevel(int level, bool anchorFocus)
    {
        _level = Math.Clamp(level, 0, ColumnLevels.Length - 1);
        _layoutSize = default;
        _scroll = 0;
        if (anchorFocus)
        {
            _focus = Math.Clamp(_focus, 0, Photos.Length - 1);
        }

        InvalidateVisual();
    }

    private void Open(int index)
    {
        _index = Math.Clamp(index, 0, Photos.Length - 1);
        _focus = _index;
        UpdateStage(Bounds.Size);
        _origin = _cells[_index];
        _open = true;
        _dragY = 0;
        _viewer.Jump(SpecialUi.Reduced ? 1 : 0);
        EnsureFrame();
    }

    private void Close()
    {
        _open = false;
        _viewer.Jump(0);
        EnsureFrame();
        InvalidateVisual();
    }

    private void Move(int direction)
    {
        if (!_open)
        {
            return;
        }

        _index = (_index + direction + Photos.Length) % Photos.Length;
        _focus = _index;
        UpdateStage(Bounds.Size);
        _origin = _cells[_index];
        EnsureFrame();
    }

    protected override void OnFrame(double dt) => _viewer.Step(_open ? 1 : 0, dt);

    protected override bool NeedsFrame => Math.Abs(_viewer.Value - (_open ? 1 : 0)) > 0.002;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        BuildLayout(Bounds.Size);
        var point = e.GetPosition(this);

        if (_open)
        {
            if (_close.Contains(point))
            {
                Close();
            }
            else if (_heart.Contains(point))
            {
                ToggleFavorite(_index);
            }
            else
            {
                for (var index = 0; index < _film.Length; index++)
                {
                    if (_film[index].Contains(point))
                    {
                        _index = index;
                        _focus = index;
                        UpdateStage(Bounds.Size);
                        _origin = _cells[index];
                        break;
                    }
                }

                if (!_stage.Contains(point))
                {
                    Close();
                }
            }

            e.Handled = true;
            return;
        }

        if (_minus.Contains(point))
        {
            SetLevel(_level + 1, true);
            e.Handled = true;
            return;
        }

        if (_plus.Contains(point))
        {
            SetLevel(_level - 1, true);
            e.Handled = true;
            return;
        }

        for (var index = 0; index < _cells.Length; index++)
        {
            if (!_cells[index].Contains(point))
            {
                continue;
            }

            _focus = index;
            if (point.X > _cells[index].Right - 30 && point.Y < _cells[index].Y + 30)
            {
                ToggleFavorite(index);
            }
            else
            {
                Open(index);
            }

            break;
        }

        e.Handled = true;
    }

    private void ToggleFavorite(int index)
    {
        if (!_favorites.Remove(index))
        {
            _favorites.Add(index);
        }

        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            SetLevel(_level + (e.Delta.Y < 0 ? 1 : -1), true);
            e.Handled = true;
            return;
        }

        if (!_open && _level > 0)
        {
            _scroll = Math.Max(0, _scroll - e.Delta.Y * 40);
            _layoutSize = default;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_open)
        {
            switch (e.Key)
            {
                case Key.Left:
                    Move(-1);
                    e.Handled = true;
                    break;
                case Key.Right:
                    Move(1);
                    e.Handled = true;
                    break;
                case Key.Home:
                    _index = 0;
                    _focus = 0;
                    UpdateStage(Bounds.Size);
                    _origin = _cells[0];
                    e.Handled = true;
                    break;
                case Key.End:
                    _index = Photos.Length - 1;
                    _focus = _index;
                    UpdateStage(Bounds.Size);
                    _origin = _cells[^1];
                    e.Handled = true;
                    break;
                case Key.F:
                    ToggleFavorite(_index);
                    e.Handled = true;
                    break;
                case Key.Escape:
                    Close();
                    e.Handled = true;
                    break;
            }

            InvalidateVisual();
            return;
        }

        switch (e.Key)
        {
            case Key.Left:
                _focus = Math.Max(0, _focus - 1);
                e.Handled = true;
                break;
            case Key.Right:
                _focus = Math.Min(Photos.Length - 1, _focus + 1);
                e.Handled = true;
                break;
            case Key.Up:
                _focus = Math.Max(0, _focus - Columns);
                e.Handled = true;
                break;
            case Key.Down:
                _focus = Math.Min(Photos.Length - 1, _focus + Columns);
                e.Handled = true;
                break;
            case Key.PageUp:
                _focus = Math.Max(0, _focus - Columns * 3);
                e.Handled = true;
                break;
            case Key.PageDown:
                _focus = Math.Min(Photos.Length - 1, _focus + Columns * 3);
                e.Handled = true;
                break;
            case Key.Home:
                _focus = 0;
                e.Handled = true;
                break;
            case Key.End:
                _focus = Photos.Length - 1;
                e.Handled = true;
                break;
            case Key.Add:
            case Key.OemPlus:
                SetLevel(_level - 1, false);
                e.Handled = true;
                break;
            case Key.Subtract:
            case Key.OemMinus:
                SetLevel(_level + 1, false);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                Open(_focus);
                e.Handled = true;
                break;
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        BuildLayout(size);
        var foreground = SpecialUi.Brush(ArcTokens.Foreground);
        var secondary = SpecialUi.Brush(ArcTokens.TextSecondary);
        var muted = SpecialUi.Brush(ArcTokens.TextMuted);
        var border = SpecialUi.Brush(ArcTokens.Border);

        var title = SpecialUi.Text("September", 22, foreground, FontWeight.Medium);
        context.DrawText(title, new Point(0, 0));
        context.DrawText(SpecialUi.Text($"{Photos.Length} photos  ·  zoom level {_level + 1} of {ColumnLevels.Length}", 12, secondary), new Point(0, 28));

        // Zoom pill: minus, column count, plus.
        var pill = new Rect(size.Width - 214, 0, 214, 40);
        SpecialUi.RoundRect(context, pill, 20, SpecialUi.Brush(ArcTokens.Surface));
        context.DrawRectangle(null, new Pen(border, 1), pill.Deflate(0.5), 20, 20);
        SpecialUi.RoundRect(context, _minus, 16, SpecialUi.Brush(ArcTokens.SurfaceMuted));
        SpecialIcons.Draw(context, "minus", _minus.Deflate(9), foreground, 1.9);
        SpecialUi.RoundRect(context, _plus, 16, SpecialUi.Brush(ArcTokens.SurfaceMuted));
        SpecialIcons.Draw(context, "plus", _plus.Deflate(9), foreground, 1.9);
        SpecialUi.DrawTextCentered(context, SpecialUi.Text($"{Columns} columns", 12, secondary), new Rect(_minus.Right + 2, 4, _plus.X - _minus.Right - 4, 32));

        // Grid cells (clipped to the viewport under the header).
        using (context.PushClip(new Rect(0, 52, size.Width, size.Height - 52)))
        {
            for (var index = 0; index < Photos.Length; index++)
            {
                var rect = _cells[index];
                if (rect.Bottom < 52 || rect.Y > size.Height)
                {
                    continue;
                }

                using (context.PushClip(new RoundedRect(rect, 8)))
                {
                    SampleArt.Draw(context, rect, Photos[index].Seed, 8, null, false);
                }

                context.DrawRectangle(null, new Pen(SpecialUi.Alpha(ArcTokens.Border, 0.55), 1), rect.Deflate(0.5), 8, 8);

                if (_favorites.Contains(index))
                {
                    var badge = new Rect(rect.X + 6, rect.Y + 6, 22, 22);
                    context.DrawEllipse(SpecialUi.Alpha(ArcTokens.Background, 0.85), null, badge.Center, 11, 11);
                    SpecialIcons.Draw(context, "heart", badge.Deflate(5), SpecialUi.Brush(ArcTokens.Accent), 2.2);
                }

                if (Hovering && !_open && rect.Contains(Pointer))
                {
                    var ink = SpecialUi.Color(ArcTokens.Scrim);
                    var strip = new Rect(rect.X, rect.Bottom - 26, rect.Width, 26);
                    context.DrawRectangle(
                        new LinearGradientBrush
                        {
                            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                            GradientStops =
                            {
                                new GradientStop(ArcColor.WithAlpha(ink, 0), 0),
                                new GradientStop(ArcColor.WithAlpha(ink, 0.5), 1)
                            }
                        },
                        null,
                        strip);
                    context.DrawText(SpecialUi.Text(Photos[index].Title, 10, SpecialUi.Brush(ArcTokens.Background), FontWeight.Medium), new Point(rect.X + 8, rect.Bottom - 17));
                }
            }
        }

        context.DrawText(SpecialUi.Text("Click a photo to open  ·  Ctrl+scroll to change zoom", 11, muted), new Point(0, size.Height - 20));

        if (ShowFocusRing && !_open)
        {
            SpecialUi.FocusRing(context, _cells[Math.Clamp(_focus, 0, Photos.Length - 1)], 10);
        }

        if (_viewer.Value > 0.01)
        {
            DrawViewer(context, size, foreground, secondary);
        }
    }

    private void DrawViewer(DrawingContext context, Size size, IBrush foreground, IBrush secondary)
    {
        var t = Math.Clamp(_viewer.Value, 0, 1);
        using (context.PushOpacity(t))
        {
            context.DrawRectangle(SpecialUi.Brush(ArcTokens.Scrim), null, new Rect(0, 0, size.Width, size.Height), 0, 0);
        }

        var origin = _open ? _origin : _cells[Math.Clamp(_index, 0, Photos.Length - 1)];
        var rect = new Rect(
            SpecialUi.Lerp(origin.X, _stage.X, t),
            SpecialUi.Lerp(origin.Y, _stage.Y + _dragY, t),
            SpecialUi.Lerp(origin.Width, _stage.Width, t),
            SpecialUi.Lerp(origin.Height, _stage.Height, t));

        if (t > 0.55)
        {
            SpecialUi.Lift(context, rect, 20, 1.3);
        }

        using (context.PushClip(new RoundedRect(rect, SpecialUi.Lerp(8, 18, t))))
        {
            SampleArt.Draw(context, rect, Photos[Math.Clamp(_index, 0, Photos.Length - 1)].Seed, 18);
        }

        var shot = Photos[Math.Clamp(_index, 0, Photos.Length - 1)];
        using (context.PushOpacity(t))
        {
            var title = SpecialUi.Text(shot.Title, 18, SpecialUi.Brush(ArcTokens.Surface), FontWeight.Medium);
            var meta = SpecialUi.Text($"{shot.Detail}  ·  {_index + 1} of {Photos.Length}", 12, SpecialUi.Alpha(ArcTokens.Surface, 0.72));
            var captionY = Math.Min(rect.Bottom + 12, size.Height - 96);
            context.DrawText(title, new Point(rect.X, captionY));
            context.DrawText(meta, new Point(rect.X, captionY + title.Height + 2));

            var close = new Rect(rect.Right - 44, rect.Y + 12, 32, 32);
            _close = close;
            SpecialUi.RoundRect(context, close, 16, SpecialUi.Brush(ArcTokens.Surface));
            SpecialIcons.Draw(context, "x", close.Deflate(8), SpecialUi.Brush(ArcTokens.Foreground), 2);

            var heart = new Rect(rect.X + 12, rect.Y + 12, 34, 34);
            _heart = heart;
            SpecialUi.RoundRect(context, heart, 17, SpecialUi.Alpha(ArcTokens.Surface, 0.92));
            SpecialIcons.Draw(
                context,
                "heart",
                heart.Deflate(9),
                _favorites.Contains(_index) ? SpecialUi.Brush(ArcTokens.Accent) : SpecialUi.Brush(ArcTokens.Foreground),
                2.2);

            // Filmstrip.
            for (var index = 0; index < _film.Length; index++)
            {
                var thumb = _film[index];
                using (context.PushClip(new RoundedRect(thumb, 7)))
                {
                    SampleArt.Draw(context, thumb, Photos[index].Seed, 7, null, false);
                }

                if (index == _index)
                {
                    context.DrawRectangle(null, new Pen(SpecialUi.Brush(ArcTokens.Surface), 2), thumb.Inflate(2), 8, 8);
                }
            }

            var previous = new Rect(rect.X - 50, rect.Center.Y - 18, 36, 36);
            if (previous.X > 8)
            {
                SpecialUi.RoundRect(context, previous, 18, SpecialUi.Alpha(ArcTokens.Surface, 0.92));
                SpecialIcons.Draw(context, "chevron-left", previous.Deflate(9), SpecialUi.Brush(ArcTokens.Foreground), 2);
            }

            var next = new Rect(rect.Right + 14, rect.Center.Y - 18, 36, 36);
            if (next.Right < size.Width - 8)
            {
                SpecialUi.RoundRect(context, next, 18, SpecialUi.Alpha(ArcTokens.Surface, 0.92));
                SpecialIcons.Draw(context, "chevron-right", next.Deflate(9), SpecialUi.Brush(ArcTokens.Foreground), 2);
            }
        }

        _ = foreground;
        _ = secondary;
    }
}
