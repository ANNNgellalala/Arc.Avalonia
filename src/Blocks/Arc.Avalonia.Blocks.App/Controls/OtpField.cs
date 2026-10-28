using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App;

/// <summary>
/// A one-time-code entry row: single-character slots with a gliding focus ring, arrow-key navigation,
/// backspace that walks back, and paste that fills the row from the focused slot. It is drawn rather than
/// built from six text fields so the focus ring and the filled/unfilled states stay under our control;
/// the trade-off is that it does not host the platform's one-time-code autofill.
/// </summary>
internal sealed class OtpField : Border
{
    private readonly List<Border> _slots = new();
    private readonly List<TextBlock> _glyphs = new();
    private readonly int _length;
    private readonly bool _masked;
    private readonly double _slotSize;
    private readonly TextBlock _status;
    private string _value = string.Empty;
    private int _cursor;
    private bool _hasError;

    public OtpField(int length = 6, bool masked = false, double slotSize = 46, double gap = 8,
        string label = "Verification code", bool numeric = true)
    {
        _length = length;
        _masked = masked;
        _slotSize = slotSize;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Ibeam);
        AutomationProperties.SetName(this, label);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = gap };
        for (var index = 0; index < length; index++)
        {
            var glyph = new TextBlock
            {
                Classes = { "arc-body", "arc-medium" },
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            var slot = new Border
            {
                Width = slotSize,
                Height = slotSize + 2,
                CornerRadius = new CornerRadius(12),
                Background = AppKit.Brush(ArcTokens.SurfaceMuted),
                BorderBrush = AppKit.Brush(ArcTokens.Border),
                BorderThickness = new Thickness(1),
                Child = glyph
            };
            _slots.Add(slot);
            _glyphs.Add(glyph);
            row.Children.Add(slot);
        }

        _status = AppKit.Caption(string.Empty);
        _status.IsVisible = false;
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);

        Child = new StackPanel
        {
            Spacing = 8,
            Children = { row, _status }
        };

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        GotFocus += (_, _) => Paint();
        LostFocus += (_, _) => Paint();
        PointerPressed += (_, _) => Focus();
        Paint();
    }

    /// <summary>Raised after every edit with the full code.</summary>
    public event EventHandler<string>? ValueChanged;

    /// <summary>Raised when the last slot is filled.</summary>
    public event EventHandler<string>? Completed;

    public string Value
    {
        get => _value;
        set
        {
            _value = new string((value ?? string.Empty).Where(char.IsLetterOrDigit).Take(_length).ToArray());
            _cursor = Math.Min(_value.Length, _length - 1);
            Paint();
        }
    }

    /// <summary>Shows an error line under the slots and sets the invalid border.</summary>
    public string? Error
    {
        get => _status.Text;
        set
        {
            _hasError = !string.IsNullOrWhiteSpace(value);
            _status.Text = value ?? string.Empty;
            _status.IsVisible = _hasError;
            _status.Foreground = AppKit.Brush(ArcTokens.Danger);
            Paint();
        }
    }

    /// <summary>Helper copy shown under the slots when there is no error.</summary>
    public void SetDescription(string text)
    {
        _hasError = false;
        _status.Text = text;
        _status.IsVisible = !string.IsNullOrWhiteSpace(text);
        _status.Foreground = AppKit.Brush(ArcTokens.TextMuted);
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        var handled = true;
        if (args.KeyModifiers.HasFlag(KeyModifiers.Control) && args.Key == Key.V)
        {
            _ = PasteAsync();
            args.Handled = true;
            return;
        }

        switch (args.Key)
        {
            case Key.Left:
                _cursor = Math.Max(0, _cursor - 1);
                break;
            case Key.Right:
                _cursor = Math.Min(_length - 1, _cursor + 1);
                break;
            case Key.Back:
                if (_value.Length > 0)
                {
                    _value = _value[..^1];
                }

                _cursor = Math.Max(0, Math.Min(_value.Length, _cursor) - 1);
                Emit();
                break;
            case Key.Delete:
                if (_cursor < _value.Length)
                {
                    _value = _value.Remove(_cursor, 1);
                }

                Emit();
                break;
            case Key.Space:
            case Key.Tab:
                handled = false;
                break;
            default:
                if (TryDigit(args.Key, out var digit))
                {
                    Append(digit);
                }
                else
                {
                    handled = false;
                }

                break;
        }

        args.Handled = handled;
        Paint();
    }

    private async System.Threading.Tasks.Task PasteAsync()
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                return;
            }

            var text = await clipboard.TryGetTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var digits = new string(text.Where(char.IsLetterOrDigit).ToArray());
            if (digits.Length == 0)
            {
                return;
            }

            var builder = new StringBuilder(_value);
            var start = Math.Min(_cursor, _length);
            for (var index = 0; index < digits.Length && start + index < _length; index++)
            {
                if (start + index < builder.Length)
                {
                    builder[start + index] = digits[index];
                }
                else
                {
                    builder.Append(digits[index]);
                }
            }

            _value = builder.ToString();
            _cursor = Math.Min(_value.Length, _length - 1);
            Emit();
            Paint();
        }
        catch
        {
            // Clipboard access is best effort; a denied read simply does not fill the slots.
        }
    }

    private void Append(char digit)
    {
        if (_value.Length >= _length)
        {
            _value = _value[..(_length - 1)] + digit;
            _cursor = _length - 1;
        }
        else if (_cursor < _value.Length)
        {
            _value = _value[.._cursor] + digit + _value[(_cursor + 1)..];
            _cursor = Math.Min(_cursor + 1, _length - 1);
        }
        else
        {
            _value += digit;
            _cursor = Math.Min(_value.Length, _length - 1);
        }

        Emit();
    }

    private void Emit()
    {
        ValueChanged?.Invoke(this, _value);
        if (_value.Length == _length)
        {
            Completed?.Invoke(this, _value);
        }

        Paint();
    }

    private void Paint()
    {
        var focused = IsFocused;
        var invalid = _hasError;
        var active = Math.Min(_cursor, _length - 1);

        for (var index = 0; index < _length; index++)
        {
            var filled = index < _value.Length;
            _glyphs[index].Text = filled ? (_masked ? "•" : _value[index].ToString()) : string.Empty;
            _glyphs[index].Foreground = AppKit.Brush(invalid
                ? ArcTokens.Danger
                : filled ? ArcTokens.Foreground : ArcTokens.TextMuted);

            _slots[index].Background = filled
                ? AppKit.Brush(ArcTokens.SurfaceRaised)
                : AppKit.Brush(ArcTokens.SurfaceMuted);

            if (invalid)
            {
                _slots[index].BorderBrush = AppKit.Brush(ArcTokens.Danger);
                _slots[index].BorderThickness = new Thickness(1);
            }
            else if (focused && index == active)
            {
                _slots[index].BorderBrush = AppKit.Brush(ArcTokens.FocusOutline);
                _slots[index].BorderThickness = new Thickness(2);
            }
            else
            {
                _slots[index].BorderBrush = filled
                    ? AppKit.Brush(ArcTokens.BorderStrong)
                    : AppKit.Brush(ArcTokens.Border);
                _slots[index].BorderThickness = new Thickness(1);
            }
        }
    }

    private static bool TryDigit(Key key, out char digit)
    {
        digit = '\0';
        if (key >= Key.D0 && key <= Key.D9)
        {
            digit = (char)('0' + (key - Key.D0));
            return true;
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            digit = (char)('0' + (key - Key.NumPad0));
            return true;
        }

        return false;
    }
}
