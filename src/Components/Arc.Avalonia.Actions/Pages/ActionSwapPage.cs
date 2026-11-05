using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Action swap's completeness preview: the accent, quiet, success and neutral tones pinned to one state each,
/// plus a disabled copy. Declared state keys match Docs/action-swap.json.
/// </summary>
public sealed class ActionSwapPage : ArcItemPage
{
    public override string ItemSlug => "action-swap";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "tone-accent", "tone-quiet", "tone-success", "tone-neutral", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        var disabled = Follow("follow");
        disabled.IsEnabled = false;

        panel.Children.Add(Row("强调色",
            Follow("follow"),
            Follow("following"),
            Invite("sent"),
            Save("save")));

        panel.Children.Add(Row("不可用",
            disabled));

        return panel;
    }

    private static ArcActionSwap Follow(string value) => new()
    {
        Value = value,
        States = new[]
        {
            new ArcActionSwapState { Id = "follow", Label = "Follow", Icon = "plus", Tone = ArcActionSwapTone.Accent },
            new ArcActionSwapState { Id = "following", Label = "Following", Icon = "check", Tone = ArcActionSwapTone.Quiet }
        }
    };

    private static ArcActionSwap Invite(string value) => new()
    {
        Value = value,
        States = new[]
        {
            new ArcActionSwapState { Id = "send", Label = "Send invite", Icon = "send", Tone = ArcActionSwapTone.Accent },
            new ArcActionSwapState
            {
                Id = "sent",
                Label = "Sent",
                Icon = "check",
                Tone = ArcActionSwapTone.Success
            }
        }
    };

    private static ArcActionSwap Save(string value) => new()
    {
        Value = value,
        States = new[]
        {
            new ArcActionSwapState { Id = "save", Label = "Save draft", Icon = "save", Tone = ArcActionSwapTone.Neutral },
            new ArcActionSwapState { Id = "saved", Label = "Saved", Icon = "check", Tone = ArcActionSwapTone.Success }
        }
    };

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
}
