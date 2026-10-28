using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.LifeToday.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.LifeToday.Pages;

/// <summary>
/// The quick capture bar in its five host-supplied parse states. The page plays the host: a state button
/// supplies sample text and chips, and the event card records what the bar would hand back.
/// </summary>
public sealed class QuickCaptureBarPage : ArcPage
{
    public override string Title => "Quick capture bar";

    public override string Group => "P0 · Foundation";

    public override string? Description =>
        "One sentence in, parsed chips out, confirmation in three seconds. The bar only edits and displays; " +
        "parsing, persistence and the global hotkey stay with the host.";

    public override int Order => 20022;

    public override Control Build(ArcDemoContext context)
    {
        var bar = new ArcQuickCaptureBar { Width = 720 };
        var events = ArcUi.Caption("No events yet. Press Capture, remove a chip, or switch a state.");
        events.TextWrapping = TextWrapping.Wrap;
        events.MaxWidth = 720;

        bar.SubmitRequested += text =>
            events.Text = $"SubmitRequested(\"{text}\") \u00b7 the host would now parse and persist.";
        bar.ChipRemoveRequested += id =>
            events.Text = $"ChipRemoveRequested(\"{id}\") \u00b7 the host would drop that parsed field.";
        bar.CancelRequested += (_, _) =>
            events.Text = "CancelRequested \u00b7 the host would close the bar and keep the draft.";

        var presets = Presets().ToList();

        var stateRow = new WrapPanel { Orientation = Orientation.Horizontal };
        var hint = ArcUi.Caption("Host state:");
        hint.Margin = new Thickness(0, 0, 10, 0);
        hint.VerticalAlignment = VerticalAlignment.Center;
        stateRow.Children.Add(hint);

        foreach (var preset in presets)
        {
            var button = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Content = preset.Label
            };
            ArcUi.Name(button, $"Show {preset.Label} state");
            button.Click += (_, _) =>
            {
                bar.Chips = preset.Chips;
                bar.Text = preset.Text;
                bar.State = preset.State;
                events.Text = $"Host set state to {preset.Label} \u00b7 the bar only displays it.";
            };
            button.Margin = new Thickness(0, 0, 6, 6);
            stateRow.Children.Add(button);
        }

        var initial = presets[2];
        bar.Chips = initial.Chips;
        bar.Text = initial.Text;
        bar.State = initial.State;
        events.Text = "Showing the Parsed state from the host's static sample data.";

        var live = new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { bar, stateRow }
        };

        var eventCard = new ArcCard
        {
            Padding = new Thickness(16),
            Content = ArcUi.Stack(6, ArcUi.Label("Event output"), events)
        };

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Quick capture bar",
                    "The field holds the draft, the chips are the host's parsed details and each one can be " +
                    "removed. Parsing shows a spinner and holds capture; ambiguous and error states explain " +
                    "what the host needs. Enter captures, Escape cancels.",
                    live),

                DemoUi.Section(
                    "What the host owns",
                    "The control never parses, never writes and never registers the hotkey. It raises " +
                    "SubmitRequested(text), ChipRemoveRequested(id) and CancelRequested, and renders whatever " +
                    "state the host supplies.",
                    eventCard)
            }
        };
    }

    private static IEnumerable<Preset> Presets()
    {
        yield return new Preset(
            "Idle",
            ArcQuickCaptureState.Idle,
            string.Empty,
            null);

        yield return new Preset(
            "Parsing",
            ArcQuickCaptureState.Parsing,
            "Lunch with Priya tomorrow at 1 pm",
            null);

        yield return new Preset(
            "Parsed",
            ArcQuickCaptureState.Parsed,
            "Dinner with Zhang Wei on Friday at 7 pm, \u00a5280 for two",
            new List<ArcQuickCaptureChip>
            {
                new("person", ArcQuickCaptureChipKind.Person, "Zhang Wei"),
                new("time", ArcQuickCaptureChipKind.Time, "Fri 19:00"),
                new("amount", ArcQuickCaptureChipKind.Amount, "\u00a5280"),
                new("list", ArcQuickCaptureChipKind.List, "Two people")
            });

        yield return new Preset(
            "Ambiguous",
            ArcQuickCaptureState.Ambiguous,
            "Call mom next week",
            new List<ArcQuickCaptureChip>
            {
                new("time", ArcQuickCaptureChipKind.Time, "Next week \u2014 which day?"),
                new("person", ArcQuickCaptureChipKind.Person, "Mom")
            });

        yield return new Preset(
            "Error",
            ArcQuickCaptureState.Error,
            "Remind me to call",
            null);
    }

    private sealed record Preset(
        string Label,
        ArcQuickCaptureState State,
        string Text,
        IList<ArcQuickCaptureChip>? Chips);
}
