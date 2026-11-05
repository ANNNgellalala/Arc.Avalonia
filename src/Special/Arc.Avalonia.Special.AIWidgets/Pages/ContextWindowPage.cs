using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIWidgets.Controls;

namespace Arc.Avalonia.Special.AIWidgets.Pages;

/// <summary>
/// Catalog page for the context window. The demo starts on a 1M token model with the conversation just over half
/// the window, so sending a couple of messages crosses the compaction threshold and folds the oldest turns.
/// </summary>
public sealed class ContextWindowPage : ArcItemPage
{
    public override string ItemSlug => "context-window";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-comfortable", "state-near-limit", "state-compacted"
    };

    private static List<ArcContextModel> Models() => new()
    {
        new() { Id = "opus", Name = "Claude Opus 5.5", Window = 1_000_000, Reserve = 40_000 },
        new() { Id = "haiku", Name = "Claude Haiku 5.5", Window = 200_000, Reserve = 16_000 }
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var comfortable = new ArcContextWindow
        {
            ModelId = "opus",
            Threshold = 0.8,
            Models = Models(),
            Items = new List<ArcContextItem>
            {
                new() { Id = "system", Category = ArcContextCategory.System, Label = "System prompt", Tokens = 15_000 },
                new() { Id = "tools", Category = ArcContextCategory.Tools, Label = "Tool definitions", Tokens = 45_000 },
                new() { Id = "files", Category = ArcContextCategory.Files, Label = "attached files", Tokens = 60_000 },
                new() { Id = "summary", Category = ArcContextCategory.Summary, Label = "Summary of 4 earlier turns", Tokens = 25_000 },
                new() { Id = "messages", Category = ArcContextCategory.Messages, Label = "Conversation", Tokens = 375_000 }
            }
        };

        var nearLimit = new ArcContextWindow
        {
            ModelId = "opus",
            Threshold = 0.8,
            Models = Models(),
            Items = new List<ArcContextItem>
            {
                new() { Id = "system", Category = ArcContextCategory.System, Label = "System prompt", Tokens = 20_000 },
                new() { Id = "tools", Category = ArcContextCategory.Tools, Label = "Tool definitions", Tokens = 60_000 },
                new() { Id = "files", Category = ArcContextCategory.Files, Label = "attached files", Tokens = 120_000 },
                new() { Id = "summary", Category = ArcContextCategory.Summary, Label = "Summary of 9 earlier turns", Tokens = 80_000 },
                new() { Id = "messages", Category = ArcContextCategory.Messages, Label = "Conversation", Tokens = 570_000 }
            }
        };

        var compacted = new ArcContextWindow
        {
            ModelId = "opus",
            Threshold = 0.8,
            Models = Models(),
            Items = new List<ArcContextItem>
            {
                new() { Id = "system", Category = ArcContextCategory.System, Label = "System prompt", Tokens = 15_000 },
                new() { Id = "tools", Category = ArcContextCategory.Tools, Label = "Tool definitions", Tokens = 45_000 },
                new() { Id = "files", Category = ArcContextCategory.Files, Label = "attached files", Tokens = 40_000 },
                new() { Id = "summary", Category = ArcContextCategory.Summary, Label = "Summary of 12 earlier turns", Tokens = 160_000 },
                new() { Id = "messages", Category = ArcContextCategory.Messages, Label = "Conversation since compaction", Tokens = 40_000 }
            }
        };

        var panel = new StackPanel { Spacing = 18, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(WithCaption("舒适区：窗口用了一半多，离虚线还远", comfortable));
        panel.Children.Add(WithCaption("接近阈值：填充已越过 80% 虚线，下一轮发送就会压缩", nearLimit));
        panel.Children.Add(WithCaption("压缩后：早期对话折成摘要，占用回落，摘要块明显变大", compacted));
        return panel;
    }

    private static Control WithCaption(string caption, Control content)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(Sv.Caption(caption));
        stack.Children.Add(content);
        return stack;
    }

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Developers or power users need to see what fills a model's context and when it will compact.",
        "An agent or coding tool compacts long sessions and the effect should be understandable.",
        "Docs explain context windows, compaction and the output reserve."
    };


    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Tab moves through the model menu, Send message, Attach file and Compact now.",
        "Enter or Space opens the model menu or runs an action; Escape closes the menu and returns focus to its trigger.",
        "The grid is one image with a full text description and the legend lists every part with tokens and percent, so nothing depends on colour."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "New tokens fill their cells as a short wave from where the change happened, never longer than a few hundred milliseconds.",
        "Compaction dims the folded messages, lands the summary after the files and returns the freed cells to free space in the same wave.",
        "Pointing at a legend row lights its cells and quiets the rest; reduced motion changes cells and numbers at once."
    };

    public override string? ImplementationNotes =>
        "Recreated from the public context-window documentation. The grid is one drawn pass over 200 cells rather than 200 animated nodes, so the fill " +
        "wave is a single pulse instead of a per-cell delay, and the model menu is a flyout rather than a dropdown menu. Token counts are sample numbers.";
}
