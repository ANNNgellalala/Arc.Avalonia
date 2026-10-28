using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using K = Arc.Avalonia.BlocksAI.AiKit;

namespace Arc.Avalonia.BlocksAI.Pages;

/// <summary>
/// Agent memory: what an assistant remembers, grouped and filterable. Forgetting a memory, filing a captured
/// one and pausing saved memories all change the list in place.
/// </summary>
public sealed class AgentMemoryPage : ArcItemPage
{
    public override string ItemSlug => "agent-memory";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Settings for an assistant that remembers people across chats, where people see, correct and delete what it knows.",
        "The moment an assistant saves something from a chat, with a way to see where it was filed.",
        "Any small personal knowledge store grouped by kind, where each entry is one sentence with a source."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use knowledge base for documents an agent retrieves from, or agent routines for scheduled instructions."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Filter pills are toggle buttons and the selected one is the only tab stop.",
        "Switches are labelled by their visible titles and described by the line beneath them.",
        "A polite status announces captures, adds, saves, forgets with their undo window and switch changes."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "A filed memory glides to its group on the smooth spring under a tint that fades over a second.",
        "Reduced motion places the row immediately, stops the highlight and keeps every announcement."
    };

    public override string? ImplementationNotes =>
        "Forget removes the row immediately instead of holding a five second undo window, and the capture pill "
        + "files one sample memory, and the list scrolls inside a fixed height.";

    private sealed record Memory(string Id, string Text, string Group, string Source);

    private static readonly Memory[] Seed =
    {
        new("m1", "Prefers metric units and 24 hour times", "About", "From a chat · 4 Oct"),
        new("m2", "Based in Bristol, United Kingdom", "About", "Added by you · 12 Sep"),
        new("m3", "Prefers pnpm over npm in install commands", "Preferences", "From a chat · 2 Oct")
    };

    private static readonly string[] Groups = { "All", "About", "Preferences", "Projects" };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var memories = Seed.ToList();
        var filter = "All";
        var saved = true;
        var captured = true;
        var status = K.Caption("3 memories, 62% of the budget. Claude sorts new memories with a keyword guess.");

        var list = new StackPanel { Spacing = 4 };
        var capturedPill = new Border
        {
            Background = K.Tint(ArcTokens.Accent, 0.1),
            BorderBrush = K.Tint(ArcTokens.Accent, 0.28),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 6, 6, 6),
            Child = K.Row(7,
                new ArcIcon { Icon = "sparkles", Size = 13, Foreground = K.Brush(ArcTokens.Accent) },
                K.Text("Noted: you prefer pnpm over npm", "arc-caption"),
                K.Chip("Save", icon: "plus"))
        };
        var capturedButton = (ArcButton)((StackPanel)capturedPill.Child).Children[2];
        var capacity = K.Text("62% full", "arc-caption");
        var capacityBar = K.Bar(62, 100, ArcProgressSize.Sm);
        var listNote = K.Caption("Saved memories are off. The assistant will not keep new facts.");
        listNote.IsVisible = false;

        void RenderList()
        {
            list.Children.Clear();
            var visible = memories.Where(memory => filter == "All" || memory.Group == filter).ToList();
            if (visible.Count == 0)
            {
                list.Children.Add(new Border
                {
                    Background = K.Brush(ArcTokens.SurfaceMuted),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(12),
                    Child = K.Caption($"Nothing saved in {filter} yet.")
                });
                return;
            }

            foreach (var group in visible.GroupBy(memory => memory.Group))
            {
                var heading = K.Row(6, K.Label(group.Key), K.Caption(group.Count().ToString()));
                heading.Margin = new Thickness(0, 4, 0, 1);
                list.Children.Add(heading);

                foreach (var memory in group)
                {
                    var id = memory.Id;
                    var edit = K.IconBtn("pencil", $"Edit memory: {memory.Text}", size: 13);
                    edit.Click += (_, _) => status.Text = "Editing this memory in place. Simulated save.";
                    var forget = K.IconBtn("trash-2", $"Forget memory: {memory.Text}", size: 13);
                    forget.Click += (_, _) =>
                    {
                        memories.Remove(memory);
                        status.Text = $"Forgotten: {memory.Text}. Undo is available for five seconds in the real block.";
                        Refresh();
                    };

                    var row = new Border
                    {
                        Background = K.Brush(ArcTokens.Surface),
                        BorderBrush = K.Brush(ArcTokens.BorderSubtle),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(10, 5, 6, 5),
                        Opacity = saved ? 1 : 0.45,
                        Child = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                            Children =
                            {
                                K.Stack(2,
                                    K.Text(memory.Text, "arc-body-sm"),
                                    K.Caption(memory.Source)),
                                K.Row(2, edit, forget)
                            }
                        }
                    };
                    if (row.Child is Grid rowGrid)
                    {
                        Grid.SetColumn(rowGrid.Children[1], 1);
                    }

                    AutomationProperties.SetName(row, memory.Text);
                    list.Children.Add(row);
                }
            }
        }

        void Refresh()
        {
            RenderList();
            capacity.Text = $"{Math.Min(100, memories.Count * 15 + 17)}% full";
            capacityBar.Value = Math.Min(100, memories.Count * 15 + 17);
            capturedPill.IsVisible = captured && saved;
            listNote.IsVisible = !saved;
            status.Text = memories.Count == 0
                ? "No memories left. The assistant starts from scratch."
                : $"{memories.Count} memories across {memories.Select(memory => memory.Group).Distinct().Count()} groups.";
        }

        // Filter pills.
        var filterRow = new WrapPanel { Orientation = Orientation.Horizontal };
        void RenderFilters()
        {
            filterRow.Children.Clear();
            foreach (var group in Groups)
            {
                var value = group;
                var count = value == "All" ? memories.Count : memories.Count(memory => memory.Group == value);
                var chip = K.Chip($"{value} {count}", selected: filter == value);
                chip.Click += (_, _) =>
                {
                    filter = value;
                    RenderFilters();
                    RenderList();
                    status.Text = filter == "All" ? "Showing every memory." : $"Showing {filter} only.";
                };
                chip.Margin = new Thickness(0, 0, 5, 4);
                filterRow.Children.Add(chip);
            }
        }

        var savedSwitch = K.Toggle("Saved memories", true, on =>
        {
            saved = on;
            capturedPill.IsVisible = captured && saved;
            listNote.IsVisible = !saved;
            RenderList();
            status.Text = on ? "Saved memories on. New facts are kept." : "Saved memories off. Nothing new is kept.";
        });
        var historySwitch = K.Toggle("Chat history", true, on =>
        {
            status.Text = on ? "Chat history on. The assistant may quote recent chats." : "Chat history off. Only saved memories are used.";
        });

        capturedButton.Click += (_, _) =>
        {
            if (!captured)
            {
                return;
            }

            captured = false;
            memories.Add(new Memory("m6", "Prefers pnpm over npm in install commands", "Preferences", "Noted in this chat"));
            filter = "All";
            RenderFilters();
            Refresh();
            status.Text = "Saved to Preferences. The pill folds away once the row lands.";
        };

        var addField = new ArcInput { Placeholder = "Tell the assistant something to remember", Height = 40 };
        AutomationProperties.SetName(addField, "New memory");
        var add = K.IconBtn("plus", "Add memory", variant: ArcButtonVariant.Primary);
        add.Width = 34;
        add.Height = 34;
        add.Padding = new Thickness(0);
        add.Click += (_, _) =>
        {
            var text = (addField.Value ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                status.Text = "Type a memory first.";
                return;
            }

            memories.Add(new Memory("m" + (memories.Count + 1), text, "About", "Added by you · now"));
            addField.Value = string.Empty;
            filter = "All";
            RenderFilters();
            Refresh();
            status.Text = "Added to About you. The keyword guess is simulated.";
        };

        var addRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        addRow.Children.Add(addField);
        Grid.SetColumn(add, 1);
        add.Margin = new Thickness(6, 0, 0, 0);
        addRow.Children.Add(add);

        RenderFilters();
        RenderList();

        var content = new StackPanel
        {
            Spacing = 6,
            MaxWidth = 660,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        K.Stack(2,
                            K.Text("Memory", "arc-heading"),
                            K.Caption("What Claude remembers between chats")),
                        K.Stack(2, capacity, capacityBar)
                    }
                },
                K.Divider(2),
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    ColumnSpacing = 16,
                    Children =
                    {
                        K.Stack(2, savedSwitch, K.Caption("Facts it keeps and reuses in later chats")),
                        K.Stack(2, historySwitch, K.Caption("Let it quote from recent conversations"))
                    }
                },
                filterRow,
                capturedPill,
                listNote,
                K.Scroller(list, 228),
                addRow,
                status
            }
        };

        if (content.Children[0] is Grid head)
        {
            Grid.SetColumn(head.Children[1], 1);
        }

        if (content.Children[2] is Grid switchGrid)
        {
            Grid.SetColumn(switchGrid.Children[1], 1);
        }

        return K.Surface(new Border
        {
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 760
        }, new Thickness(22, 16, 22, 16));
    }
}
