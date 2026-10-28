using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMedia.Controls;

/// <summary>
/// Arc's page curl: a small magazine you read by dragging a page corner. The fold line is the perpendicular
/// bisector between the grabbed corner and the pointer, the lifted part is mirrored across it with a paper
/// back, a lit crease and a shadow on the page below, and a flick or a pull past the middle turns the spread.
/// A static dog-ear marks the corner when nothing is being dragged.
/// </summary>
public sealed class ArcPageCurl : ArcSpecialCanvas
{
    private const int PageCount = 8;
    private const double PageAspect = 0.72;

    private int _spread = 1;
    private bool _dragging;
    private bool _animating;
    private bool _pending;
    private int _direction = 1;
    private double _animT;
    private Point _grab;
    private Point _pointer;
    private Point _animFrom;
    private Point _animTo;
    private Spring _fade = Spring.At(1, 200, 26);
    private Rect _left;
    private Rect _right;
    private Rect _book;
    private Rect _previous;
    private Rect _next;
    private bool _interacted;

    public ArcPageCurl()
    {
        SpecialUi.Name(
            this,
            "Page curl, pages 2 and 3 of 8",
            "A small magazine. Drag the lower outer corner, or press Right and Left (or PageDown and PageUp) to turn the spread; the page count is announced.");
    }

    protected override double DefaultWidth => 760;

    protected override double DefaultHeight => 442;

    private bool IsSpread => _spread is > 0 and < 4;

    private void Layout(Size size)
    {
        var pageHeight = Math.Min(size.Height - 106, 354);
        var pageWidth = pageHeight * PageAspect;
        var centerX = size.Width / 2;

        if (_spread == 0)
        {
            _book = new Rect(centerX - pageWidth / 2, 14, pageWidth, pageHeight);
            _left = default;
            _right = _book;
        }
        else if (_spread == 4)
        {
            _book = new Rect(centerX - pageWidth / 2, 14, pageWidth, pageHeight);
            _left = _book;
            _right = default;
        }
        else
        {
            _book = new Rect(centerX - pageWidth, 14, pageWidth * 2, pageHeight);
            _left = new Rect(_book.X, _book.Y, pageWidth, pageHeight);
            _right = new Rect(centerX, _book.Y, pageWidth, pageHeight);
        }

        _previous = new Rect(centerX - 96, _book.Bottom + 34, 34, 34);
        _next = new Rect(centerX + 62, _book.Bottom + 34, 34, 34);
    }

    private int LeftPage => _spread switch
    {
        0 => -1,
        4 => 7,
        _ => 2 * _spread - 1
    };

    private int RightPage => _spread switch
    {
        0 => 0,
        4 => -1,
        _ => 2 * _spread
    };

    private string CountText => _spread switch
    {
        0 => "Cover",
        4 => "Back cover",
        _ => $"Pages {LeftPage + 1} and {RightPage + 1} of {PageCount}"
    };

    private Point CornerFor(int direction) => direction > 0
        ? new Point(_right.Right - 4, _right.Bottom - 4)
        : new Point(_left.X + 4, _left.Bottom - 4);

    private Rect FoldingRect => _direction > 0 ? _right : _left;

    private void Turn(int direction)
    {
        if (_animating || _dragging)
        {
            return;
        }

        var target = _spread + direction;
        if (target < 0 || target > 4)
        {
            return;
        }

        _interacted = true;
        _pending = true;
        _direction = direction;
        _grab = CornerFor(direction);
        _pointer = _grab;
        _animFrom = _grab;
        _animTo = direction > 0
            ? new Point(_grab.X - FoldingRect.Width * 1.05, _grab.Y - FoldingRect.Height * 0.06)
            : new Point(_grab.X + FoldingRect.Width * 1.05, _grab.Y - FoldingRect.Height * 0.06);

        if (SpecialUi.Reduced)
        {
            Commit(target);
            return;
        }

        _animating = true;
        _animT = 0;
        EnsureFrame();
        InvalidateVisual();
    }

    private void Commit(int spread)
    {
        _spread = Math.Clamp(spread, 0, 4);
        _dragging = false;
        _animating = false;
        _pending = false;
        _pointer = default;
        _grab = default;
        _fade.Jump(SpecialUi.Reduced ? 1 : 0.55);
        EnsureFrame();
        UpdateAutomation();
        InvalidateVisual();
    }

    private void UpdateAutomation()
    {
        SpecialUi.Name(this, $"Page curl, {CountText.ToLowerInvariant()}");
    }

    protected override void OnFrame(double dt)
    {
        _fade.Step(1, dt);
        if (!_animating)
        {
            return;
        }

        _animT = Math.Min(1, _animT + dt / 0.34);
        var eased = 1 - Math.Pow(1 - _animT, 3);
        _pointer = SpecialUi.Lerp(_animFrom, _animTo, eased);
        if (_animT >= 1)
        {
            var spread = _spread + (_pending ? _direction : 0);
            Commit(spread);
        }
    }

    protected override bool NeedsFrame => _animating || Math.Abs(_fade.Value - 1) > 0.002;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        Layout(Bounds.Size);
        var point = e.GetPosition(this);

        if (_previous.Contains(point))
        {
            Turn(-1);
            e.Handled = true;
            return;
        }

        if (_next.Contains(point))
        {
            Turn(1);
            e.Handled = true;
            return;
        }

        var corner = new Point(
            _right.Width > 1 ? _right.Right : _left.Right,
            _right.Width > 1 ? _right.Bottom : _left.Bottom);
        var previousCorner = _left.Width > 1 ? new Point(_left.X, _left.Bottom) : new Point(-100, -100);

        if (_right.Width > 1 && Distance(point, corner) < 52)
        {
            _direction = 1;
            _dragging = true;
            _interacted = true;
            _grab = new Point(_right.Right - 4, _right.Bottom - 4);
            _pointer = _grab;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (_left.Width > 1 && Distance(point, previousCorner) < 52)
        {
            _direction = -1;
            _dragging = true;
            _interacted = true;
            _grab = new Point(_left.X + 4, _left.Bottom - 4);
            _pointer = _grab;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        // A tap on a page turns in that page's direction.
        if (_right.Width > 1 && _right.Contains(point))
        {
            Turn(1);
        }
        else if (_left.Width > 1 && _left.Contains(point))
        {
            Turn(-1);
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging)
        {
            return;
        }

        var point = e.GetPosition(this);
        var rect = FoldingRect;
        _pointer = _direction > 0
            ? new Point(Math.Clamp(point.X, rect.X - 20, rect.Right - 2), Math.Clamp(point.Y, rect.Y + 10, rect.Bottom - 2))
            : new Point(Math.Clamp(point.X, rect.X + 2, rect.Right + 20), Math.Clamp(point.Y, rect.Y + 10, rect.Bottom - 2));
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        var rect = FoldingRect;
        var travelled = _direction > 0 ? _grab.X - _pointer.X : _pointer.X - _grab.X;
        var progress = travelled / Math.Max(1, rect.Width);
        if (progress > 0.44)
        {
            _animating = true;
            _pending = true;
            _animT = 0;
            _animFrom = _pointer;
            _animTo = _direction > 0
                ? new Point(rect.X - rect.Width * 0.10, _pointer.Y - rect.Height * 0.05)
                : new Point(rect.Right + rect.Width * 0.10, _pointer.Y - rect.Height * 0.05);
            EnsureFrame();
        }
        else
        {
            _pointer = default;
            _grab = default;
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Right:
            case Key.PageDown:
            case Key.Down:
                Turn(1);
                e.Handled = true;
                break;
            case Key.Left:
            case Key.PageUp:
            case Key.Up:
                Turn(-1);
                e.Handled = true;
                break;
            case Key.Home:
                Commit(0);
                e.Handled = true;
                break;
            case Key.End:
                Commit(4);
                e.Handled = true;
                break;
        }
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        Layout(size);
        var paper = SpecialUi.Brush(ArcTokens.Surface);
        var border = SpecialUi.Brush(ArcTokens.Border);
        var foreground = SpecialUi.Color(ArcTokens.Foreground);
        var secondary = SpecialUi.Color(ArcTokens.TextSecondary);
        var muted = SpecialUi.Color(ArcTokens.TextMuted);
        var accent = SpecialUi.Color(ArcTokens.Accent);
        var background = SpecialUi.Color(ArcTokens.Background);

        using (context.PushOpacity(Math.Clamp(_fade.Value, 0.25, 1)))
        {
            SpecialUi.Lift(context, _book, 12, 1.1);
            if (IsSpread)
            {
                SpecialUi.RoundRect(context, _book, 8, paper);
                using (context.PushClip(new RoundedRect(_book, 8)))
                {
                    DrawPage(context, _left, LeftPage, foreground, secondary, muted, accent);
                    DrawPage(context, _right, RightPage, foreground, secondary, muted, accent);

                    // Spine: a soft fall-off into the gutter.
                    var spine = new Rect(_book.Center.X - 26, _book.Y, 52, _book.Height);
                    context.DrawRectangle(
                        new LinearGradientBrush
                        {
                            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                            GradientStops =
                            {
                                new GradientStop(ArcColor.WithAlpha(foreground, 0.0), 0),
                                new GradientStop(ArcColor.WithAlpha(foreground, 0.07), 0.5),
                                new GradientStop(ArcColor.WithAlpha(foreground, 0.0), 1)
                            }
                        },
                        null,
                        spine);
                    context.DrawLine(
                        new Pen(SpecialUi.Solid(ArcColor.WithAlpha(foreground, 0.10)), 1),
                        new Point(_book.Center.X, _book.Y + 4),
                        new Point(_book.Center.X, _book.Bottom - 4));
                }

                context.DrawRectangle(null, new Pen(border, 1), _book.Deflate(0.5), 8, 8);
            }
            else
            {
                var page = _right.Width > 1 ? _right : _left;
                SpecialUi.RoundRect(context, page, 8, paper);
                using (context.PushClip(new RoundedRect(page, 8)))
                {
                    DrawPage(context, page, page == _right ? 0 : 7, foreground, secondary, muted, accent);
                }

                context.DrawRectangle(null, new Pen(border, 1), page.Deflate(0.5), 8, 8);
            }

            if (_dragging || _animating)
            {
                DrawCurl(context, foreground, paper, background);
            }
            else if (IsSpread)
            {
                DrawDogEar(context, foreground, paper);
            }
        }

        // Hint and page controls.
        if (!_interacted)
        {
            var hint = SpecialUi.Text("Drag the corner", 11, SpecialUi.Solid(muted));
            SpecialUi.DrawTextCentered(context, hint, new Rect(0, _book.Bottom + 8, size.Width, hint.Height + 2));
        }

        DrawControl(context, _previous, "chevron-left", _spread > 0, foreground, paper);
        DrawControl(context, _next, "chevron-right", _spread < 4, foreground, paper);
        var count = SpecialUi.Text(CountText, 12, SpecialUi.Solid(secondary));
        SpecialUi.DrawTextCentered(context, count, new Rect(0, _book.Bottom + 34, size.Width, 34));

        if (ShowFocusRing)
        {
            SpecialUi.FocusRing(context, _book, 10);
        }
    }

    private void DrawControl(DrawingContext context, Rect rect, string icon, bool enabled, Color foreground, IBrush paper)
    {
        SpecialUi.RoundRect(context, rect, 17, paper);
        context.DrawRectangle(null, new Pen(SpecialUi.Brush(ArcTokens.Border), 1), rect.Deflate(0.5), 17, 17);
        SpecialIcons.Draw(
            context,
            icon,
            rect.Deflate(10),
            enabled ? foreground : SpecialUi.Color(ArcTokens.TextMuted),
            1.9);
    }

    private void DrawDogEar(DrawingContext context, Color foreground, IBrush paper)
    {
        if (_right.Width < 1)
        {
            return;
        }

        var corner = new Point(_right.Right - 2, _right.Bottom - 2);
        var size = 22.0;
        var triangle = new[]
        {
            new Point(corner.X - size, corner.Y),
            new Point(corner.X, corner.Y - size),
            new Point(corner.X - size, corner.Y - size)
        };
        context.DrawGeometry(SpecialUi.Solid(ArcColor.Mix(SpecialUi.Color(ArcTokens.Surface), foreground, 0.08)), null, SpecialUi.Polygon(triangle));
        context.DrawLine(
            new Pen(SpecialUi.Solid(ArcColor.WithAlpha(foreground, 0.22)), 1),
            new Point(corner.X - size, corner.Y),
            new Point(corner.X, corner.Y - size));
        _ = paper;
    }

    private void DrawCurl(DrawingContext context, Color foreground, IBrush paper, Color background)
    {
        var rect = FoldingRect;
        if (rect.Width < 1)
        {
            return;
        }

        var pull = new Point(_pointer.X - _grab.X, _pointer.Y - _grab.Y);
        if (Math.Abs(pull.X) < 4 && Math.Abs(pull.Y) < 4)
        {
            if (IsSpread)
            {
                DrawDogEar(context, foreground, paper);
            }

            return;
        }

        var normal = SpecialUi.Normalize(pull);
        var mid = new Point((_grab.X + _pointer.X) / 2, (_grab.Y + _pointer.Y) / 2);
        var offset = SpecialUi.Dot(mid, normal);
        var pagePoly = SpecialUi.RoundedPolygon(rect, 6, 2);
        var remaining = SpecialUi.ClipHalfPlane(pagePoly, normal, offset);
        var folded = SpecialUi.ClipHalfPlane(pagePoly, new Point(-normal.X, -normal.Y), -offset);
        if (remaining.Count < 3 || folded.Count < 3)
        {
            return;
        }

        var flap = folded.Select(point => SpecialUi.Reflect(point, normal, offset)).ToList();

        // Under page: the next spread's page shows where the sheet has lifted.
        var underSpread = _spread + Math.Sign(_direction);
        var underRect = _direction > 0 ? _right : _left;
        if (underSpread is > 0 and < 4)
        {
            var underIndex = _direction > 0 ? 2 * underSpread : 2 * underSpread - 1;
            using (context.PushClip(new RoundedRect(underRect, 6)))
            using (context.PushOpacity(0.55))
            {
                DrawPage(context, underRect, underIndex, foreground, secondary(), mutedColor(), accentColor());
            }
        }

        // Current sheet, only up to the fold.
        using (context.PushGeometryClip(SpecialUi.Polygon(remaining)))
        {
            DrawPage(context, rect, _direction > 0 ? RightPage : LeftPage, foreground, secondary(), mutedColor(), accentColor());
            context.DrawRectangle(null, new Pen(SpecialUi.Solid(ArcColor.WithAlpha(foreground, 0.10)), 1), rect, 6, 6);
        }

        // Shadow of the lifted sheet on the page below.
        var shadowPoly = flap.Select(point => new Point(point.X + 5, point.Y + 7)).ToList();
        context.DrawGeometry(SpecialUi.ShadowBrush(0.22), null, SpecialUi.Polygon(shadowPoly));

        // Paper back, with a lit crease and a soft shade at the tip.
        var back = ArcColor.Mix(SpecialUi.Color(ArcTokens.Surface), background, 0.18);
        context.DrawGeometry(
            SpecialUi.Gradient(ArcColor.Mix(back, SpecialUi.Color(ArcTokens.Surface), 0.5), back, false),
            null,
            SpecialUi.Polygon(flap));
        context.DrawGeometry(
            null,
            new Pen(SpecialUi.Solid(ArcColor.WithAlpha(foreground, 0.20)), 1),
            SpecialUi.Polygon(flap));

        // The crease itself catches the light.
        var crease = CreasePoints(rect, normal, offset);
        if (crease is not null)
        {
            context.DrawLine(
                new Pen(SpecialUi.Solid(ArcColor.WithAlpha(background, 0.8)), 2.5, null, PenLineCap.Round, PenLineJoin.Round),
                crease.Value.A,
                crease.Value.B);
            context.DrawLine(
                new Pen(SpecialUi.Solid(ArcColor.WithAlpha(foreground, 0.16)), 1),
                crease.Value.A,
                crease.Value.B);
        }
    }

    private static Color secondary() => SpecialUi.Color(ArcTokens.TextSecondary);

    private static Color mutedColor() => SpecialUi.Color(ArcTokens.TextMuted);

    private static Color accentColor() => SpecialUi.Color(ArcTokens.Accent);

    private void DrawPage(DrawingContext context, Rect page, int index, Color foreground, Color secondary, Color muted, Color accent)
    {
        if (index < 0 || page.Width < 1)
        {
            return;
        }

        var padding = 18.0;
        var content = page.Deflate(padding);
        var issue = index switch
        {
            0 => "Cover",
            7 => "Back cover",
            _ => "Field notes"
        };

        switch (index)
        {
            case 0:
            {
                var cover = page;
                context.DrawRectangle(SpecialUi.Gradient(accent, ArcColor.Mix(accent, foreground, 0.35)), null, cover);
                var title = SpecialUi.Text("Field notes", 30, SpecialUi.Brush(ArcTokens.AccentForeground), FontWeight.Medium, cover.Width - 36, 2);
                context.DrawText(title, new Point(cover.X + 20, cover.Y + cover.Height * 0.52));
                var subtitle = SpecialUi.Text("Issue 04  ·  Autumn", 12, SpecialUi.Alpha(ArcTokens.AccentForeground, 0.8));
                context.DrawText(subtitle, new Point(cover.X + 20, cover.Y + cover.Height * 0.52 + title.Height + 6));
                context.DrawEllipse(null, new Pen(SpecialUi.Alpha(ArcTokens.AccentForeground, 0.7), 1.5), new Point(cover.Right - 54, cover.Y + 62), 26, 26);
                context.DrawEllipse(null, new Pen(SpecialUi.Alpha(ArcTokens.AccentForeground, 0.4), 1), new Point(cover.Right - 54, cover.Y + 62), 36, 36);
                break;
            }
            case 1:
            {
                context.DrawText(SpecialUi.Text("Contents", 20, SpecialUi.Solid(foreground), FontWeight.Medium), new Point(content.X, content.Y + 4));
                var entries = new[] { ("The long way north", "04"), ("Notes from the coast", "12"), ("After dark", "20"), ("A quiet index", "28") };
                var y = content.Y + 46;
                foreach (var (labelText, number) in entries)
                {
                    context.DrawText(SpecialUi.Text(labelText, 12, SpecialUi.Solid(foreground)), new Point(content.X, y));
                    var num = SpecialUi.Text(number, 11, SpecialUi.Solid(muted));
                    context.DrawText(num, new Point(content.Right - num.Width, y));
                    context.DrawLine(
                        new Pen(SpecialUi.Solid(ArcColor.WithAlpha(foreground, 0.08)), 1),
                        new Point(content.X, y + 20),
                        new Point(content.Right, y + 20));
                    y += 40;
                }

                break;
            }
            case 2:
            {
                var image = new Rect(content.X, content.Y, content.Width, content.Height * 0.46);
                SampleArt.Draw(context, image, 5, 4);
                var title = SpecialUi.Text("The long way north", 18, SpecialUi.Solid(foreground), FontWeight.Medium, content.Width, 2);
                context.DrawText(title, new Point(content.X, image.Bottom + 14));
                DrawLines(context, new Rect(content.X, image.Bottom + 14 + title.Height + 10, content.Width, content.Height * 0.3), 6, ArcColor.WithAlpha(foreground, 0.22), 12);
                break;
            }
            case 3:
            {
                SampleArt.Draw(context, page, 9, 0);
                var caption = SpecialUi.Text("Plate II  ·  After the rain", 11, SpecialUi.Brush(ArcTokens.Surface));
                context.DrawText(caption, new Point(page.X + 16, page.Bottom - 26));
                break;
            }
            case 4:
            {
                context.DrawText(SpecialUi.Text("Notes from the coast", 18, SpecialUi.Solid(foreground), FontWeight.Medium), new Point(content.X, content.Y + 2));
                var column = (content.Width - 10) / 2;
                DrawLines(context, new Rect(content.X, content.Y + 34, column, content.Height * 0.62), 9, ArcColor.WithAlpha(foreground, 0.22), 12);
                DrawLines(context, new Rect(content.X + column + 10, content.Y + 34, column, content.Height * 0.62), 9, ArcColor.WithAlpha(foreground, 0.16), 12);
                var folio = SpecialUi.Text("05", 10, SpecialUi.Solid(muted));
                context.DrawText(folio, new Point(content.Right - folio.Width, content.Bottom - 14));
                break;
            }
            case 5:
            {
                var image = new Rect(content.X + content.Width * 0.48, content.Y, content.Width * 0.52, content.Height);
                SampleArt.Draw(context, image, 6, 4);
                var title = SpecialUi.Text("Salt air", 16, SpecialUi.Solid(foreground), FontWeight.Medium, content.Width * 0.42, 2);
                context.DrawText(title, new Point(content.X, content.Y + 8));
                DrawLines(context, new Rect(content.X, content.Y + 8 + title.Height + 12, content.Width * 0.42, content.Height * 0.45), 8, ArcColor.WithAlpha(foreground, 0.20), 12);
                break;
            }
            case 6:
            {
                SampleArt.Draw(context, page, 8, 0);
                var title = SpecialUi.Text("After dark", 24, SpecialUi.Brush(ArcTokens.Surface), FontWeight.Medium);
                context.DrawText(title, new Point(page.X + 18, page.Bottom - 54));
                var caption = SpecialUi.Text("Night crossing, 11:40", 11, SpecialUi.Alpha(ArcTokens.Surface, 0.75));
                context.DrawText(caption, new Point(page.X + 18, page.Bottom - 28));
                break;
            }
            default:
            {
                context.DrawRectangle(SpecialUi.Solid(ArcColor.Mix(SpecialUi.Color(ArcTokens.Surface), accent, 0.08)), null, page);
                var title = SpecialUi.Text("Field notes", 22, SpecialUi.Solid(foreground), FontWeight.Medium);
                SpecialUi.DrawTextCentered(context, title, new Rect(page.X, page.Center.Y - 24, page.Width, 30));
                var subtitle = SpecialUi.Text("Thanks for reading", 12, SpecialUi.Solid(secondary));
                SpecialUi.DrawTextCentered(context, subtitle, new Rect(page.X, page.Center.Y + 6, page.Width, 20));
                context.DrawEllipse(null, new Pen(SpecialUi.Solid(ArcColor.WithAlpha(foreground, 0.2)), 1), new Point(page.Center.X, page.Center.Y + 54), 14, 14);
                break;
            }
        }

        _ = issue;
    }

    private static void DrawLines(DrawingContext context, Rect area, int lines, Color color, double gap)
    {
        var pen = new Pen(SpecialUi.Solid(color), 2.5, null, PenLineCap.Round, PenLineJoin.Round);
        for (var index = 0; index < lines; index++)
        {
            var y = area.Y + index * gap;
            var width = area.Width * (0.62 + SpecialUi.Noise(index * 7 + (int)area.Y) * 0.38);
            context.DrawLine(pen, new Point(area.X, y), new Point(area.X + width, y));
        }
    }

    private static (Point A, Point B)? CreasePoints(Rect rect, Point normal, double offset)
    {
        var points = new List<Point>();
        if (Math.Abs(normal.Y) > 0.0001)
        {
            foreach (var x in new[] { rect.X, rect.Right })
            {
                var y = (offset - x * normal.X) / normal.Y;
                if (y >= rect.Y - 0.5 && y <= rect.Bottom + 0.5)
                {
                    points.Add(new Point(x, Math.Clamp(y, rect.Y, rect.Bottom)));
                }
            }
        }

        if (Math.Abs(normal.X) > 0.0001)
        {
            foreach (var y in new[] { rect.Y, rect.Bottom })
            {
                var x = (offset - y * normal.Y) / normal.X;
                if (x >= rect.X - 0.5 && x <= rect.Right + 0.5)
                {
                    points.Add(new Point(Math.Clamp(x, rect.X, rect.Right), y));
                }
            }
        }

        if (points.Count < 2)
        {
            return null;
        }

        return (points[0], points[^1]);
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
