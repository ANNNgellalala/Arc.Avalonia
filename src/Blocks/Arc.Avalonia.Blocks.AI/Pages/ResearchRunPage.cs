using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using K = Arc.Avalonia.Blocks.AI.AiKit;

namespace Arc.Avalonia.Blocks.AI.Pages;

/// <summary>
/// Research run: a planned deep research answer. Sub-queries show their checks, sources gather in a rail with
/// their read progress, and each citation lights the sentence and the source it supports.
/// </summary>
public sealed class ResearchRunPage : ArcItemPage
{
    public override string ItemSlug => "research-run";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "An answer engine or deep research feature that searches, reads and cites.",
        "Internal knowledge search where people must verify each claim.",
        "Any long streamed answer that benefits from visible sources and follow ups."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use AI chat for open ended conversation without sources, or agent run when the work edits files."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each turn is an article named by its question and the sources rail is a labelled complementary region.",
        "Citation chips are buttons named with the source number and title; a focused chip lights its sentence.",
        "Reading progress is exposed as text on each source, such as Reading, 60 percent, not only as a ring."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Answer text is revealed in whole word chunks while sub-queries settle from spinners into checks.",
        "Reduced motion reveals whole chunks without fades or blur and never runs a spinner."
    };

    public override string? ImplementationNotes =>
        "The answer is pre-written rather than streamed from a search pipeline, and the read percentages are "
        + "static. Citations light the paragraph and the matching source card on click and on Enter. The steps "
        + "line folds open instead of collapsing by measure, and the rail always sits beside the answer.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = K.Caption("Researched for 7s, 12 sources read. All results are simulated.");
        var stepsBody = new StackPanel { Spacing = 5, IsVisible = false };
        foreach (var (query, count) in new[]
                 {
                     ("support ticket deflection benchmarks 2026", "6 sites"),
                     ("AI resolution rate measurement", "4 sites"),
                     ("deflection vs containment definitions", "2 sites")
                 })
        {
            stepsBody.Children.Add(K.Row(7,
                new ArcIcon { Icon = "check", Size = 12, Foreground = K.Brush(ArcTokens.Success) },
                K.Text(query, "arc-caption"),
                K.Dot(ArcTokens.BorderStrong, 3),
                K.Caption(count)));
        }

        var paragraphs = new List<Border>();
        var sources = new List<Border>();

        void ClearHighlights()
        {
            foreach (var paragraph in paragraphs)
            {
                paragraph.Background = Brushes.Transparent;
            }

            foreach (var source in sources)
            {
                source.BorderBrush = K.Brush(ArcTokens.BorderSubtle);
                source.BorderThickness = new Thickness(1);
            }
        }

        void Select(int number)
        {
            ClearHighlights();
            if (number - 1 < paragraphs.Count)
            {
                paragraphs[number - 1].Background = K.Tint(ArcTokens.Accent, 0.09);
            }

            if (number - 1 < sources.Count)
            {
                sources[number - 1].BorderBrush = K.Brush(ArcTokens.Accent);
                sources[number - 1].BorderThickness = new Thickness(2);
            }

            status.Text = $"Reading the sentence supported by source {number}. Click another chip to move the highlight.";
        }

        Control Sentence(string text, int number)
        {
            var border = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(-8, 0, 0, 0),
                Child = K.Paragraph(text, Select, maxWidth: 640)
            };
            paragraphs.Add(border);
            return border;
        }

        Control SourceRow(int number, string title, string domain, string initial, double read)
        {
            var card = new Border
            {
                Background = K.Brush(ArcTokens.SurfaceMuted),
                BorderBrush = K.Brush(ArcTokens.BorderSubtle),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 7, 10, 7),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    Children =
                    {
                        new Border
                        {
                            Width = 22,
                            Height = 22,
                            CornerRadius = new CornerRadius(6),
                            Background = K.Tint(K.SeriesToken(number - 1), 0.16),
                            Child = new TextBlock
                            {
                                Text = initial,
                                FontSize = 10,
                                FontWeight = FontWeight.Medium,
                                Foreground = K.Brush(K.SeriesToken(number - 1)),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        },
                        K.Stack(1,
                            K.Ellipsis(title, "arc-caption", 150),
                            K.Caption(domain)),
                        K.Caption(read >= 1 ? "Read" : $"{read * 100:0}%")
                    }
                }
            };
            if (card.Child is Grid grid)
            {
                Grid.SetColumn(grid.Children[1], 1);
                grid.Children[1].Margin = new Thickness(8, 0, 8, 0);
                Grid.SetColumn(grid.Children[2], 2);
            }

            sources.Add(card);
            card.Focusable = true;
            card.Cursor = new Cursor(StandardCursorType.Hand);
            AutomationProperties.SetName(card, $"Source {number}: {title}, {domain}");
            card.PointerPressed += (_, _) => Select(number);
            card.KeyDown += (_, args) =>
            {
                if (args.Key is Key.Enter or Key.Space)
                {
                    Select(number);
                    args.Handled = true;
                }
            };
            return card;
        }

        var stepsToggle = K.Btn("Researched for 7s, 12 sources", ArcButtonVariant.Secondary, ArcButtonSize.Sm, icon: "check", trailingIcon: "chevron-down");
        stepsToggle.Click += (_, _) =>
        {
            stepsBody.IsVisible = !stepsBody.IsVisible;
            status.Text = stepsBody.IsVisible ? "Sub-queries shown." : "Sub-queries folded.";
        };

        var rerun = K.Chip("Rerun", icon: "refresh-cw");
        rerun.Click += (_, _) =>
        {
            ClearHighlights();
            status.Text = "Rerun finished in 6s with the same 12 sources. Simulated.";
        };

        var copy = K.Chip("Copy answer", icon: "copy");
        copy.Click += (_, _) => status.Text = "Answer copied. Clipboard content is simulated.";

        var followUps = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var question in new[]
                 {
                     "What counts as a deflection?", "How is containment different?",
                     "Which benchmark is most cited?"
                 })
        {
            var chip = K.Chip(question, icon: "arrow-up-right");
            chip.Click += (_, _) => status.Text = "Follow up asked. A simulated turn would be appended here.";
            chip.Margin = new Thickness(0, 0, 6, 4);
            followUps.Children.Add(chip);
        }

        var composer = new ArcInput { Placeholder = "Ask a follow up", Height = 42 };
        AutomationProperties.SetName(composer, "Follow up");
        var send = K.IconBtn("arrow-up", "Send question", variant: ArcButtonVariant.Primary);
        send.Width = 34;
        send.Height = 34;
        send.Padding = new Thickness(0);
        send.Click += (_, _) =>
        {
            status.Text = string.IsNullOrWhiteSpace(composer.Value)
                ? "Type a question first."
                : "Answer ready. A simulated turn would be appended here.";
        };

        var composerRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        composerRow.Children.Add(composer);
        Grid.SetColumn(send, 1);
        send.Margin = new Thickness(6, 0, 0, 0);
        composerRow.Children.Add(send);

        var left = new StackPanel
        {
            Spacing = 11,
            Children =
            {
                K.Stack(3,
                    K.Text("How do support teams measure ticket deflection?", "arc-heading", 620),
                    K.Caption("Deep research · 7s · 12 sources")),
                K.Row(6, stepsToggle),
                stepsBody,
                K.Divider(2),
                Sentence("Teams report deflection as the share of conversations that closed without a human "
                    + "reply. It is only comparable when the definition of a resolved conversation is published "
                    + "with it. [1]", 1),
                K.Stack(3,
                    K.Markdown("- AI-only resolution: no agent ever joined\n"
                        + "- Assisted resolution: an agent reviewed and sent the answer\n"
                        + "- Containment: the user did not reopen the issue within seven days", Select, 640)),
                Sentence("Across the three benchmarks we read, AI-only deflection lands between 22 and 41 percent, "
                    + "while containment is 9 points lower on average because reopened tickets are counted back. [2]", 2),
                K.Row(6, copy, rerun, K.Caption("286 words")),
                followUps,
                composerRow,
                status
            }
        };

        var addSources = K.Btn("Add sources", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () =>
        {
            status.Text = "Source picker opened. Simulated.";
        }, icon: "plus");
        addSources.HorizontalAlignment = HorizontalAlignment.Left;

        var railHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        railHeader.Children.Add(K.Label("Sources"));
        var railCount = K.Caption("12 read");
        Grid.SetColumn(railCount, 1);
        railHeader.Children.Add(railCount);

        var rail = new StackPanel
        {
            Spacing = 7,
            Children =
            {
                railHeader,
                SourceRow(1, "Ticket deflection benchmarks", "supportbench.io", "S", 1),
                SourceRow(2, "Containment vs deflection", "helpdeskweekly.com", "H", 1),
                SourceRow(3, "Resolution rate definitions", "docs.acme.dev", "D", 0.6),
                SourceRow(4, "Deflection on the status page", "status.acme.dev", "A", 0),
                addSources
            }
        };

        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,280") };
        columns.Children.Add(left);
        Grid.SetColumn(rail, 2);
        columns.Children.Add(rail);

        return K.Surface(columns, new Thickness(20, 16, 20, 16));
    }
}
