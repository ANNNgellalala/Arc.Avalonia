using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
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

    protected override Control BuildDemo(ArcDemoContext context)
    {
        return new ArcContextWindow
        {
            ModelId = "opus",
            Threshold = 0.8,
            Models = new List<ArcContextModel>
            {
                new() { Id = "opus", Name = "Claude Opus 5.5", Window = 1_000_000, Reserve = 40_000 },
                new() { Id = "haiku", Name = "Claude Haiku 5.5", Window = 200_000, Reserve = 16_000 }
            },
            Items = new List<ArcContextItem>
            {
                new() { Id = "system", Category = ArcContextCategory.System, Label = "System prompt", Tokens = 15_000 },
                new() { Id = "tools", Category = ArcContextCategory.Tools, Label = "Tool definitions", Tokens = 45_000 },
                new() { Id = "files", Category = ArcContextCategory.Files, Label = "attached files", Tokens = 60_000 },
                new() { Id = "summary", Category = ArcContextCategory.Summary, Label = "Summary of 4 earlier turns", Tokens = 25_000 },
                new() { Id = "messages", Category = ArcContextCategory.Messages, Label = "Conversation", Tokens = 375_000 }
            }
        };
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
