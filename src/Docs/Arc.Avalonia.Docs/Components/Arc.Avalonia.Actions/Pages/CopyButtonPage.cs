using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Copy button's completeness preview: the idle face, the Copied and Failed confirmations kept on screen so a
/// screenshot shows them, plus the icon-only and ghost forms. Declared state keys match
/// Docs/copy-button.json.
/// </summary>
public sealed class CopyButtonPage : ArcItemPage
{
    private const string SampleKey = "arc_live_51N8kQ2Lm4X";

    public override string ItemSlug => "copy-button";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-idle", "state-copied", "state-failed", "variant-icon-only", "variant-ghost"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("状态",
            new ArcCopyButton { Value = SampleKey, Label = "Copy key" },
            Pinned("Copy key", ArcCopyState.Copied),
            Pinned("Copy key", ArcCopyState.Failed)));

        panel.Children.Add(Row("外观",
            new ArcCopyButton { Value = SampleKey, Label = "Copy key", IconOnly = true },
            new ArcCopyButton { Value = SampleKey, Label = "Copy link", Variant = ArcButtonVariant.Ghost }));

        return panel;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 10, 0);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }

    /// <summary>
    /// Builds a copy button pinned to a confirmation face. The control returns to Idle after about two
    /// seconds by design, so the preview re-asserts the face whenever the timer resets it; the handler is
    /// detached when the preview leaves the tree, so it cannot keep the timer alive off-page.
    /// </summary>
    private static ArcCopyButton Pinned(string label, ArcCopyState state)
    {
        var button = new ArcCopyButton { Label = label, Value = SampleKey };

        void Keep(object? sender, AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == ArcCopyButton.StateProperty && button.State != state)
            {
                button.State = state;
            }
        }

        button.PropertyChanged += Keep;
        button.DetachedFromVisualTree += (_, _) => button.PropertyChanged -= Keep;
        button.State = state;
        return button;
    }
}
