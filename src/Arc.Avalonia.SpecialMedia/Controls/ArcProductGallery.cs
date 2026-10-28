using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMedia.Controls;

/// <summary>
/// Arc's product gallery: one large product photo with a hover magnifier, a gliding thumbnail rail, colour
/// and size variants that crossfade the photos, and a fullscreen zoom viewer. Photos are in-code sample
/// imagery; the colour swatches re-tint the same four shots so a variant change reads as one product in
/// another colourway.
/// </summary>
public sealed class ArcProductGallery : ArcSpecialCanvas
{
    private sealed record Colorway(string Name, double Hue, string[] SoldOut);

    private static readonly Colorway[] Colors =
    {
        new("Olive", 108, Array.Empty<string>()),
        new("Clay", 32, new[] { "M" }),
        new("Indigo", 268, new[] { "S" })
    };

    private static readonly string[] Sizes = { "S", "M", "L" };
    private static readonly string[] PhotoLabels = { "Front", "Back", "Detail", "Folded" };
    private static readonly int[] Seeds = { 2, 6, 0, 9 };
    private const double Zoom = 2.4;
    private const double LensRadius = 64;

    private readonly Rect[] _thumbs = new Rect[4];
    private readonly Rect[] _swatches = new Rect[3];
    private readonly Rect[] _sizes = new Rect[3];
    private Rect _main;
    private Rect _bag;
    private Rect _details;
    private Rect _viewerPhoto;
    private Rect _viewerClose;
    private Rect _viewerPrev;
    private Rect _viewerNext;
    private Size _layoutSize;

    private int _color;
    private int _photo;
    private int _size = 1;
    private int _prevSeed = Seeds[0];
    private double _prevHue = Colors[0].Hue;
    private Spring _fade = Spring.At(1, 190, 24);
    private Spring _thumbGlide = Spring.At(0, 190, 26);
    private Spring _lensX = Spring.At(0, 260, 30);
    private Spring _lensY = Spring.At(0, 260, 30);
    private Spring _viewer = Spring.At(0, 210, 28);
    private bool _viewerOpen;
    private bool _added;
    private bool _lensOn;
    private Point _lensTarget;
    private int _focusGroup;
    private int _focusIndex;

    private bool _lensPlaced;

    public ArcProductGallery()
    {
        SpecialUi.Name(
            this,
            "Product gallery",
            "A product photo with a hover magnifier, a thumbnail rail, colour and size options and a zoom viewer. " +
            "Use the arrow keys to move within a group and Up and Down to change group; Enter opens the viewer.");
    }

    protected override double DefaultWidth => 840;

    protected override double DefaultHeight => 460;

    private Colorway Current => Colors[_color];

    private bool IsSoldOut(string size) => Array.IndexOf(Current.SoldOut, size) >= 0;

    private void BuildLayout(Size size)
    {
        if (_layoutSize == size)
        {
            return;
        }

        _layoutSize = size;
        var left = 20.0;
        var top = 20.0;
        _main = new Rect(left, top, 300, 358);
        for (var index = 0; index < _thumbs.Length; index++)
        {
            _thumbs[index] = new Rect(left + index * 65, _main.Bottom + 14, 57, 44);
        }

        _details = new Rect(_main.Right + 32, top, Math.Max(120, size.Width - _main.Right - 52), size.Height - 40);
        var width = _details.Width;
        for (var index = 0; index < _swatches.Length; index++)
        {
            _swatches[index] = new Rect(_details.X + index * 44, _details.Y + 126, 30, 30);
        }

        for (var index = 0; index < _sizes.Length; index++)
        {
            _sizes[index] = new Rect(_details.X + index * 56, _details.Y + 202, 48, 36);
        }

        _bag = new Rect(_details.X, _details.Y + 268, width, 46);

        var photoHeight = Math.Min(size.Height - 90, 380);
        var photoWidth = photoHeight * 0.8;
        _viewerPhoto = new Rect((size.Width - photoWidth) / 2, (size.Height - photoHeight) / 2 - 6, photoWidth, photoHeight);
        _viewerClose = new Rect(_viewerPhoto.Right - 14, _viewerPhoto.Y - 14, 32, 32);
        _viewerPrev = new Rect(_viewerPhoto.X - 52, _viewerPhoto.Center.Y - 20, 40, 40);
        _viewerNext = new Rect(_viewerPhoto.Right + 12, _viewerPhoto.Center.Y - 20, 40, 40);
    }

    private void ChangePhoto(int index)
    {
        if (index == _photo)
        {
            return;
        }

        RememberCurrent();
        _photo = (index + Seeds.Length) % Seeds.Length;
        _fade.Jump(0);
        StartFade();
    }

    private void ChangeColor(int index)
    {
        if (index == _color)
        {
            return;
        }

        RememberCurrent();
        _color = (index + Colors.Length) % Colors.Length;
        if (IsSoldOut(Sizes[Math.Max(0, _size)]))
        {
            _size = -1;
        }

        _fade.Jump(0);
        StartFade();
    }

    private void ChangeSize(int index) => _size = index;

    private void RememberCurrent()
    {
        _prevSeed = Seeds[_photo];
        _prevHue = Current.Hue;
    }

    private void StartFade()
    {
        EnsureFrame();
        InvalidateVisual();
    }

    private void OpenViewer()
    {
        _viewerOpen = true;
        _viewer.Jump(SpecialUi.Reduced ? 1 : 0);
        EnsureFrame();
    }

    private void CloseViewer()
    {
        _viewerOpen = false;
        _viewer.Jump(0);
        EnsureFrame();
    }

    protected override void OnFrame(double dt)
    {
        _fade.Step(1, dt);
        _thumbGlide.Step(_photo, dt);
        _lensX.Step(_lensTarget.X, dt);
        _lensY.Step(_lensTarget.Y, dt);
        _viewer.Step(_viewerOpen ? 1 : 0, dt);
    }

    protected override bool NeedsFrame =>
        Math.Abs(_fade.Value - 1) > 0.002 ||
        Math.Abs(_thumbGlide.Value - _photo) > 0.01 ||
        Math.Abs(_lensX.Value - _lensTarget.X) > 0.2 ||
        Math.Abs(_lensY.Value - _lensTarget.Y) > 0.2 ||
        Math.Abs(_viewer.Value - (_viewerOpen ? 1 : 0)) > 0.002;

    protected override void OnHoverChanged()
    {
        _lensOn = true;
        if (Hovering)
        {
            _lensTarget = ClampLens(Pointer);
            if (SpecialUi.Reduced)
            {
                _lensX.Jump(_lensTarget.X);
                _lensY.Jump(_lensTarget.Y);
            }
        }
        else
        {
            // A curated resting lens position keeps the magnifier legible in a still frame.
            _lensTarget = new Point(_main.X + _main.Width * 0.52, _main.Y + _main.Height * 0.44);
            _lensX.Jump(_lensTarget.X);
            _lensY.Jump(_lensTarget.Y);
        }
    }

    private void PlaceLens()
    {
        if (_lensPlaced)
        {
            return;
        }

        _lensPlaced = true;
        _lensOn = true;
        _lensTarget = new Point(_main.X + _main.Width * 0.52, _main.Y + _main.Height * 0.44);
        _lensX.Jump(_lensTarget.X);
        _lensY.Jump(_lensTarget.Y);
        _thumbGlide.Jump(_photo);
    }

    private Point ClampLens(Point point)
    {
        return new Point(
            Math.Clamp(point.X, _main.X + 10, _main.Right - 10),
            Math.Clamp(point.Y, _main.Y + 10, _main.Bottom - 10));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        BuildLayout(Bounds.Size);
        _lensOn = _main.Contains(point) && !_viewerOpen;
        if (_lensOn)
        {
            _lensTarget = ClampLens(point);
            EnsureFrame();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        var point = e.GetPosition(this);
        BuildLayout(Bounds.Size);

        if (_viewerOpen)
        {
            if (_viewerClose.Contains(point))
            {
                CloseViewer();
            }
            else if (_viewerPrev.Contains(point))
            {
                ChangePhoto(_photo - 1);
            }
            else if (_viewerNext.Contains(point))
            {
                ChangePhoto(_photo + 1);
            }
            else if (!_viewerPhoto.Contains(point))
            {
                CloseViewer();
            }

            e.Handled = true;
            return;
        }

        if (_main.Contains(point) || _main.Inflate(6).Contains(point))
        {
            _focusGroup = 0;
            OpenViewer();
        }
        else
        {
            for (var index = 0; index < _thumbs.Length; index++)
            {
                if (_thumbs[index].Contains(point))
                {
                    _focusGroup = 1;
                    _focusIndex = index;
                    ChangePhoto(index);
                    break;
                }
            }

            for (var index = 0; index < _swatches.Length; index++)
            {
                if (_swatches[index].Inflate(6).Contains(point))
                {
                    _focusGroup = 2;
                    _focusIndex = index;
                    ChangeColor(index);
                    break;
                }
            }

            for (var index = 0; index < _sizes.Length; index++)
            {
                if (_sizes[index].Contains(point) && !IsSoldOut(Sizes[index]))
                {
                    _focusGroup = 3;
                    _focusIndex = index;
                    ChangeSize(index);
                    break;
                }
            }

            if (_bag.Contains(point))
            {
                _focusGroup = 4;
                _added = true;
            }
        }

        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_viewerOpen)
        {
            return;
        }

        ChangePhoto(_photo + (e.Delta.Y < 0 ? 1 : -1));
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_viewerOpen)
        {
            switch (e.Key)
            {
                case Key.Left:
                    ChangePhoto(_photo - 1);
                    e.Handled = true;
                    break;
                case Key.Right:
                    ChangePhoto(_photo + 1);
                    e.Handled = true;
                    break;
                case Key.Escape:
                    CloseViewer();
                    e.Handled = true;
                    break;
            }

            return;
        }

        switch (e.Key)
        {
            case Key.Up:
                _focusGroup = (_focusGroup + 4) % 5;
                _focusIndex = 0;
                e.Handled = true;
                break;
            case Key.Down:
                _focusGroup = (_focusGroup + 1) % 5;
                _focusIndex = 0;
                e.Handled = true;
                break;
            case Key.Left:
                if (_focusGroup != 0)
                {
                    _focusIndex = Math.Max(0, _focusIndex - 1);
                    e.Handled = true;
                }

                break;
            case Key.Right:
                if (_focusGroup != 0)
                {
                    _focusIndex = Math.Min(GroupCount(_focusGroup) - 1, _focusIndex + 1);
                    e.Handled = true;
                }

                break;
            case Key.Enter:
            case Key.Space:
                ActivateFocus();
                e.Handled = true;
                break;
        }

        InvalidateVisual();
    }

    private int GroupCount(int group) => group switch
    {
        1 => _thumbs.Length,
        2 => _swatches.Length,
        3 => _sizes.Length,
        _ => 1
    };

    private void ActivateFocus()
    {
        switch (_focusGroup)
        {
            case 0:
                OpenViewer();
                break;
            case 1:
                ChangePhoto(_focusIndex);
                break;
            case 2:
                ChangeColor(_focusIndex);
                break;
            case 3:
                if (!IsSoldOut(Sizes[Math.Clamp(_focusIndex, 0, 2)]))
                {
                    ChangeSize(_focusIndex);
                }

                break;
            case 4:
                _added = true;
                break;
        }

        InvalidateVisual();
    }

    private Rect FocusRegion()
    {
        BuildLayout(Bounds.Size);
        return _focusGroup switch
        {
            1 => _thumbs[Math.Clamp(_focusIndex, 0, _thumbs.Length - 1)],
            2 => _swatches[Math.Clamp(_focusIndex, 0, _swatches.Length - 1)].Inflate(5),
            3 => _sizes[Math.Clamp(_focusIndex, 0, _sizes.Length - 1)],
            4 => _bag,
            _ => _main
        };
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        BuildLayout(size);
        PlaceLens();
        var surface = SpecialUi.Brush(ArcTokens.Surface);
        var border = SpecialUi.Brush(ArcTokens.Border);
        var foreground = SpecialUi.Color(ArcTokens.Foreground);
        var secondary = SpecialUi.Brush(ArcTokens.TextSecondary);
        var muted = SpecialUi.Brush(ArcTokens.TextMuted);
        var accent = SpecialUi.Color(ArcTokens.Accent);

        var panel = new Rect(0, 0, size.Width, size.Height);
        SpecialUi.RoundRect(context, panel, 30, surface);
        context.DrawRectangle(null, new Pen(border, 1), panel.Deflate(0.5), 30, 30);

        // --- Main photo, with a colour crossfade under the new shot ---
        using (context.PushClip(new RoundedRect(_main, 26)))
        {
            if (_fade.Value < 0.999)
            {
                SampleArt.Draw(context, _main, _prevSeed, 26, _prevHue);
            }

            using (context.PushOpacity(Math.Clamp(_fade.Value, 0, 1)))
            {
                SampleArt.Draw(context, _main, Seeds[_photo], 26, Current.Hue);
            }
        }

        context.DrawRectangle(null, new Pen(SpecialUi.Alpha(ArcTokens.Border, 0.8), 1), _main, 26, 26);

        DrawLens(context, foreground);

        // --- Thumbnail rail ---
        var indicator = new Rect(
            SpecialUi.Lerp(_thumbs[0].X, _thumbs[^1].X, _thumbs.Length == 1 ? 0 : _thumbGlide.Value / (_thumbs.Length - 1)),
            _thumbs[0].Bottom + 3,
            57,
            3);
        context.DrawRectangle(SpecialUi.Brush(ArcTokens.Accent), null, indicator, 2, 2);

        for (var index = 0; index < _thumbs.Length; index++)
        {
            var thumb = _thumbs[index];
            using (context.PushClip(new RoundedRect(thumb, 10)))
            {
                SampleArt.Draw(context, thumb, Seeds[index], 10, Current.Hue, false);
            }

            var active = index == _photo;
            context.DrawRectangle(
                null,
                active ? new Pen(SpecialUi.Brush(ArcTokens.Accent), 2) : new Pen(border, 1),
                active ? thumb.Inflate(1) : thumb.Deflate(0.5),
                11,
                11);
        }

        // --- Details column ---
        var title = SpecialUi.Text("Field jacket", 26, SpecialUi.Brush(ArcTokens.Foreground), FontWeight.Medium);
        context.DrawText(title, new Point(_details.X, _details.Y + 2));
        context.DrawText(SpecialUi.Text("Waxed cotton chore jacket", 13, secondary), new Point(_details.X, _details.Y + 40));
        context.DrawText(SpecialUi.Text("$240", 18, SpecialUi.Brush(ArcTokens.Foreground), FontWeight.Medium), new Point(_details.X, _details.Y + 62));
        context.DrawLine(new Pen(border, 1), new Point(_details.X, _details.Y + 100), new Point(_details.Right, _details.Y + 100));

        context.DrawText(SpecialUi.Text($"Colour  ·  {Current.Name}", 12, secondary), new Point(_details.X, _details.Y + 108));
        for (var index = 0; index < Colors.Length; index++)
        {
            var rect = _swatches[index];
            var center = rect.Center;
            var color = ArcColor.Oklch(0.63, 0.10, Colors[index].Hue);
            context.DrawEllipse(SpecialUi.Solid(color), null, center, 13, 13);
            if (index == _color)
            {
                context.DrawEllipse(null, new Pen(SpecialUi.Brush(ArcTokens.Foreground), 1.5), center, 16.5, 16.5);
            }
            else
            {
                context.DrawEllipse(null, new Pen(SpecialUi.Alpha(ArcTokens.BorderStrong, 0.8), 1), center, 14.5, 14.5);
            }

            if (Hovering && rect.Inflate(6).Contains(Pointer))
            {
                context.DrawEllipse(null, new Pen(SpecialUi.Brush(ArcTokens.Accent), 1.5), center, 18, 18);
            }
        }

        context.DrawText(SpecialUi.Text("Size", 12, secondary), new Point(_details.X, _details.Y + 176));
        var guide = SpecialUi.Text("Size guide", 12, secondary);
        context.DrawText(guide, new Point(_details.Right - guide.Width, _details.Y + 176));

        for (var index = 0; index < Sizes.Length; index++)
        {
            var rect = _sizes[index];
            var soldOut = IsSoldOut(Sizes[index]);
            var active = index == _size;
            var fill = active ? SpecialUi.Brush(ArcTokens.Accent) : surface;
            var line = active ? SpecialUi.Brush(ArcTokens.Accent) : border;
            SpecialUi.RoundRect(context, rect, 12, fill);
            context.DrawRectangle(null, new Pen(line, 1), rect.Deflate(0.5), 12, 12);
            var label = SpecialUi.Text(Sizes[index], 13, active ? SpecialUi.Brush(ArcTokens.AccentForeground) : soldOut ? muted : SpecialUi.Brush(ArcTokens.Foreground), FontWeight.Medium);
            SpecialUi.DrawTextCentered(context, label, rect);
            if (soldOut)
            {
                context.DrawLine(
                    new Pen(muted, 1),
                    new Point(rect.X + 10, rect.Center.Y + 8),
                    new Point(rect.Right - 10, rect.Center.Y - 8));
            }
        }

        var bagEnabled = _size >= 0;
        var bagFill = bagEnabled ? SpecialUi.Brush(ArcTokens.Accent) : SpecialUi.Brush(ArcTokens.SurfaceMuted);
        SpecialUi.RoundRect(context, _bag, 24, bagFill);
        var bagLabel = _added
            ? "Added to bag"
            : bagEnabled ? "Add to bag" : "Select a size";
        var bagText = SpecialUi.Text(bagLabel, 14, bagEnabled ? SpecialUi.Brush(ArcTokens.AccentForeground) : muted, FontWeight.Medium);
        SpecialUi.DrawTextCentered(context, bagText, _bag);
        if (_added)
        {
            SpecialIcons.Draw(context, "check", new Rect(_bag.Center.X - bagText.Width / 2 - 22, _bag.Center.Y - 8, 16, 16), SpecialUi.Brush(ArcTokens.AccentForeground));
        }

        var note = _bagEnabledNote();
        context.DrawText(SpecialUi.Text(note, 12, muted), new Point(_details.X, _details.Y + 328));
        context.DrawText(
            SpecialUi.Text("Hover to magnify  ·  Enter to open", 12, muted),
            new Point(_details.X, _details.Y + 380));

        // --- Focus ring ---
        if (ShowFocusRing)
        {
            SpecialUi.FocusRing(context, FocusRegion(), 14);
        }

        // --- Fullscreen zoom viewer ---
        if (_viewer.Value > 0.01)
        {
            DrawViewer(context, size);
        }
    }

    private string _bagEnabledNote()
    {
        if (_size < 0)
        {
            return "Choose a size to add this colourway.";
        }

        return IsSoldOut(Sizes[_size])
            ? "That size is sold out in this colour."
            : "Free returns within 30 days.";
    }

    private void DrawLens(DrawingContext context, Color foreground)
    {
        if (!_lensOn || _viewerOpen || _fade.Value < 0.9)
        {
            return;
        }

        var point = new Point(_lensX.Value, _lensY.Value);
        if (!_main.Contains(point))
        {
            return;
        }

        var circle = new Rect(point.X - LensRadius, point.Y - LensRadius, LensRadius * 2, LensRadius * 2);
        using (context.PushGeometryClip(new EllipseGeometry(circle)))
        {
            var art = new Rect(
                point.X + (_main.X - point.X) * Zoom,
                point.Y + (_main.Y - point.Y) * Zoom,
                _main.Width * Zoom,
                _main.Height * Zoom);
            SampleArt.Draw(context, art, Seeds[_photo], 26, Current.Hue, true);
        }

        context.DrawEllipse(null, new Pen(SpecialUi.Brush(ArcTokens.Surface), 3), point, LensRadius, LensRadius);
        context.DrawEllipse(null, new Pen(SpecialUi.Alpha(foreground, 0.25), 1), point, LensRadius + 2, LensRadius + 2);

        var badge = new Rect(point.X - 22, point.Y + LensRadius - 24, 44, 18);
        SpecialUi.RoundRect(context, badge, 9, SpecialUi.Alpha(ArcTokens.Surface, 0.85));
        SpecialUi.DrawTextCentered(context, SpecialUi.Text("2.4×", 10, SpecialUi.Solid(foreground), FontWeight.Medium), badge);
    }

    private void DrawViewer(DrawingContext context, Size size)
    {
        var t = Math.Clamp(_viewer.Value, 0, 1);
        var scrim = new Rect(0, 0, size.Width, size.Height);
        using (context.PushOpacity(t))
        {
            SpecialUi.RoundRect(context, scrim, 30, SpecialUi.Brush(ArcTokens.Scrim));
        }

        var photo = _viewerPhoto;
        var interpolated = new Rect(
            photo.X + (1 - t) * 12,
            photo.Y + (1 - t) * 10,
            photo.Width,
            photo.Height);
        using (context.PushOpacity(t))
        {
            SpecialUi.Lift(context, interpolated, 24, 1.4);
            using (context.PushClip(new RoundedRect(interpolated, 22)))
            {
                SampleArt.Draw(context, interpolated, Seeds[_photo], 22, Current.Hue);
            }

            context.DrawRectangle(null, new Pen(SpecialUi.Alpha(ArcTokens.Surface, 0.4), 1), interpolated, 22, 22);

            var caption = SpecialUi.Text($"{Current.Name}  ·  {PhotoLabels[_photo]}  ·  {_photo + 1} of {Seeds.Length}", 13, SpecialUi.Brush(ArcTokens.Surface));
            SpecialUi.DrawTextCentered(context, caption, new Rect(photo.X, photo.Bottom + 12, photo.Width, caption.Height));

            var button = SpecialUi.Brush(ArcTokens.Surface);
            var ink = SpecialUi.Color(ArcTokens.Foreground);
            SpecialUi.RoundRect(context, _viewerClose, 16, button);
            SpecialIcons.Draw(context, "x", _viewerClose.Deflate(8), SpecialUi.Brush(ArcTokens.Foreground), 2);
            if (_viewerPrev.X > 0)
            {
                SpecialUi.RoundRect(context, _viewerPrev, 20, button);
                SpecialIcons.Draw(context, "chevron-left", _viewerPrev.Deflate(10), SpecialUi.Brush(ArcTokens.Foreground), 2);
            }

            SpecialUi.RoundRect(context, _viewerNext, 20, button);
            SpecialIcons.Draw(context, "chevron-right", _viewerNext.Deflate(10), SpecialUi.Brush(ArcTokens.Foreground), 2);
        }
    }
}
