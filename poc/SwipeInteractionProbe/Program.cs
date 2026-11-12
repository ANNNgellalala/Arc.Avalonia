using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Media.Controls;
using Arc.Avalonia.Theme;

namespace SwipeInteractionProbe;

/// <summary>
/// Behaviour probe for the "tappable ArcSwipeActions row" change (task-3). Runs under Avalonia.Headless
/// with headless drawing: no window is shown on the desktop, pointer input is injected into the real
/// input pipeline (MouseDown/Move/Up on the TopLevel), so routed events, pointer capture and control
/// templates behave exactly as in a desktop window.
///
/// Mandatory cases T1-T6 print PASS/FAIL plus the observed values and drive the exit code; T7 (wrapping
/// the row in a Button) is exploratory and only prints observations.
/// </summary>
internal static class Program
{
    private static Window _window = null!;
    private static int _failures;

    private static ArcSwipeActions _tapRow = null!;
    private static ArcSwipeActions _dragRow = null!;
    private static ArcSwipeActions _buttonRow = null!;
    private static ArcSwipeActions _buttonDragRow = null!;
    private static ArcSwipeActions _openRow = null!;
    private static ArcSwipeActions _verticalRow = null!;
    private static ArcSwipeActions _wrappedRow = null!;
    private static ArcSwipeActions _wrappedButtonRow = null!;
    private static Button _button = null!;
    private static Button _buttonDrag = null!;
    private static Button _openButton = null!;
    private static Button _outerButton = null!;
    private static Button _wrappedInnerButton = null!;
    private static Button _wrappedButtonOuter = null!;

    private static int _tapRowTaps;
    private static int _dragRowTaps;
    private static int _buttonClicks;
    private static int _buttonDragClicks;
    private static int _openButtonClicks;
    private static int _wrappedRowTaps;
    private static int _wrappedOuterClicks;
    private static int _wrappedInnerClicks;
    private static int _wrappedButtonOuterClicks;
    private static int _wrappedButtonRowTaps;

    [STAThread]
    public static int Main()
    {
        try
        {
            AppBuilder.Configure<ProbeApp>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
                .SetupWithoutStarting();

            // The row tween is DispatcherTimer based; reduced motion applies Open/Close immediately, which
            // keeps every assertion deterministic without pumping a render timer for 400 ms.
            ArcThemeManager.ReducedMotion = true;
            ArcThemeManager.Apply(Application.Current!, ArcColorScheme.Dark, ArcAccent.Violet);

            BuildWindow();

            Console.WriteLine($"Swipe interaction probe | Avalonia {typeof(Application).Assembly.GetName().Version} | headless drawing | reduced motion");
            Console.WriteLine($"window {_window.Bounds.Width:0}x{_window.Bounds.Height:0}, row width {_tapRow.Bounds.Width:0}");
            Console.WriteLine();

            RunT1();
            RunT2();
            RunT3();
            RunT4();
            RunT5();
            RunT6();
            RunT7();

            Console.WriteLine();
            Console.WriteLine(_failures == 0
                ? "PROBE PASS: T1-T6 all matched the expected interaction contract (T7 exploratory, see T7 INFO above)."
                : $"PROBE FAIL: {_failures} mandatory case(s) failed.");
            return _failures == 0 ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.WriteLine("PROBE CRASH: " + exception);
            return 2;
        }
    }

    private static void BuildWindow()
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(16) };

        // T1/T2: content is a plain hit-testable surface, no interactive element inside.
        _tapRow = CreateRow("T1 tap row", Content("T1 tap row"));
        _tapRow.Tapped += (_, _) => _tapRowTaps++;
        panel.Children.Add(_tapRow);

        _dragRow = CreateRow("T2 drag row", Content("T2 drag row"));
        _dragRow.Tapped += (_, _) => _dragRowTaps++;
        panel.Children.Add(_dragRow);

        // T3: content is a Button; a tap must reach it.
        _button = new Button
        {
            Content = "Open details",
            Height = 64,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _button.Click += (_, _) => _buttonClicks++;
        _buttonRow = CreateRow("T3 button row", _button);
        panel.Children.Add(_buttonRow);

        // T4: drag starting on the Button must open the row and cancel the Button click.
        _buttonDrag = new Button
        {
            Content = "Open details",
            Height = 64,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _buttonDrag.Click += (_, _) => _buttonDragClicks++;
        _buttonDragRow = CreateRow("T4 button drag row", _buttonDrag);
        panel.Children.Add(_buttonDragRow);

        // T5: an already open row must consume a tap, close itself and never click the content.
        _openButton = new Button
        {
            Content = "Open details",
            Height = 64,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _openButton.Click += (_, _) => _openButtonClicks++;
        _openRow = CreateRow("T5 open row", _openButton);
        panel.Children.Add(_openRow);

        // T6: a vertical gesture belongs to the scroll host; the row must stay closed.
        _verticalRow = CreateRow("T6 vertical drag row", Content("T6 vertical drag row"));
        panel.Children.Add(_verticalRow);

        // T7 (exploratory): the whole row wrapped in a Button.
        _wrappedRow = CreateRow("T7 wrapped row", Content("T7 wrapped row"));
        _wrappedRow.Tapped += (_, _) => _wrappedRowTaps++;
        _outerButton = new Button
        {
            Content = _wrappedRow,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        _outerButton.Click += (_, _) => _wrappedOuterClicks++;
        panel.Children.Add(_outerButton);

        // T7b (exploratory): a Button-content row wrapped in another Button.
        _wrappedInnerButton = new Button
        {
            Content = "Open details",
            Height = 64,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _wrappedInnerButton.Click += (_, _) => _wrappedInnerClicks++;
        _wrappedButtonRow = CreateRow("T7b wrapped button row", _wrappedInnerButton);
        _wrappedButtonRow.Tapped += (_, _) => _wrappedButtonRowTaps++;
        _wrappedButtonOuter = new Button
        {
            Content = _wrappedButtonRow,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        _wrappedButtonOuter.Click += (_, _) => _wrappedButtonOuterClicks++;
        panel.Children.Add(_wrappedButtonOuter);

        _window = new Window
        {
            Width = 760,
            Height = 820,
            Content = panel
        };

        _window.Show();
        Pump(10);

        if (_tapRow.Bounds.Width <= 0)
        {
            throw new InvalidOperationException("the probe window did not lay out (row width is 0)");
        }
    }

    private static ArcSwipeActions CreateRow(string label, Control content) => new()
    {
        Label = label,
        Width = 640,
        MinHeight = 64,
        HorizontalAlignment = HorizontalAlignment.Left,
        Content = content,
        Trailing = new List<ArcSwipeAction>
        {
            new("Archive", "archive"),
            new("Delete", "trash-2", ArcSwipeActionTone.Danger)
        }
    };

    private static Control Content(string text) => new Border
    {
        Background = ArcResources.Brush(ArcTokens.SurfaceMuted),
        Child = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 16, 0)
        }
    };

    private static void RunT1()
    {
        _tapRowTaps = 0;
        Tap(CenterOf(_tapRow));
        Report("T1", _tapRowTaps == 1 && !_tapRow.IsOpen,
            $"taps={_tapRowTaps} isOpen={_tapRow.IsOpen} | tap on a pure-content row (expect taps=1, isOpen=False)");
    }

    private static void RunT2()
    {
        _dragRowTaps = 0;
        Drag(CenterOf(_dragRow), -120, 0);
        Report("T2", _dragRow.IsOpen && _dragRowTaps == 0,
            $"isOpen={_dragRow.IsOpen} taps={_dragRowTaps} | horizontal drag dx=-120 on a pure-content row (expect isOpen=True, taps=0)");
    }

    private static void RunT3()
    {
        _buttonClicks = 0;
        Tap(CenterOf(_button));
        Report("T3", _buttonClicks == 1 && !_buttonRow.IsOpen,
            $"clicks={_buttonClicks} isOpen={_buttonRow.IsOpen} | tap on the content Button (expect clicks=1, isOpen=False)");
    }

    private static void RunT4()
    {
        _buttonDragClicks = 0;
        Drag(CenterOf(_buttonDrag), -120, 0);
        Report("T4", _buttonDragRow.IsOpen && _buttonDragClicks == 0,
            $"isOpen={_buttonDragRow.IsOpen} clicks={_buttonDragClicks} | horizontal drag dx=-120 starting on the content Button (expect isOpen=True, clicks=0)");
    }

    private static void RunT5()
    {
        _openRow.Open(-1);
        Pump();
        var openedBefore = _openRow.IsOpen;
        _openButtonClicks = 0;
        Tap(CenterOf(_openRow));
        Report("T5", openedBefore && !_openRow.IsOpen && _openButtonClicks == 0,
            $"openedBefore={openedBefore} isOpenAfter={_openRow.IsOpen} clicks={_openButtonClicks} | tap on an already open row (expect openedBefore=True, isOpen=False, clicks=0)");
    }

    private static void RunT6()
    {
        Drag(CenterOf(_verticalRow), 0, 80);
        Report("T6", !_verticalRow.IsOpen,
            $"isOpen={_verticalRow.IsOpen} | vertical drag dy=80 (expect isOpen=False)");
    }

    private static void RunT7()
    {
        _wrappedRowTaps = 0;
        _wrappedOuterClicks = 0;
        Tap(CenterOf(_wrappedRow));
        Console.WriteLine($"T7 INFO tap inside a pure-content row wrapped in a Button: outer.Click={_wrappedOuterClicks} row.Tapped={_wrappedRowTaps} row.IsOpen={_wrappedRow.IsOpen}");

        _wrappedRowTaps = 0;
        _wrappedOuterClicks = 0;
        Drag(CenterOf(_wrappedRow), -120, 0);
        Console.WriteLine($"T7 INFO drag inside a pure-content row wrapped in a Button: row.IsOpen={_wrappedRow.IsOpen} outer.Click={_wrappedOuterClicks} row.Tapped={_wrappedRowTaps}");

        _wrappedInnerClicks = 0;
        _wrappedButtonOuterClicks = 0;
        _wrappedButtonRowTaps = 0;
        Tap(CenterOf(_wrappedInnerButton));
        Console.WriteLine($"T7 INFO tap inside a Button-content row wrapped in a Button: inner.Click={_wrappedInnerClicks} outer.Click={_wrappedButtonOuterClicks} row.Tapped={_wrappedButtonRowTaps} row.IsOpen={_wrappedButtonRow.IsOpen}");

        _wrappedInnerClicks = 0;
        _wrappedButtonOuterClicks = 0;
        _wrappedButtonRowTaps = 0;
        Drag(CenterOf(_wrappedInnerButton), -120, 0);
        Console.WriteLine($"T7 INFO drag inside a Button-content row wrapped in a Button: row.IsOpen={_wrappedButtonRow.IsOpen} inner.Click={_wrappedInnerClicks} outer.Click={_wrappedButtonOuterClicks} row.Tapped={_wrappedButtonRowTaps}");

        // Close every exploratory row so a repeated run starts from the same state.
        _wrappedRow.Close();
        _wrappedButtonRow.Close();
        Pump();
    }

    private static void Report(string id, bool pass, string observed)
    {
        if (!pass)
        {
            _failures++;
        }

        Console.WriteLine($"{id} {(pass ? "PASS" : "FAIL")} | {observed}");
    }

    private static Point CenterOf(Control control)
    {
        var center = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var point = control.TranslatePoint(center, _window);
        if (point is null)
        {
            throw new InvalidOperationException($"{control.GetType().Name} is not attached to the probe window");
        }

        return point.Value;
    }

    private static void Tap(Point point)
    {
        _window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Pump();
        _window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Pump();
    }

    private static void Drag(Point start, double dx, double dy, int steps = 8)
    {
        _window.MouseDown(start, MouseButton.Left, RawInputModifiers.LeftMouseButton);
        Pump();

        for (var step = 1; step <= steps; step++)
        {
            var point = new Point(start.X + dx * step / steps, start.Y + dy * step / steps);
            // The headless platform derives PointerPointProperties.IsLeftButtonPressed from the raw
            // modifiers, so a drag move must carry LeftMouseButton or handlers see the button as up.
            _window.MouseMove(point, RawInputModifiers.LeftMouseButton);
            Pump();
        }

        _window.MouseUp(new Point(start.X + dx, start.Y + dy), MouseButton.Left, RawInputModifiers.None);
        Pump();
    }

    private static void Pump(int iterations = 3)
    {
        for (var i = 0; i < iterations; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        }
    }
}
