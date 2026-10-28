using System;
using System.Globalization;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMotion.Controls;

/// <summary>The step a booking pill is showing.</summary>
public enum ArcBookingStep
{
    Start,
    Party,
    Date,
    Time,
    Review,
    Booked
}

/// <summary>
/// One floating pill that reshapes itself through a booking flow: party size, a draggable date strip, a time
/// grid, a ticket to confirm, and a booked state. The shape springs to fit each step while content fades a beat
/// later in the direction of travel. Reduced motion jumps the steps without travel.
/// </summary>
public class ArcBookingPill : ArcMotionCanvas
{
    private static readonly string[] Times =
    {
        "17:00", "17:30", "18:00", "18:30", "19:00", "19:30",
        "20:00", "20:30", "21:00", "21:30", "22:00", "22:30"
    };

    private const int IdPill = 1;
    private const int IdBack = 2;
    private const int IdNext = 3;
    private const int IdMinus = 4;
    private const int IdPlus = 5;
    private const int IdStrip = 6;
    private const int IdDay = 20;
    private const int IdTime = 40;
    private const int IdConfirm = 60;
    private const int IdDone = 61;

    private ArcBookingStep _step = ArcBookingStep.Review;
    private ArcBookingStep _from = ArcBookingStep.Review;
    private double _morph = 1;
    private int _party = 2;
    private int _day = 2;
    private int _time = 5;
    private double _dayOffset;
    private double _dragStart = double.NaN;
    private double _dragFrom;
    private bool _sending;
    private readonly DateTime _start = DateTime.Today;

    public ArcBookingPill()
    {
        Sm.Name(this, "Book a table", "Booking flow. Arrow keys choose a day or time, Enter continues, Escape steps back.");
    }

    /// <summary>The step the pill is showing.</summary>
    public ArcBookingStep Step
    {
        get => _step;
        set => GoTo(value);
    }

    protected override double DefaultWidth => 960;

    protected override double DefaultHeight => 420;

    private (double W, double H, double R) Metrics(ArcBookingStep step) => step switch
    {
        ArcBookingStep.Start => (210, 64, 32),
        ArcBookingStep.Party => (340, 200, 28),
        ArcBookingStep.Date => (380, 244, 28),
        ArcBookingStep.Time => (380, 300, 28),
        ArcBookingStep.Review => (380, 268, 28),
        _ => (380, 256, 28)
    };

    private Rect Shell(Size size)
    {
        var from = Metrics(_from);
        var to = Metrics(_step);
        var w = Sm.Lerp(from.W, to.W, _morph);
        var h = Sm.Lerp(from.H, to.H, _morph);
        return new Rect(size.Width / 2 - w / 2, size.Height - 24 - h, w, h);
    }

    private void GoTo(ArcBookingStep step)
    {
        if (step == _step)
        {
            return;
        }

        _from = _step;
        _step = step;
        _morph = 0;
        _sending = false;
        Animate(320, ArcMotion.SpringSmooth, progress =>
        {
            _morph = progress;
            InvalidateVisual();
        }, () => InvalidateVisual());
    }

    private void Back()
    {
        GoTo(_step switch
        {
            ArcBookingStep.Party => ArcBookingStep.Start,
            ArcBookingStep.Date => ArcBookingStep.Party,
            ArcBookingStep.Time => ArcBookingStep.Date,
            ArcBookingStep.Review => ArcBookingStep.Time,
            _ => ArcBookingStep.Review
        });
    }

    private string TimeLabel(int index)
    {
        var parts = Times[index].Split(':');
        var hour = int.Parse(parts[0]);
        var minute = parts[1];
        var suffix = hour >= 12 ? "PM" : "AM";
        var display = hour % 12 == 0 ? 12 : hour % 12;
        return $"{display}:{minute} {suffix}";
    }

    private string DateLabel(int dayIndex)
    {
        var date = _start.AddDays(dayIndex);
        return date.ToString("ddd, d MMM", CultureInfo.InvariantCulture);
    }

    private bool Available(int timeIndex) => !(_party > 6 && Times[timeIndex].CompareTo("20:00") > 0);

    protected override void OnRender(DrawingContext context, Size size)
    {
        if (size.Width < 420 || size.Height < 300)
        {
            return;
        }

        DrawVenue(context, size);
        var rect = Shell(size);
        var radius = Sm.Lerp(Metrics(_from).R, Metrics(_step).R, _morph);
        Sm.Lift(context, rect, radius, 1.2);
        context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceRaised), new Pen(Sm.Brush(ArcTokens.Border), 1), rect, radius, radius);
        Hit(rect, IdPill);

        using (context.PushClip(new RoundedRect(rect, radius, radius)))
        {
            if (Sm.Reduced || _morph >= 0.999)
            {
                DrawStep(context, rect, _step);
            }
            else
            {
                using (context.PushOpacity(Math.Max(0, 1 - _morph * 1.6)))
                {
                    DrawStep(context, rect, _from);
                }

                using (context.PushOpacity(Sm.Clamp01(_morph * 1.5 - 0.2)))
                {
                    DrawStep(context, rect, _step);
                }
            }
        }

        if (ShowFocusRing)
        {
            Sm.FocusRing(context, rect, radius);
        }
    }

    private void DrawVenue(DrawingContext context, Size size)
    {
        var hero = new Rect(30, 26, size.Width - 60, 200);
        context.DrawRectangle(Sm.Brush(ArcTokens.GradientSoft), null, hero, 26, 26);

        var name = Sm.Text("Osteria Nove", 26, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
        context.DrawText(name, new Point(hero.X + 28, hero.Y + 26));
        var detail = Sm.Text("12 Harbour Street · Italian · Open until 11 PM", 13, Sm.Brush(ArcTokens.TextSecondary));
        context.DrawText(detail, new Point(hero.X + 28, hero.Y + 62));

        var note = Sm.Text("Tables for 1 to 12 · free cancellation", 12, Sm.Brush(ArcTokens.TextMuted));
        context.DrawText(note, new Point(hero.X + 28, hero.Bottom - 34));

        // A quiet table setting so the hero reads as a venue, not a wireframe.
        var plate = new Rect(hero.Right - 220, hero.Y + 24, 192, 152);
        context.DrawRectangle(Sm.Brush(ArcTokens.GradientSoft), new Pen(Sm.Brush(ArcTokens.Border), 1), plate, 20, 20);
        var setting = new Pen(Sm.Solid(ArcColor.WithAlpha(Sm.Color(ArcTokens.BorderStrong), 0.9)), 1);
        context.DrawEllipse(null, setting, new Point(plate.Center.X, plate.Center.Y - 8), 40, 40);
        context.DrawEllipse(null, setting, new Point(plate.Center.X, plate.Center.Y - 8), 26, 26);
        context.DrawEllipse(Sm.Alpha(ArcTokens.Accent, 0.35), null, new Point(plate.Center.X, plate.Center.Y - 8), 8, 8);
        var caption = Sm.Text("Tonight, 7:30 PM", 11, Sm.Brush(ArcTokens.TextSecondary));
        Sm.TextCentered(context, caption, plate.Center.X, plate.Bottom - 18);
    }

    private void DrawStep(DrawingContext context, Rect rect, ArcBookingStep step)
    {
        switch (step)
        {
            case ArcBookingStep.Start:
                DrawStart(context, rect);
                break;
            case ArcBookingStep.Party:
                DrawParty(context, rect);
                break;
            case ArcBookingStep.Date:
                DrawDate(context, rect);
                break;
            case ArcBookingStep.Time:
                DrawTime(context, rect);
                break;
            case ArcBookingStep.Review:
                DrawReview(context, rect);
                break;
            case ArcBookingStep.Booked:
                DrawBooked(context, rect);
                break;
        }
    }

    private void DrawStart(DrawingContext context, Rect rect)
    {
        Sm.Icon(context, "calendar", new Rect(rect.X + 20, rect.Center.Y - 10, 20, 20), Sm.Brush(ArcTokens.AccentStrong));
        var label = Sm.Text("Book a table", 14, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
        context.DrawText(label, new Point(rect.X + 50, rect.Center.Y - label.Height / 2));
        Sm.Icon(context, "chevron-right", new Rect(rect.Right - 34, rect.Center.Y - 8, 16, 16), Sm.Brush(ArcTokens.TextMuted));
    }

    private void DrawBack(DrawingContext context, Rect rect)
    {
        var center = new Point(rect.X + 30, rect.Y + 30);
        context.DrawEllipse(Sm.Alpha(ArcTokens.Foreground, 0.05), null, center, 14, 14);
        Sm.Icon(context, "chevron-left", center, 15, Sm.Brush(ArcTokens.TextSecondary));
        Hit(new Rect(center.X - 16, center.Y - 16, 32, 32), IdBack);
    }

    private void DrawParty(DrawingContext context, Rect rect)
    {
        DrawBack(context, rect);
        var title = Sm.Text("How many guests?", 15, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
        context.DrawText(title, new Point(rect.X + 22, rect.Y + 62));

        var centerY = rect.Y + 122;
        var minus = new Point(rect.X + 46, centerY);
        var plus = new Point(rect.Right - 46, centerY);
        DrawRound(context, minus, "minus", IdMinus);
        DrawRound(context, plus, "plus", IdPlus);

        var number = Sm.Text(_party.ToString(), 40, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
        Sm.TextCentered(context, number, rect.Center.X, centerY);
        var unit = Sm.Text(_party == 1 ? "guest" : "guests", 12, Sm.Brush(ArcTokens.TextMuted));
        Sm.TextCentered(context, unit, rect.Center.X, centerY + 34);

        DrawAction(context, new Rect(rect.X + 22, rect.Bottom - 58, rect.Width - 44, 40), "Continue", IdNext);
    }

    private void DrawRound(DrawingContext context, Point center, string icon, int id)
    {
        context.DrawRectangle(null, new Pen(Sm.Brush(ArcTokens.Border), 1), new Rect(center.X - 18, center.Y - 18, 36, 36), 18, 18);
        Sm.Icon(context, icon, center, 16, Sm.Brush(ArcTokens.TextSecondary));
        HitCircle(center, 20, id);
    }

    private void DrawDate(DrawingContext context, Rect rect)
    {
        DrawBack(context, rect);
        var title = Sm.Text("Pick a date", 15, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
        context.DrawText(title, new Point(rect.X + 22, rect.Y + 20));
        var month = Sm.Text(_start.AddDays(_day).ToString("MMMM yyyy", CultureInfo.InvariantCulture), 12, Sm.Brush(ArcTokens.TextMuted));
        Sm.TextRight(context, month, rect.Right - 22, rect.Y + 34);

        var strip = new Rect(rect.X + 16, rect.Y + 58, rect.Width - 32, 74);
        context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceMuted), null, strip, 18, 18);
        using (context.PushClip(new RoundedRect(strip, 18, 18)))
        {
            var cardWidth = 56.0;
            var gap = 6.0;
            var offset = strip.X + 8 - _dayOffset * (cardWidth + gap);
            for (var index = 0; index < 21; index++)
            {
                var x = offset + index * (cardWidth + gap);
                if (x + cardWidth < strip.X || x > strip.Right)
                {
                    continue;
                }

                var card = new Rect(x, strip.Y + 8, cardWidth, strip.Height - 16);
                var selected = index == _day;
                if (selected)
                {
                    context.DrawRectangle(Sm.Brush(ArcTokens.Accent), null, card, 14, 14);
                }

                var date = _start.AddDays(index);
                var weekday = Sm.Text(date.ToString("ddd", CultureInfo.InvariantCulture), 10, Sm.Brush(selected ? ArcTokens.AccentForeground : ArcTokens.TextMuted));
                Sm.TextCentered(context, weekday, card.Center.X, card.Y + 12);
                var day = Sm.Text(date.Day.ToString(), 17, Sm.Brush(selected ? ArcTokens.AccentForeground : ArcTokens.Foreground), FontWeight.Medium);
                Sm.TextCentered(context, day, card.Center.X, card.Y + 30);
                Hit(card, IdDay + index);
            }
        }

        Hit(strip, IdStrip);
        DrawAction(context, new Rect(rect.X + 22, rect.Bottom - 56, rect.Width - 44, 40), "Continue", IdNext);
    }

    private void DrawTime(DrawingContext context, Rect rect)
    {
        DrawBack(context, rect);
        var title = Sm.Text("Pick a time", 15, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
        context.DrawText(title, new Point(rect.X + 22, rect.Y + 20));
        var day = Sm.Text(DateLabel(_day) + " · " + (_party == 1 ? "1 guest" : $"{_party} guests"), 12, Sm.Brush(ArcTokens.TextMuted));
        context.DrawText(day, new Point(rect.X + 22, rect.Y + 44));

        var columnWidth = (rect.Width - 44 - 24) / 4;
        for (var index = 0; index < Times.Length; index++)
        {
            var row = index / 4;
            var column = index % 4;
            var chip = new Rect(rect.X + 22 + column * (columnWidth + 8), rect.Y + 74 + row * 44, columnWidth, 36);
            var available = Available(index);
            var selected = index == _time;
            if (selected)
            {
                context.DrawRectangle(Sm.Brush(ArcTokens.Accent), null, chip, 18, 18);
            }
            else
            {
                context.DrawRectangle(
                    available ? null : Sm.Brush(ArcTokens.SurfaceMuted),
                    new Pen(Sm.Brush(ArcTokens.Border), 1),
                    chip,
                    18,
                    18);
            }

            var text = Sm.Text(TimeLabel(index), 12, Sm.Brush(selected ? ArcTokens.AccentForeground : available ? ArcTokens.Foreground : ArcTokens.TextMuted), FontWeight.Medium);
            Sm.TextCentered(context, text, chip.Center.X, chip.Center.Y);
            if (available)
            {
                Hit(chip, IdTime + index);
            }
        }

        var note = Sm.Text("Times shown in 12 hour clock · data stays HH:MM", 10, Sm.Brush(ArcTokens.TextMuted));
        context.DrawText(note, new Point(rect.X + 22, rect.Bottom - 30));
    }

    private void DrawAction(DrawingContext context, Rect rect, string label, int id)
    {
        context.DrawRectangle(Sm.Brush(ArcTokens.Accent), null, rect, 20, 20);
        var text = Sm.Text(label, 13, Sm.Brush(ArcTokens.AccentForeground), FontWeight.Medium);
        Sm.TextCentered(context, text, rect.Center.X, rect.Center.Y);
        Hit(rect, id);
    }

    private void DrawReview(DrawingContext context, Rect rect)
    {
        var title = Sm.Text("Your table", 13, Sm.Brush(ArcTokens.TextMuted), FontWeight.Medium);
        context.DrawText(title, new Point(rect.X + 22, rect.Y + 20));
        var venue = Sm.Text("Osteria Nove", 19, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
        context.DrawText(venue, new Point(rect.X + 22, rect.Y + 42));
        var address = Sm.Text("12 Harbour Street", 12, Sm.Brush(ArcTokens.TextMuted));
        context.DrawText(address, new Point(rect.X + 22, rect.Y + 70));

        var rule = new Pen(Sm.Brush(ArcTokens.Border), 1, new DashStyle(new double[] { 4, 4 }, 0));
        context.DrawLine(rule, new Point(rect.X + 22, rect.Y + 96), new Point(rect.Right - 22, rect.Y + 96));

        var rows = new (string Label, string Value)[]
        {
            ("Date", DateLabel(_day)),
            ("Time", TimeLabel(_time)),
            ("Party", _party == 1 ? "1 guest" : $"{_party} guests")
        };

        for (var index = 0; index < rows.Length; index++)
        {
            var y = rect.Y + 104 + index * 26;
            var label = Sm.Text(rows[index].Label, 12, Sm.Brush(ArcTokens.TextMuted));
            context.DrawText(label, new Point(rect.X + 22, y));
            var value = Sm.Text(rows[index].Value, 13, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
            Sm.TextRight(context, value, rect.Right - 22, y + 7);
        }

        var actionHeight = 44.0;
        var action = new Rect(rect.X + 22, rect.Bottom - actionHeight - 44, rect.Width - 44, actionHeight);
        if (_sending)
        {
            context.DrawRectangle(Sm.Brush(ArcTokens.AccentStrong), null, action, 22, 22);
            var dots = Sm.Text("Booking", 13, Sm.Brush(ArcTokens.AccentForeground), FontWeight.Medium);
            Sm.TextCentered(context, dots, action.Center.X, action.Center.Y);
        }
        else
        {
            DrawAction(context, action, "Confirm booking", IdConfirm);
        }

        var cancel = Sm.Text("Free cancellation until 5:00 PM", 10, Sm.Brush(ArcTokens.TextMuted));
        Sm.TextCentered(context, cancel, rect.Center.X, rect.Bottom - 26);
    }

    private void DrawBooked(DrawingContext context, Rect rect)
    {
        var center = new Point(rect.Center.X, rect.Y + 58);
        context.DrawEllipse(Sm.Brush(ArcTokens.SuccessSubtle), null, center, 28, 28);
        Sm.Icon(context, "check", center, 22, Sm.Brush(ArcTokens.Success), 2.4);

        var title = Sm.Text("Table booked", 19, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
        Sm.TextCentered(context, title, rect.Center.X, rect.Y + 108);
        var detail = Sm.Text($"{DateLabel(_day)} · {TimeLabel(_time)} · {(_party == 1 ? "1 guest" : $"{_party} guests")}", 12, Sm.Brush(ArcTokens.TextSecondary));
        Sm.TextCentered(context, detail, rect.Center.X, rect.Y + 134);
        var note = Sm.Text("We sent the confirmation to dana@acme.dev", 11, Sm.Brush(ArcTokens.TextMuted));
        Sm.TextCentered(context, note, rect.Center.X, rect.Y + 156);

        DrawAction(context, new Rect(rect.X + 22, rect.Bottom - 62, rect.Width - 44, 40), "Done", IdDone);
    }

    // ---- Interaction -------------------------------------------------------------------------------

    protected override void OnPress(int id, Point point)
    {
        Focus();
        if (id == IdStrip || (id >= IdDay && id < IdDay + 21))
        {
            _dragStart = point.X;
            _dragFrom = _dayOffset;
        }
    }

    protected override void OnDrag(int id, Point point)
    {
        if (double.IsNaN(_dragStart))
        {
            return;
        }

        const double card = 62;
        var delta = (_dragStart - point.X) / card;
        var next = _dragFrom + delta;
        var max = Math.Max(0, 21 - 6);
        if (next < 0)
        {
            next *= 0.35;
        }
        else if (next > max)
        {
            next = max + (next - max) * 0.35;
        }

        _dayOffset = next;
        InvalidateVisual();
    }

    protected override void OnRelease(int id, Point point)
    {
        if (double.IsNaN(_dragStart))
        {
            return;
        }

        var dragged = Math.Abs(_dayOffset - _dragFrom) > 0.08;
        _dragStart = double.NaN;
        if (dragged)
        {
            var target = Math.Clamp((int)Math.Round(_dayOffset), 0, 20);
            _day = target;
            var from = _dayOffset;
            Animate(240, ArcMotion.SpringSmooth, progress =>
            {
                _dayOffset = Sm.Lerp(from, target, progress);
                InvalidateVisual();
            });
            return;
        }

        if (HitAt(point) != id)
        {
            return;
        }

        if (id >= IdDay && id < IdDay + 21)
        {
            _day = id - IdDay;
            _dayOffset = _day;
            InvalidateVisual();
            return;
        }

        Handle(id);
    }

    private void Handle(int id)
    {
        if (id == IdPill && _step == ArcBookingStep.Start)
        {
            GoTo(ArcBookingStep.Party);
            return;
        }

        switch (id)
        {
            case IdBack:
                Back();
                break;
            case IdNext:
                GoTo(_step == ArcBookingStep.Party ? ArcBookingStep.Date : ArcBookingStep.Time);
                break;
            case IdMinus:
                _party = Math.Max(1, _party - 1);
                InvalidateVisual();
                break;
            case IdPlus:
                _party = Math.Min(12, _party + 1);
                InvalidateVisual();
                break;
            case IdConfirm:
                _sending = true;
                InvalidateVisual();
                Animate(420, ArcMotion.EaseStandard, _ => { }, () => GoTo(ArcBookingStep.Booked));
                break;
            case IdDone:
                GoTo(ArcBookingStep.Start);
                break;
            default:
                if (id >= IdTime && id < IdTime + Times.Length)
                {
                    _time = id - IdTime;
                    GoTo(ArcBookingStep.Review);
                }

                break;
        }
    }


    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Escape:
                Back();
                e.Handled = true;
                break;
            case Key.Enter:
                if (_step == ArcBookingStep.Start)
                {
                    GoTo(ArcBookingStep.Party);
                }
                else if (_step == ArcBookingStep.Party)
                {
                    GoTo(ArcBookingStep.Date);
                }
                else if (_step == ArcBookingStep.Date)
                {
                    GoTo(ArcBookingStep.Time);
                }
                else if (_step == ArcBookingStep.Time)
                {
                    GoTo(ArcBookingStep.Review);
                }
                else if (_step == ArcBookingStep.Review)
                {
                    Handle(IdConfirm);
                }
                else
                {
                    GoTo(ArcBookingStep.Start);
                }

                e.Handled = true;
                break;
            case Key.Left:
                if (_step == ArcBookingStep.Date)
                {
                    _day = Math.Max(0, _day - 1);
                    _dayOffset = _day;
                }
                else if (_step == ArcBookingStep.Time)
                {
                    StepTime(-1);
                }

                e.Handled = true;
                break;
            case Key.Right:
                if (_step == ArcBookingStep.Date)
                {
                    _day = Math.Min(20, _day + 1);
                    _dayOffset = _day;
                }
                else if (_step == ArcBookingStep.Time)
                {
                    StepTime(1);
                }

                e.Handled = true;
                break;
            case Key.PageUp:
                _day = Math.Max(0, _day - 7);
                _dayOffset = _day;
                e.Handled = true;
                break;
            case Key.PageDown:
                _day = Math.Min(20, _day + 7);
                _dayOffset = _day;
                e.Handled = true;
                break;
            case Key.Home:
                _day = 0;
                _dayOffset = 0;
                e.Handled = true;
                break;
            case Key.End:
                _day = 20;
                _dayOffset = 20;
                e.Handled = true;
                break;
        }

        InvalidateVisual();
    }

    private void StepTime(int delta)
    {
        var index = _time;
        for (var attempt = 0; attempt < Times.Length; attempt++)
        {
            index = (index + delta + Times.Length) % Times.Length;
            if (Available(index))
            {
                _time = index;
                return;
            }
        }
    }
}
