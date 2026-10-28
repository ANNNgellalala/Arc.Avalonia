using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Motion.Controls;

/// <summary>Which face of the morphing action surface is showing.</summary>
public enum ArcActionMorphFace
{
    Button,
    Menu,
    Composer,
    Success
}

/// <summary>
/// A floating create button that springs into a quick action menu, slides into an inline composer and folds
/// back into the button with a check on success. One surface animates its width, height and corner radius on
/// the morph spring; faces cross fade inside it, and the menu rows stagger in. Reduced motion jumps the size
/// and fades the faces.
/// </summary>
public class ArcActionMorph : ArcMotionCanvas
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcActionMorph, string?>(nameof(Label), "Create");

    public static readonly StyledProperty<ArcActionMorphFace> FaceProperty =
        AvaloniaProperty.Register<ArcActionMorph, ArcActionMorphFace>(nameof(Face), ArcActionMorphFace.Button);

    public static readonly StyledProperty<bool> CornerStartProperty =
        AvaloniaProperty.Register<ArcActionMorph, bool>(nameof(CornerStart));

    private const int IdButton = 1;
    private const int IdRow = 10;
    private const int IdBack = 20;
    private const int IdChip = 30;
    private const int IdField = 36;
    private const int IdSubmit = 40;
    private const int IdSuccess = 50;

    private static readonly (string Id, string Title, string Description, string Icon, string Shortcut, bool Multiline)[] Actions =
    {
        ("task", "New task", "Add to your list", "circle-check", "T", false),
        ("note", "New note", "Write a note", "pencil", "N", true)
    };

    private static readonly string[] Choices = { "Today", "Tomorrow", "Next week" };

    private ArcActionMorphFace _face = ArcActionMorphFace.Button;
    private ArcActionMorphFace _from = ArcActionMorphFace.Button;
    private double _morph = 1;
    private bool _settled = true;
    private int _menuIndex;
    private int _choice;
    private bool _editing;
    private string _draft = string.Empty;
    private int _action;
    private double _successAlpha;
    private bool _sending;

    static ArcActionMorph()
    {
        FaceProperty.Changed.AddClassHandler<ArcActionMorph>((morph, change) =>
        {
            if (change.NewValue is ArcActionMorphFace face && face != morph._face)
            {
                morph.GoTo(face);
            }
        });
    }

    public ArcActionMorph()
    {
        Sm.Name(this, "Create quick actions", "Enter opens the menu. Arrow keys move, T or N opens a composer, Escape goes back.");
    }

    /// <summary>Accessible name of the button and the title of the menu.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>The visible face; set it to morph between states.</summary>
    public ArcActionMorphFace Face
    {
        get => GetValue(FaceProperty);
        set => SetValue(FaceProperty, value);
    }

    /// <summary>Grows the surface away from the bottom-left corner instead of the bottom-right.</summary>
    public bool CornerStart
    {
        get => GetValue(CornerStartProperty);
        set => SetValue(CornerStartProperty, value);
    }

    public void OpenMenu() => Face = ArcActionMorphFace.Menu;

    public void OpenComposer(string actionId)
    {
        _action = Math.Max(0, Array.FindIndex(Actions, action => action.Id == actionId));
        _editing = true;
        Face = ArcActionMorphFace.Composer;
    }

    public void ShowSuccess() => Face = ArcActionMorphFace.Success;

    private void GoTo(ArcActionMorphFace face)
    {
        _from = _face;
        _face = face;
        _settled = false;
        _morph = 0;
        if (face == ArcActionMorphFace.Composer)
        {
            _editing = true;
            _sending = false;
        }

        if (face == ArcActionMorphFace.Success)
        {
            _sending = false;
        }

        var growing = Metrics(face).W * Metrics(face).H >= Metrics(_from).W * Metrics(_from).H;
        Animate(320, growing ? ArcMotion.SpringMorph : ArcMotion.SpringSmooth, progress =>
        {
            _morph = progress;
            _successAlpha = face == ArcActionMorphFace.Success ? progress : 0;
            InvalidateVisual();
        }, () =>
        {
            _settled = true;
            _successAlpha = face == ArcActionMorphFace.Success ? 1 : 0;
            InvalidateVisual();
        });

        if (face == ArcActionMorphFace.Success && !Sm.Reduced)
        {
            Animate(1600, ArcMotion.EaseStandard, _ => { }, () =>
            {
                if (_face == ArcActionMorphFace.Success)
                {
                    Face = ArcActionMorphFace.Button;
                }
            });
        }
    }

    private static (double W, double H, double R) Metrics(ArcActionMorphFace face) => face switch
    {
        ArcActionMorphFace.Menu => (264, 168, 26),
        ArcActionMorphFace.Composer => (336, 244, 26),
        _ => (56, 56, 28)
    };

    private Rect Shell(Size size)
    {
        var from = Metrics(_from);
        var to = Metrics(_face);
        var eased = _morph;
        var w = Sm.Lerp(from.W, to.W, eased);
        var h = Sm.Lerp(from.H, to.H, eased);
        var r = Sm.Lerp(from.R, to.R, eased);
        var right = CornerStart ? 24 : size.Width - 24;
        var x = CornerStart ? 24 : right - w;
        var y = size.Height - 24 - h;
        return new Rect(x, y, w, h);
    }

    private double ShellRadius()
    {
        var from = Metrics(_from);
        var to = Metrics(_face);
        return Sm.Lerp(from.R, to.R, _morph);
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        var rect = Shell(size);
        var radius = ShellRadius();
        var target = _face;

        // Surface: the accent button and the success state carry a tint; the menu and composer are panels.
        Sm.Lift(context, rect, radius, target is ArcActionMorphFace.Menu or ArcActionMorphFace.Composer ? 1.2 : 0.9);
        var accent = Sm.Brush(ArcTokens.Accent);
        var surface = Sm.Brush(ArcTokens.SurfaceRaised);
        var success = Sm.Brush(ArcTokens.Success);
        var border = new Pen(Sm.Brush(ArcTokens.Border), 1);

        if (target is ArcActionMorphFace.Button or ArcActionMorphFace.Success)
        {
            var fill = target == ArcActionMorphFace.Success ? success : accent;
            if (target == ArcActionMorphFace.Success && _morph < 1)
            {
                context.DrawRectangle(accent, null, rect, radius, radius);
                using (context.PushOpacity(_morph))
                {
                    context.DrawRectangle(fill, null, rect, radius, radius);
                }
            }
            else
            {
                context.DrawRectangle(fill, null, rect, radius, radius);
            }
        }
        else
        {
            context.DrawRectangle(surface, border, rect, radius, radius);
        }

        using (context.PushClip(new RoundedRect(rect, radius, radius)))
        {
            if (_morph < 0.99 && _from != _face)
            {
                using (context.PushOpacity(Math.Max(0, 1 - _morph * 2)))
                {
                    DrawFace(context, rect, _from, interactive: false);
                }
            }

            using (context.PushOpacity(_morph >= 0.99 ? 1 : Sm.Clamp01(_morph * 1.6 - 0.15)))
            {
                DrawFace(context, rect, target, interactive: _settled && _morph > 0.99);
            }
        }

        if (ShowFocusRing)
        {
            Sm.FocusRing(context, rect, radius);
        }
    }

    private void DrawFace(DrawingContext context, Rect rect, ArcActionMorphFace face, bool interactive)
    {
        switch (face)
        {
            case ArcActionMorphFace.Button:
            case ArcActionMorphFace.Success:
                DrawButton(context, rect, face == ArcActionMorphFace.Success);
                break;
            case ArcActionMorphFace.Menu:
                DrawMenu(context, rect, interactive);
                break;
            default:
                DrawComposer(context, rect, interactive);
                break;
        }
    }

    private void DrawButton(DrawingContext context, Rect rect, bool success)
    {
        var center = rect.Center;
        if (success)
        {
            var progress = Math.Clamp(_successAlpha, 0, 1);
            var ink = Sm.Brush(ArcTokens.Background);
            var pen = new Pen(ink, 2.4, null, PenLineCap.Round, PenLineJoin.Round);
            var a = new Point(center.X - 10, center.Y);
            var b = new Point(center.X - 3, center.Y + 7);
            var c = new Point(center.X + 11, center.Y - 8);
            if (progress > 0.05)
            {
                context.DrawLine(pen, a, Sm.Lerp(a, b, Sm.Clamp01(progress * 2)));
            }

            if (progress > 0.5)
            {
                context.DrawLine(pen, b, Sm.Lerp(b, c, Sm.Clamp01((progress - 0.5) * 2)));
            }

            Hit(new Rect(rect.X, rect.Y, rect.Width, rect.Height), IdSuccess);
            if (_morph > 0.6)
            {
                var label = Sm.Text("Task added", 12, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
                var pillWidth = label.Width + 22;
                var pill = new Rect(rect.X - 10 - pillWidth, center.Y - 16, pillWidth, 32);
                var opacity = Sm.Clamp01((_morph - 0.6) / 0.4);
                using (context.PushOpacity(opacity))
                {
                    Sm.Lift(context, pill, 16, 0.6);
                    context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceRaised), new Pen(Sm.Brush(ArcTokens.Border), 1), pill, 16, 16);
                    Sm.TextCentered(context, label, pill.Center.X, pill.Center.Y);
                }
            }

            return;
        }

        var plus = Sm.Brush(ArcTokens.AccentForeground);
        var thickness = 2.2;
        var half = 8.5;
        context.DrawLine(new Pen(plus, thickness, null, PenLineCap.Round), new Point(center.X - half, center.Y), new Point(center.X + half, center.Y));
        context.DrawLine(new Pen(plus, thickness, null, PenLineCap.Round), new Point(center.X, center.Y - half), new Point(center.X, center.Y + half));
        Hit(new Rect(rect.X, rect.Y, rect.Width, rect.Height), IdButton);
    }

    private void DrawMenu(DrawingContext context, Rect rect, bool interactive)
    {
        var left = rect.X + 16;
        var label = Sm.Text(Label ?? "Create", 13, Sm.Brush(ArcTokens.TextMuted), FontWeight.Medium);
        context.DrawText(label, new Point(left, rect.Y + 13));

        var menuIndex = -1;
        for (var index = 0; index < Actions.Length; index++)
        {
            var rowRect = new Rect(rect.X + 8, rect.Y + 42 + index * 58, rect.Width - 16, 54);
            var hovered = (HoverId == IdRow + index || _menuIndex == index) && interactive;
            if (hovered)
            {
                context.DrawRectangle(Sm.Alpha(ArcTokens.Foreground, 0.06), null, rowRect, 14, 14);
            }

            var iconCenter = new Point(rowRect.X + 22, rowRect.Y + 27);
            context.DrawRectangle(
                Sm.Alpha(ArcTokens.Foreground, 0.05),
                null,
                new Rect(iconCenter.X - 15, iconCenter.Y - 15, 30, 30),
                10,
                10);
            Sm.Icon(context, Actions[index].Icon, iconCenter, 17, Sm.Brush(ArcTokens.TextSecondary));

            var title = Sm.Text(Actions[index].Title, 14, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
            context.DrawText(title, new Point(rowRect.X + 44, rowRect.Y + 9));
            var description = Sm.Text(Actions[index].Description, 12, Sm.Brush(ArcTokens.TextMuted));
            context.DrawText(description, new Point(rowRect.X + 44, rowRect.Y + 29));

            var shortcut = Sm.Text(Actions[index].Shortcut, 11, Sm.Brush(ArcTokens.TextMuted), FontWeight.Medium, family: Sm.Mono);
            var badgeWidth = Math.Max(20, shortcut.Width + 12);
            var badge = new Rect(rowRect.Right - badgeWidth - 6, rowRect.Y + 17, badgeWidth, 20);
            context.DrawRectangle(null, new Pen(Sm.Brush(ArcTokens.Border), 1), badge, 6, 6);
            Sm.TextCentered(context, shortcut, badge.Center.X, badge.Center.Y);

            if (interactive)
            {
                Hit(rowRect, IdRow + index);
            }

            menuIndex = index;
        }

        _ = menuIndex;
    }

    private void DrawComposer(DrawingContext context, Rect rect, bool interactive)
    {
        var action = Actions[Math.Clamp(_action, 0, Actions.Length - 1)];
        var backCenter = new Point(rect.X + 28, rect.Y + 26);
        Sm.Circle(context, backCenter, 15, Sm.Alpha(ArcTokens.Foreground, 0.05));
        Sm.Icon(context, "chevron-left", backCenter, 16, Sm.Brush(ArcTokens.TextSecondary));

        var title = Sm.Text(action.Title, 14, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
        context.DrawText(title, new Point(rect.X + 50, rect.Y + 17));

        var sending = _face == ArcActionMorphFace.Composer && _sending;
        var field = new Rect(rect.X + 14, rect.Y + 50, rect.Width - 28, 76);
        var fieldBorder = new Pen(Sm.Brush(sending ? ArcTokens.Accent : ArcTokens.Border), 1);
        context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceMuted), fieldBorder, field, 14, 14);

        var hasText = !string.IsNullOrEmpty(_draft);
        var placeholder = action.Multiline ? "Write a note" : "What needs doing?";
        var text = Sm.Text(
            hasText ? _draft : placeholder,
            14,
            Sm.Brush(hasText ? ArcTokens.Foreground : ArcTokens.TextMuted),
            FontWeight.Normal,
            field.Width - 24,
            action.Multiline ? 3 : 2);
        context.DrawText(text, new Point(field.X + 12, field.Y + 11));

        if (_editing && hasText)
        {
            var caretX = field.X + 12 + Math.Min(text.Width, field.Width - 26);
            var caretY = field.Y + 11 + Math.Min(text.Height - 4, 46);
            context.DrawLine(new Pen(Sm.Brush(ArcTokens.Accent), 1.6), new Point(caretX, caretY), new Point(caretX, caretY + 17));
        }

        var chipLabel = Sm.Text("Due", 12, Sm.Brush(ArcTokens.TextMuted));
        context.DrawText(chipLabel, new Point(rect.X + 16, rect.Y + 140));
        var x = rect.X + 48;
        for (var index = 0; index < Choices.Length; index++)
        {
            var chipText = Sm.Text(Choices[index], 12, Sm.Brush(index == _choice ? ArcTokens.AccentStrong : ArcTokens.TextSecondary), FontWeight.Medium);
            var chip = new Rect(x, rect.Y + 134, chipText.Width + 22, 26);
            if (index == _choice)
            {
                context.DrawRectangle(Sm.Alpha(ArcTokens.Accent, 0.14), null, chip, 13, 13);
            }
            else
            {
                context.DrawRectangle(null, new Pen(Sm.Brush(ArcTokens.Border), 1), chip, 13, 13);
            }

            Sm.TextCentered(context, chipText, chip.Center.X, chip.Center.Y);
            if (interactive)
            {
                Hit(chip, IdChip + index);
            }

            x = chip.Right + 8;
        }

        var hint = Sm.Text("Enter to add", 11, Sm.Brush(ArcTokens.TextMuted));
        context.DrawText(hint, new Point(rect.X + 16, rect.Bottom - 30));

        var submitLabel = Sm.Text(sending ? "Adding" : "Add", 13, Sm.Brush(ArcTokens.AccentForeground), FontWeight.Medium);
        var submit = new Rect(rect.Right - 14 - 86, rect.Bottom - 50, 86, 36);
        context.DrawRectangle(Sm.Brush(ArcTokens.Accent), null, submit, 18, 18);
        Sm.TextCentered(context, submitLabel, submit.Center.X, submit.Center.Y);

        if (interactive)
        {
            Hit(field, IdField);
            Hit(submit, IdSubmit);
            Hit(new Rect(backCenter.X - 16, backCenter.Y - 16, 32, 32), IdBack);
        }
    }

    // ---- Interaction -------------------------------------------------------------------------------

    protected override void OnPress(int id, Point point)
    {
        Focus();
        InvalidateVisual();
    }

    protected override void OnRelease(int id, Point point)
    {
        if (id < 0 || HitAt(point) != id)
        {
            return;
        }

        switch (_face)
        {
            case ArcActionMorphFace.Button:
                if (id == IdButton)
                {
                    Face = ArcActionMorphFace.Menu;
                }

                break;
            case ArcActionMorphFace.Menu:
                if (id >= IdRow && id < IdRow + Actions.Length)
                {
                    OpenComposer(Actions[id - IdRow].Id);
                }

                break;
            case ArcActionMorphFace.Composer:
                if (id == IdBack)
                {
                    Face = ArcActionMorphFace.Menu;
                }
                else if (id >= IdChip && id < IdChip + Choices.Length)
                {
                    _choice = id - IdChip;
                }
                else if (id == IdField)
                {
                    _editing = true;
                }
                else if (id == IdSubmit && !string.IsNullOrEmpty(_draft))
                {
                    Submit();
                }

                break;
            case ArcActionMorphFace.Success:
                if (id == IdSuccess || id == IdButton)
                {
                    Face = ArcActionMorphFace.Button;
                }

                break;
        }

        InvalidateVisual();
    }


    private void Submit()
    {
        if (_sending)
        {
            return;
        }

        _sending = true;
        InvalidateVisual();
        Animate(520, ArcMotion.EaseStandard, _ => { }, () =>
        {
            _draft = string.Empty;
            Face = ArcActionMorphFace.Success;
        });
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (_face != ArcActionMorphFace.Composer || !_editing || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        _draft += e.Text;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_face == ArcActionMorphFace.Button && e.Key is Key.Enter or Key.Space)
        {
            Face = ArcActionMorphFace.Menu;
            e.Handled = true;
            return;
        }

        switch (_face)
        {
            case ArcActionMorphFace.Menu:
                switch (e.Key)
                {
                    case Key.Down:
                        _menuIndex = (_menuIndex + 1) % Actions.Length;
                        e.Handled = true;
                        break;
                    case Key.Up:
                        _menuIndex = (_menuIndex - 1 + Actions.Length) % Actions.Length;
                        e.Handled = true;
                        break;
                    case Key.Enter:
                    case Key.Space:
                        OpenComposer(Actions[_menuIndex].Id);
                        e.Handled = true;
                        break;
                    case Key.Escape:
                        Face = ArcActionMorphFace.Button;
                        e.Handled = true;
                        break;
                    case Key.T:
                        OpenComposer("task");
                        e.Handled = true;
                        break;
                    case Key.N:
                        OpenComposer("note");
                        e.Handled = true;
                        break;
                }

                break;
            case ArcActionMorphFace.Composer:
                switch (e.Key)
                {
                    case Key.Escape:
                        Face = ArcActionMorphFace.Menu;
                        e.Handled = true;
                        break;
                    case Key.Back:
                        if (_draft.Length > 0)
                        {
                            _draft = _draft[..^1];
                            e.Handled = true;
                        }

                        break;
                    case Key.Left:
                        _choice = Math.Max(0, _choice - 1);
                        e.Handled = true;
                        break;
                    case Key.Right:
                        _choice = Math.Min(Choices.Length - 1, _choice + 1);
                        e.Handled = true;
                        break;
                    case Key.Enter when _draft.Length > 0:
                        Submit();
                        e.Handled = true;
                        break;
                }

                break;
        }

        InvalidateVisual();
    }


    protected override void OnHoverChanged(int id)
    {
        if (_face == ArcActionMorphFace.Menu && id >= IdRow && id < IdRow + Actions.Length)
        {
            _menuIndex = id - IdRow;
        }

        InvalidateVisual();
    }
}
