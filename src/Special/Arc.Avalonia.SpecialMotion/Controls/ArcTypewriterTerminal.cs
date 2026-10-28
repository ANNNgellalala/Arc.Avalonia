using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMotion.Controls;

/// <summary>
/// A terminal window that types a scripted CLI session with a seeded human cadence, colours the shell syntax,
/// and streams output lines, progress bars and spinners, with a scrubber, play and pause. Everything on the
/// screen is a simulation derived from one session time, so it is deterministic in every replay. Reduced
/// motion shows the finished session at once and stops the caret blink.
/// </summary>
public class ArcTypewriterTerminal : ArcMotionCanvas
{
    private sealed class Command
    {
        public string Cwd = "~";
        public string Text = string.Empty;
        public double Start;
        public double[] CharTimes = Array.Empty<double>();
        public double TypeEnd;
        public readonly List<Output> Outputs = new();
    }

    private sealed class Output
    {
        public string Kind = "text";
        public string Text = string.Empty;
        public string Tone = "plain";
        public double Start;
        public double Duration;
        public string? Done;
    }

    private static readonly (string Cwd, string Command, Output[] Output)[] Script =
    {
        ("~", "npm create arc@latest gallery", new[]
        {
            new Output { Kind = "progress", Text = "Fetching packages", Duration = 1.1 },
            new Output { Kind = "text", Text = "Created gallery in ./gallery", Tone = "success" },
            new Output { Kind = "text", Text = "Added 42 packages in 1.8s", Tone = "muted" }
        }),

        ("~/gallery", "npm run dev", new[]
        {
            new Output { Kind = "task", Text = "Starting dev server", Duration = 0.7, Done = "0.4s" },
            new Output { Kind = "text", Text = "ready in 312 ms", Tone = "accent" },
            new Output { Kind = "text", Text = "Local: http://localhost:5173", Tone = "muted" }
        })
    };

    private const int IdPlay = 1;
    private const int IdReplay = 2;
    private const int IdScrub = 3;
    private const int IdCopy = 4;

    private readonly List<Command> _commands = new();
    private readonly List<Output> _outputs = new();
    private double _total;
    private double _time;
    private bool _playing;
    private bool _scrubbing;
    private bool _built;

    public ArcTypewriterTerminal()
    {
        Sm.Name(this, "Terminal session", "Scripted CLI session. Space plays or pauses, arrow keys jump between steps, Home and End jump to the ends.");
    }

    protected override double DefaultWidth => 900;

    protected override double DefaultHeight => 360;

    protected override bool NeedsFrame => base.NeedsFrame || (!Sm.Reduced && _playing);

    private void Build()
    {
        if (_built)
        {
            return;
        }

        _built = true;
        var time = 0.35;
        var stepIndex = 0;
        foreach (var (cwd, command, output) in Script)
        {
            var cmd = new Command { Cwd = cwd, Text = command, Start = time };
            var times = new double[command.Length];
            var cursor = time + 0.22 + 0.18 * Sm.Noise(stepIndex * 17 + 3);
            for (var index = 0; index < command.Length; index++)
            {
                var seed = stepIndex * 101 + index * 13;
                var character = command[index];
                var gap = 0.030 + 0.045 * Sm.Noise(seed);
                if (character == ' ')
                {
                    gap += 0.035;
                }

                if (index > 0 && char.IsUpper(character))
                {
                    gap += 0.03;
                }

                cursor += gap;
                times[index] = cursor;
            }

            cmd.CharTimes = times;
            cmd.TypeEnd = cursor;
            _commands.Add(cmd);
            time = cursor + 0.16;

            foreach (var line in output)
            {
                line.Start = time;
                if (line.Kind == "progress" || line.Kind == "task")
                {
                    time += line.Duration + 0.14;
                }
                else
                {
                    time += 0.12;
                }

                cmd.Outputs.Add(line);
                _outputs.Add(line);
            }

            time += 0.42;
            stepIndex++;
        }

        _total = time + 0.6;
        _time = Sm.Reduced ? _total : 0;
        _playing = !Sm.Reduced;
        if (!Sm.Reduced)
        {
            EnsureFrame();
        }
    }

    protected override void OnFrame(double dt)
    {
        if (!_playing)
        {
            return;
        }

        _time += dt;
        if (_time >= _total + 1.4)
        {
            _time = 0;
        }
    }

    private (Rect Window, Rect Screen, Rect Title, Rect Scrubber) Metrics(Size size)
    {
        var width = Math.Min(size.Width - 60, 860);
        var window = new Rect(size.Width / 2 - width / 2, 24, width, 236);
        var title = new Rect(window.X, window.Y, window.Width, 38);
        var screen = new Rect(window.X + 16, title.Bottom + 6, window.Width - 32, 172);
        var scrubber = new Rect(window.X + 16, window.Bottom + 18, window.Width - 32, 32);
        return (window, screen, title, scrubber);
    }

    private const int Rows = 8;
    private const double RowHeight = 20;

    protected override void OnRender(DrawingContext context, Size size)
    {
        Build();
        if (size.Width < 420 || size.Height < 280)
        {
            return;
        }

        var metrics = Metrics(size);
        Sm.Lift(context, metrics.Window, 24, 1.3);
        context.DrawRectangle(Sm.Brush(ArcTokens.Background), new Pen(Sm.Brush(ArcTokens.Border), 1), metrics.Window, 24, 24);

        // Title bar.
        context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceMuted), null, new Rect(metrics.Title.X + 1, metrics.Title.Y + 1, metrics.Title.Width - 2, metrics.Title.Height - 2), 23, 23);
        var dots = new Point(metrics.Title.X + 20, metrics.Title.Center.Y);
        for (var index = 0; index < 3; index++)
        {
            context.DrawEllipse(Sm.Brush(ArcTokens.BorderStrong), null, new Point(dots.X + index * 9, dots.Y), 3.2, 3.2);
        }

        var name = Sm.Text("zsh", 12, Sm.Brush(ArcTokens.TextSecondary), FontWeight.Medium, family: Sm.Mono);
        Sm.TextCentered(context, name, metrics.Title.Center.X, metrics.Title.Center.Y);
        var copy = new Rect(metrics.Title.Right - 40, metrics.Title.Y + 7, 26, 24);
        Sm.Icon(context, "copy", copy.Center, 14, Sm.Brush(ArcTokens.TextMuted));
        Hit(copy, IdCopy);

        DrawScreen(context, metrics);
        DrawScrubber(context, metrics);
    }

    private void DrawScreen(DrawingContext context, (Rect Window, Rect Screen, Rect Title, Rect Scrubber) metrics)
    {
        var screen = metrics.Screen;
        context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceMuted), null, screen, 14, 14);
        using (context.PushClip(new RoundedRect(screen.Deflate(8), 10, 10)))
        {
            var rows = BuildRows();
            var totalRows = rows.Count;
            var offset = Math.Max(0, totalRows - Rows);
            var y = screen.Y + 8;
            for (var index = offset; index < totalRows; index++)
            {
                DrawRow(context, rows[index], new Point(screen.X + 12, y), screen.Width - 24);
                y += RowHeight;
            }
        }

        // Feather the top edge as lines scroll away.
        var feather = new Rect(screen.X + 8, screen.Y + 6, screen.Width - 16, 16);
        context.DrawRectangle(Sm.Gradient(Sm.WithAlpha(Sm.Color(ArcTokens.SurfaceMuted), 0.9), Sm.WithAlpha(Sm.Color(ArcTokens.SurfaceMuted), 0), vertical: true), null, feather, 8, 8);
    }

    private sealed class Row
    {
        public string Kind = "output";
        public string Text = string.Empty;
        public string Tone = "plain";
        public Command? Command;
        public Output? Output;
        public double Start;
        public double Fraction = 1;
    }

    private List<Row> BuildRows()
    {
        var rows = new List<Row>();
        foreach (var command in _commands)
        {
            if (_time < command.Start)
            {
                break;
            }

            rows.Add(new Row { Kind = "command", Command = command, Start = command.Start });
            foreach (var output in command.Outputs)
            {
                if (output.Start <= _time)
                {
                    rows.Add(new Row { Kind = "output", Output = output, Start = output.Start, Tone = output.Tone });
                }
            }
        }

        return rows;
    }

    private void DrawRow(DrawingContext context, Row row, Point origin, double width)
    {
        if (row.Kind == "command" && row.Command is { } command)
        {
            var prompt = Sm.MonoText($"{command.Cwd} $", 12, Sm.Brush(ArcTokens.TextMuted));
            context.DrawText(prompt, new Point(origin.X, origin.Y + 2));
            var x = origin.X + prompt.Width + 8;
            var typed = 0;
            for (var index = 0; index < command.Text.Length; index++)
            {
                if (_time >= command.CharTimes[index])
                {
                    typed = index + 1;
                }
            }

            DrawSyntax(context, command.Text, typed, new Point(x, origin.Y + 2), width - (x - origin.X));
            var typing = typed < command.Text.Length;
            if (typing || _time - command.TypeEnd < 0.45)
            {
                var textWidth = Sm.MonoText(command.Text[..typed], 12, Sm.Brush(ArcTokens.Foreground)).Width;
                var caretX = Math.Min(x + textWidth + 1, origin.X + width - 8);
                context.DrawLine(new Pen(Sm.Brush(ArcTokens.Accent), 1.6), new Point(caretX, origin.Y + 2), new Point(caretX, origin.Y + 17));
            }

            return;
        }

        if (row.Output is not { } output)
        {
            return;
        }

        var elapsed = _time - output.Start;
        var ink = output.Tone switch
        {
            "success" => Sm.Brush(ArcTokens.Success),
            "warning" => Sm.Brush(ArcTokens.Warning),
            "danger" => Sm.Brush(ArcTokens.Danger),
            "muted" => Sm.Brush(ArcTokens.TextMuted),
            "accent" => Sm.Brush(ArcTokens.AccentStrong),
            _ => Sm.Brush(ArcTokens.TextSecondary)
        };

        var textX = origin.X;
        if (output.Kind == "progress")
        {
            var fraction = Progress(elapsed, output.Duration);
            var label = Sm.MonoText(output.Text, 11, Sm.Brush(ArcTokens.TextSecondary));
            context.DrawText(label, new Point(origin.X, origin.Y + 2));
            var bar = new Rect(origin.X + label.Width + 10, origin.Y + 7, Math.Min(150, width - label.Width - 60), 6);
            context.DrawRectangle(Sm.Brush(ArcTokens.Border), null, bar, 3, 3);
            context.DrawRectangle(Sm.Brush(ArcTokens.Accent), null, new Rect(bar.X, bar.Y, Math.Max(2, bar.Width * fraction), bar.Height), 3, 3);
            var percent = Sm.MonoText($"{Math.Round(fraction * 100)}%", 10, Sm.Brush(ArcTokens.TextMuted));
            context.DrawText(percent, new Point(bar.Right + 8, origin.Y + 2));
            return;
        }

        if (output.Kind == "task")
        {
            var done = elapsed > output.Duration;
            if (done)
            {
                Sm.Icon(context, "check", new Rect(origin.X, origin.Y + 2, 12, 12), Sm.Brush(ArcTokens.Success), 2);
                textX = origin.X + 18;
                var doneText = Sm.MonoText(output.Done ?? "done", 10, Sm.Brush(ArcTokens.TextMuted));
                context.DrawText(doneText, new Point(origin.X + width - doneText.Width - 4, origin.Y + 2));
            }
            else
            {
                var center = new Point(origin.X + 6, origin.Y + 9);
                var angle = (elapsed * 900) % 360;
                var pen = new Pen(Sm.Brush(ArcTokens.Accent), 1.6, null, PenLineCap.Round);
                DrawArc(context, center, 5, angle, 250, pen);
                textX = origin.X + 18;
            }

            var task = Sm.MonoText(output.Text, 11, output.Tone == "success" ? Sm.Brush(ArcTokens.Success) : Sm.Brush(ArcTokens.TextSecondary));
            context.DrawText(task, new Point(textX, origin.Y + 2));
            return;
        }

        var line = Sm.MonoText(output.Text, 11, ink);
        if (output.Tone == "success")
        {
            Sm.Icon(context, "check", new Rect(origin.X, origin.Y + 3, 11, 11), Sm.Brush(ArcTokens.Success), 2);
            context.DrawText(line, new Point(textX + 16, origin.Y + 2));
        }
        else
        {
            context.DrawText(line, new Point(textX, origin.Y + 2));
        }
    }

    private static void DrawArc(DrawingContext context, Point center, double radius, double startDegrees, double sweep, IPen pen)
    {
        var start = startDegrees * Math.PI / 180;
        var end = (startDegrees + sweep) * Math.PI / 180;
        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(new Point(center.X + radius * Math.Cos(start), center.Y + radius * Math.Sin(start)), false);
            figure.ArcTo(
                new Point(center.X + radius * Math.Cos(end), center.Y + radius * Math.Sin(end)),
                new Size(radius, radius),
                0,
                sweep > 180,
                SweepDirection.Clockwise);
            figure.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private static double Progress(double elapsed, double duration)
    {
        var t = Sm.Clamp01(elapsed / Math.Max(0.1, duration));
        // Quick start, a stall around the middle, then a steady finish.
        if (t < 0.45)
        {
            return t / 0.45 * 0.55;
        }

        if (t < 0.7)
        {
            return 0.55 + (t - 0.45) / 0.25 * 0.08;
        }

        return 0.63 + (t - 0.7) / 0.3 * 0.37;
    }

    private void DrawSyntax(DrawingContext context, string text, int typed, Point origin, double width)
    {
        if (typed <= 0)
        {
            return;
        }

        var advance = Sm.MonoText("M", 12, Brushes.Transparent).Width;
        for (var index = 0; index < typed; index++)
        {
            var character = text[index];
            if (character == ' ')
            {
                continue;
            }

            var brush = TokenBrush(text, index);
            var formatted = Sm.MonoText(character.ToString(), 12, brush);
            context.DrawText(formatted, new Point(origin.X + index * advance, origin.Y));
            if (origin.X + (index + 1) * advance > origin.X + width)
            {
                break;
            }
        }
    }

    private static IBrush TokenBrush(string text, int index)
    {
        var start = index;
        while (start > 0 && text[start - 1] != ' ')
        {
            start--;
        }

        var end = index;
        while (end < text.Length && text[end] != ' ')
        {
            end++;
        }

        var token = text[start..end];
        if (start == 0)
        {
            return Sm.Brush(ArcTokens.AccentStrong);
        }

        if (token.StartsWith('-'))
        {
            return Sm.Brush(ArcTokens.Accent);
        }

        if (token.Contains('/') || token.Contains('@'))
        {
            return Sm.Brush(ArcTokens.Series2);
        }

        if (token is "&&" or "|" or ">")
        {
            return Sm.Brush(ArcTokens.TextMuted);
        }

        return Sm.Brush(ArcTokens.Foreground);
    }

    private void DrawScrubber(DrawingContext context, (Rect Window, Rect Screen, Rect Title, Rect Scrubber) metrics)
    {
        var area = metrics.Scrubber;
        var play = new Rect(area.X, area.Y, 32, 32);
        var replay = new Rect(area.X + 38, area.Y, 32, 32);
        DrawRoundButton(context, play, _playing ? "pause" : "play", IdPlay, filled: true);
        DrawRoundButton(context, replay, "refresh-cw", IdReplay, filled: false);

        var track = new Rect(area.X + 88, area.Center.Y - 2, area.Width - 88 - 92, 4);
        context.DrawRectangle(Sm.Brush(ArcTokens.Border), null, track, 2, 2);
        var fraction = _total <= 0 ? 0 : Sm.Clamp01(_time / _total);
        context.DrawRectangle(Sm.Brush(ArcTokens.Accent), null, new Rect(track.X, track.Y, Math.Max(2, track.Width * fraction), track.Height), 2, 2);
        var thumb = new Point(track.X + track.Width * fraction, track.Center.Y);
        context.DrawEllipse(Sm.Brush(ArcTokens.Accent), new Pen(Sm.Brush(ArcTokens.Background), 2), thumb, 7, 7);
        Hit(new Rect(track.X - 8, area.Y, track.Width + 16, area.Height), IdScrub);

        var readout = Sm.MonoText($"{Sm.Time(_time)} / {Sm.Time(_total)}", 10, Sm.Brush(ArcTokens.TextMuted));
        Sm.TextRight(context, readout, area.Right, area.Center.Y);
    }

    private void DrawRoundButton(DrawingContext context, Rect rect, string icon, int id, bool filled)
    {
        context.DrawRectangle(
            filled ? Sm.Brush(ArcTokens.Accent) : null,
            filled ? null : new Pen(Sm.Brush(ArcTokens.Border), 1),
            rect,
            10,
            10);
        Sm.Icon(context, icon, rect.Center, 15, Sm.Brush(filled ? ArcTokens.AccentForeground : ArcTokens.TextSecondary));
        Hit(rect, id);
    }

    // ---- Interaction -------------------------------------------------------------------------------

    private void Seek(double time)
    {
        _time = Math.Clamp(time, 0, _total);
        InvalidateVisual();
    }

    private void SeekStep(int delta)
    {
        var times = new List<double> { 0 };
        foreach (var command in _commands)
        {
            times.Add(command.Start);
        }

        var target = _time;
        if (delta > 0)
        {
            foreach (var time in times)
            {
                if (time > _time + 0.05)
                {
                    target = time;
                    break;
                }
            }
        }
        else
        {
            for (var index = times.Count - 1; index >= 0; index--)
            {
                if (times[index] < _time - 0.15)
                {
                    target = times[index];
                    break;
                }
            }
        }

        Seek(target);
    }

    protected override void OnPress(int id, Point point)
    {
        Focus();
        switch (id)
        {
            case IdPlay:
                _playing = !_playing;
                EnsureFrame();
                InvalidateVisual();
                break;
            case IdReplay:
                Seek(0);
                _playing = true;
                EnsureFrame();
                break;
            case IdScrub:
                _scrubbing = true;
                SeekAt(point);
                break;
            case IdCopy:
                CopyCommands();
                break;
        }
    }

    protected override void OnDrag(int id, Point point)
    {
        if (_scrubbing)
        {
            SeekAt(point);
        }
    }

    protected override void OnRelease(int id, Point point)
    {
        _scrubbing = false;
    }

    private void SeekAt(Point point)
    {
        var metrics = Metrics(Bounds.Size);
        var track = new Rect(metrics.Scrubber.X + 88, metrics.Scrubber.Y, metrics.Scrubber.Width - 88 - 92, metrics.Scrubber.Height);
        var fraction = Sm.Clamp01((point.X - track.X) / Math.Max(1, track.Width));
        Seek(fraction * _total);
    }

    private async void CopyCommands()
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                var lines = new List<string>();
                foreach (var command in _commands)
                {
                    lines.Add(command.Text);
                }

                await clipboard.SetTextAsync(string.Join(Environment.NewLine, lines));
            }
        }
        catch
        {
            // Copying is best effort; the commands stay readable on screen.
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Space:
            case Key.K:
                _playing = !_playing;
                EnsureFrame();
                e.Handled = true;
                break;
            case Key.R:
                Seek(0);
                _playing = true;
                EnsureFrame();
                e.Handled = true;
                break;
            case Key.Left:
                SeekStep(-1);
                e.Handled = true;
                break;
            case Key.Right:
                SeekStep(1);
                e.Handled = true;
                break;
            case Key.Home:
                Seek(0);
                e.Handled = true;
                break;
            case Key.End:
                Seek(_total);
                e.Handled = true;
                break;
        }

        InvalidateVisual();
    }
}
