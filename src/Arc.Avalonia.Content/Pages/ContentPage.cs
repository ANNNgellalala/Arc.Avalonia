using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Arc.Avalonia.Content.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Content.Pages;

/// <summary>Writing surfaces and user-ordered lists.</summary>
public sealed class ContentPage : ArcPage
{
    public override string Title => "Content";

    public override string Group => "Content";

    public override string? Description =>
        "Write in Markdown with a live preview, then let people put their own list in order.";

    public override int Order => 900;

    public override Control Build(ArcDemoContext context)
    {
        var editor = new ArcRichTextEditor
        {
            Width = 820,
            Height = 390,
            Label = "Release notes editor",
            Placeholder = "Write your update",
            DefaultMarkdown =
                "# Release notes\n\n" +
                "Search is now **twice as fast**, with a calmer *dark* theme and a new " +
                "[command palette](https://uiarc.dev).\n\n" +
                "- Fuzzy file search\n" +
                "- New **dark** theme\n" +
                "- `ArcButton` focus fix\n\n" +
                "> Small touches add up."
        };
        editor.AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(() => editor.SelectForDemo("twice as fast"), DispatcherPriority.Background);

        var priorities = new List<ArcReorderItem>
        {
            new() { Id = "pricing", Title = "Ship pricing page", Detail = "Due Friday" },
            new() { Id = "login", Title = "Fix login redirect", Detail = "Blocked by SSO" },
            new() { Id = "changelog", Title = "Write changelog", Detail = "Draft ready" },
            new()
            {
                Id = "email", Title = "Refresh onboarding email", Detail = "Waiting on brand",
                Disabled = true
            },
            new() { Id = "demo", Title = "Record the demo", Detail = "Thursday" }
        };

        var list = new ArcReorderableList
        {
            Width = 760,
            Items = priorities,
            Label = "Release priorities"
        };

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Rich text editor",
                    "Use for comments, notes, and posts that need Markdown shortcuts, a selection toolbar, and clean output.",
                    editor),
                DemoUi.Section(
                    "Reorderable list",
                    "Use for short priorities, playlists, and queues where people order the rows themselves.",
                    list)
            }
        };
    }
}
