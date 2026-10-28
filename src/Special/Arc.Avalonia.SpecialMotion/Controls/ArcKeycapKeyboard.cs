using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMotion.Controls;

/// <summary>
/// A board of sculpted mechanical keycaps. Typing presses the matching cap into its housing, blooms a faint
/// light under it and adds the character to the wrapping text above the board. Caps can also be pressed with
/// the pointer, or reached with the arrow keys and pressed with Enter. Reducing motion removes cap travel and
/// blinking but keeps the light and the legend colour.
/// </summary>
public class ArcKeycapKeyboard : ArcMotionCanvas
{
    private static readonly string[][] Compact =
    {
        new[] { "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "backspace" },
        new[] { "tab", "q", "w", "e", "r", "t", "y", "u", "i", "o", "p", "[", "]", "\\" },
        new[] { "caps", "a", "s", "d", "f", "g", "h", "j", "k", "l", ";", "'", "enter" },
        new[] { "shift", "z", "x", "c", "v", "b", "n", "m", ",", ".", "/", "shift" },
        new[] { "ctrl", "alt", "cmd", "space", "cmd", "alt", "ctrl" }
    };

    private readonly struct Cap
    {
        public Cap(string label, Rect rect, int row)
        {
            Label = label;
            Rect = rect;
            Row = row;
        }

        public string Label { get; }
        public Rect Rect { get; }
        public int Row { get; }
    }

    private readonly List<Cap> _keys = new();
    private double[] _down = Array.Empty<double>();
    private double[] _lit = Array.Empty<double>();
    private string _text = "hello, arc";
    private string _fitted = string.Empty;
    private int _focusKey;
    private bool _capNavigation;
    private bool _focusedOnce;
    private bool _built;
    private double _builtWidth;
    private double _clock;
    private double _typingAt = -10;

    public ArcKeycapKeyboard()
    {
        Sm.Name(this, "Keyboard", "Type to press the caps. Arrow keys move between caps and Enter presses the focused cap.");
    }

    protected override double DefaultWidth => 960;

    protected override double DefaultHeight => 400;

    /// <summary>The typed text; it wraps above the board.</summary>
    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;
            _fitted = string.Empty;
            InvalidateVisual();
        }
    }

    private void SeedTrail()
    {
        if (_keys.Count == 0)
        {
            return;
        }

        _lit = new double[_keys.Count];
        _down = new double[_keys.Count];
        var trail = new[] { ("a", 0.85), ("r", 0.5), ("c", 0.25) };
        foreach (var (label, amount) in trail)
        {
            var index = IndexOf(label);
            if (index >= 0)
            {
                _lit[index] = amount;
            }
        }
    }

    private int IndexOf(string label)
    {
        for (var index = 0; index < _keys.Count; index++)
        {
            if (string.Equals(_keys[index].Label, label, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static double Units(string key) => key switch
    {
        "backspace" => 2,
        "tab" => 1.5,
        "caps" => 1.75,
        "enter" => 2.25,
        "shift" => 2.25,
        "ctrl" => 1.2,
        "alt" => 1.2,
        "cmd" => 1.25,
        "space" => 6.5,
        _ => 1
    };

    private static string Legend(string key) => key switch
    {
        "backspace" => "del",
        "enter" => "return",
        "space" => string.Empty,
        _ => key
    };

    protected override bool NeedsFrame => base.NeedsFrame || (!Sm.Reduced && (AnyLit() || IsFocused));

    private bool AnyLit()
    {
        foreach (var value in _lit)
        {
            if (value > 0.01)
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnFrame(double dt)
    {
        _clock += dt;
        for (var index = 0; index < _keys.Count && index < _lit.Length; index++)
        {
            _lit[index] = Math.Max(0, _lit[index] - dt / 0.7);
            var pressed = PressId >= 0 && PressId < _keys.Count && _keys[PressId].Label == _keys[index].Label;
            var target = pressed ? 1 : 0;
            _down[index] += (target - _down[index]) * Math.Min(1, dt * 34);
        }
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        if (!_built || Math.Abs(size.Width - _builtWidth) > 0.5)
        {
            Build(size);
        }

        DrawOutput(context, size);
        DrawBoard(context, size);
    }

    private void Build(Size size)
    {
        _built = true;
        _builtWidth = size.Width;
        _keys.Clear();
        _fitted = string.Empty;

        var boardWidth = Math.Min(size.Width - 48, 880);
        var top = 142.0;
        var bottom = size.Height - 18;
        var rowCount = Compact.Length;
        var gap = 6.0;
        var rowHeight = Math.Max(28, (bottom - top - gap * (rowCount - 1)) / rowCount);
        var maxUnits = 15.0;
        var unit = Math.Min(50, (boardWidth - gap * (maxUnits - 1)) / maxUnits);

        for (var row = 0; row < rowCount; row++)
        {
            var labels = Compact[row];
            var units = 0.0;
            foreach (var label in labels)
            {
                units += Units(label);
            }

            var width = units * unit + gap * (labels.Length - 1);
            var x = size.Width / 2 - width / 2;
            var y = top + row * (rowHeight + gap);
            for (var column = 0; column < labels.Length; column++)
            {
                var keyWidth = Units(labels[column]) * unit;
                _keys.Add(new Cap(labels[column], new Rect(x, y, keyWidth, rowHeight), row));
                x += keyWidth + gap;
            }
        }

        _down = new double[_keys.Count];
        _lit = new double[_keys.Count];
        SeedTrail();
        _focusKey = Math.Clamp(_focusKey, 0, Math.Max(0, _keys.Count - 1));
    }

    private void DrawOutput(DrawingContext context, Size size)
    {
        var field = new Rect(48, 18, size.Width - 96, 100);
        context.DrawRectangle(Sm.Brush(ArcTokens.Surface), new Pen(Sm.Brush(ArcTokens.Border), 1), field, 20, 20);

        var inner = field.Deflate(16);
        if (string.IsNullOrEmpty(_text))
        {
            var placeholder = Sm.Text("Start typing", 20, Sm.Brush(ArcTokens.TextMuted));
            context.DrawText(placeholder, new Point(inner.X, inner.Y + 4));
            return;
        }

        if (string.IsNullOrEmpty(_fitted))
        {
            _fitted = Fit(inner.Width, inner.Height);
        }

        var text = Sm.Text(_fitted, 20, Sm.Brush(ArcTokens.Foreground), FontWeight.Normal, inner.Width, 3);
        context.DrawText(text, new Point(inner.X, inner.Y));

        var caretY = inner.Y + Math.Min(Math.Max(0, text.Height - 6), inner.Height - 24);
        var caretX = inner.X + Math.Min(text.Width, inner.Width - 4);
        var idle = _clock - _typingAt > 0.6;
        var blink = Sm.Reduced || !idle ? 1.0 : (Math.Sin(_clock * Math.PI * 2 / 1.06) > -0.2 ? 1.0 : 0.15);
        if (_focusedOnce)
        {
            var ink = Sm.Color(ArcTokens.Accent);
            var pen = new Pen(Sm.Solid(ArcColor.WithAlpha(ink, 0.85 * blink)), 2);
            context.DrawLine(pen, new Point(caretX, caretY), new Point(caretX, caretY + 22));
        }
    }

    private string Fit(double width, double height)
    {
        var value = _text;
        for (var attempt = 0; attempt < 60 && value.Length > 1; attempt++)
        {
            var text = Sm.Text(value, 20, Brushes.Transparent, FontWeight.Normal, width, 3);
            if (text.Height <= height)
            {
                return value;
            }

            value = value[1..];
        }

        return value;
    }

    private void DrawBoard(DrawingContext context, Size size)
    {
        for (var index = 0; index < _keys.Count; index++)
        {
            var key = _keys[index];
            var unit = key.Rect.Height;
            var radius = Math.Max(6, unit * 0.16);
            var lit = index < _lit.Length ? _lit[index] : 0;
            var down = index < _down.Length ? _down[index] : 0;
            var pressed = down > 0.02 || lit > 0.02;

            if (lit > 0.01 || down > 0.01)
            {
                var strength = Math.Max(lit * 0.55, down * 0.8);
                var bloom = new Rect(key.Rect.X - 8, key.Rect.Y + unit * 0.45, key.Rect.Width + 16, unit * 0.75);
                context.DrawRectangle(
                    Sm.Solid(ArcColor.WithAlpha(Sm.Color(ArcTokens.Accent), 0.24 * strength)),
                    null,
                    bloom,
                    radius + 8,
                    radius + 8);
            }

            var sink = down * (Sm.Reduced ? 0 : 3.5);
            var cap = new Rect(key.Rect.X, key.Rect.Y + sink, key.Rect.Width, key.Rect.Height - 7);
            var housing = new Rect(key.Rect.X, key.Rect.Y + 5, key.Rect.Width, key.Rect.Height - 3);

            context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceMuted), new Pen(Sm.Brush(ArcTokens.Border), 1), housing, radius, radius);

            var fill = pressed
                ? Sm.Alpha(ArcTokens.Accent, 0.26 + 0.14 * Math.Max(lit, down))
                : Sm.Brush(ArcTokens.SurfaceRaised);
            var capPen = pressed
                ? new Pen(Sm.Solid(ArcColor.WithAlpha(Sm.Color(ArcTokens.Accent), 0.55 + 0.4 * Math.Max(lit, down))), 1.2)
                : new Pen(Sm.Brush(ArcTokens.Border), 1);
            context.DrawRectangle(fill, capPen, cap, radius, radius);

            var legend = Legend(key.Label);
            if (string.IsNullOrEmpty(legend))
            {
                var bar = new Rect(cap.X + cap.Width * 0.28, cap.Y + cap.Height * 0.62, cap.Width * 0.44, 3);
                context.DrawRectangle(Sm.Brush(ArcTokens.TextMuted), null, bar, 2, 2);
            }
            else
            {
                var modifier = legend.Length > 1;
                var color = pressed
                    ? Sm.Brush(ArcTokens.AccentStrong)
                    : Sm.Brush(modifier ? ArcTokens.TextMuted : ArcTokens.TextSecondary);
                var text = Sm.Text(legend, modifier ? 10 : 12, color, modifier ? FontWeight.Medium : FontWeight.Normal);
                Sm.TextCentered(context, text, cap.Center.X, cap.Center.Y + 1);
            }

            if (ShowFocusRing && index == _focusKey)
            {
                Sm.FocusRing(context, cap, radius);
            }

            Hit(key.Rect, index);
        }
    }

    // ---- Typing ------------------------------------------------------------------------------------

    private void Press(string label)
    {
        var index = IndexOf(label);
        if (index >= 0 && index < _lit.Length)
        {
            _lit[index] = 1;
        }

        _typingAt = _clock;
        switch (label)
        {
            case "backspace":
                if (_text.Length > 0)
                {
                    _text = _text[..^1];
                }

                break;
            case "enter":
                _text += "\n";
                break;
            case "space":
                _text += " ";
                break;
            case "tab":
            case "caps":
            case "shift":
            case "ctrl":
            case "alt":
            case "cmd":
                break;
            default:
                _text += label;
                break;
        }

        if (_text.Length > 280)
        {
            _text = _text[^280..];
        }

        _fitted = string.Empty;
        EnsureFrame();
        InvalidateVisual();
    }

    protected override void OnPress(int id, Point point)
    {
        Focus();
        _focusedOnce = true;
        _capNavigation = true;
        if (id >= 0 && id < _keys.Count)
        {
            _focusKey = id;
            Press(_keys[id].Label);
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        foreach (var character in e.Text)
        {
            if (character == '\r')
            {
                continue;
            }

            var label = character.ToString().ToLowerInvariant();
            if (IndexOf(label) < 0)
            {
                continue;
            }

            if (char.IsUpper(character))
            {
                var shift = IndexOf("shift");
                if (shift >= 0 && shift < _lit.Length)
                {
                    _lit[shift] = 1;
                }
            }

            _text += character;
            var key = IndexOf(label);
            if (key >= 0 && key < _lit.Length)
            {
                _lit[key] = 1;
            }
        }

        if (_text.Length > 280)
        {
            _text = _text[^280..];
        }

        _fitted = string.Empty;
        _capNavigation = false;
        _typingAt = _clock;
        EnsureFrame();
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Back:
                Press("backspace");
                e.Handled = true;
                return;
            case Key.Enter:
                Press(_capNavigation && _focusKey >= 0 && _focusKey < _keys.Count
                    ? _keys[_focusKey].Label
                    : "enter");
                e.Handled = true;
                return;
            case Key.Space:
                Press("space");
                e.Handled = true;
                return;
            case Key.Left:
                MoveFocus(-1, 0);
                e.Handled = true;
                return;
            case Key.Right:
                MoveFocus(1, 0);
                e.Handled = true;
                return;
            case Key.Up:
                MoveFocus(0, -1);
                e.Handled = true;
                return;
            case Key.Down:
                MoveFocus(0, 1);
                e.Handled = true;
                return;
        }
    }

    private void MoveFocus(int dx, int dy)
    {
        if (_keys.Count == 0)
        {
            return;
        }

        _capNavigation = true;
        if (dx != 0)
        {
            _focusKey = (_focusKey + dx + _keys.Count) % _keys.Count;
        }
        else if (dy != 0)
        {
            var current = _keys[_focusKey];
            var row = current.Row + dy;
            if (row >= 0 && row < Compact.Length)
            {
                var best = _focusKey;
                var bestDistance = double.MaxValue;
                for (var index = 0; index < _keys.Count; index++)
                {
                    var candidate = _keys[index];
                    if (candidate.Row != row)
                    {
                        continue;
                    }

                    var distance = Math.Abs(candidate.Rect.Center.X - current.Rect.Center.X);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = index;
                    }
                }

                _focusKey = best;
            }
        }

        EnsureFrame();
        InvalidateVisual();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusedOnce = true;
        EnsureFrame();
    }
}

