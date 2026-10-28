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
/// Knowledge base: sources ingest through parse, chunk, embed and index, then a question shows the chunks that
/// come back with their scores. Reranking reorders the same results in place.
/// </summary>
public sealed class KnowledgeBasePage : ArcItemPage
{
    public override string ItemSlug => "knowledge-base";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Admin screens for a RAG or support assistant where people add documents, watch them index and test retrieval.",
        "Explaining or tuning chunking, hybrid search and reranking with a working test bench.",
        "Debugging why an assistant answered from the wrong source by looking at scores and matches."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use document extract to pull fields from one document, or agent memory for what an agent remembers."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each pipeline is a progress bar with a value text naming its step, such as Embed, step 3 of 4.",
        "A result row is a button whose name carries its score and any rank change after reranking.",
        "Indexing milestones, failures, results and reranking are announced in one polite status region."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Turning on Rerank moves each result to its new rank on a critically damped layout spring.",
        "Reduced motion places the new order and scores immediately and keeps every label and announcement."
    };

    public override string? ImplementationNotes =>
        "Ingestion progress is a snapshot: one source is mid pipeline, the rest are indexed. Search returns the "
        + "same sample chunks and reranking reorders them; chunk settings and reranker scores are not shown as controls.";

    private sealed class Source
    {
        public string Name { get; init; } = string.Empty;
        public string Kind { get; init; } = "PDF";
        public string Icon { get; init; } = "file";
        public int Pages { get; init; }
        public int Chunks { get; init; }
        public string Step { get; set; } = "indexed";
        public double Progress { get; set; } = 1;
    }

    private sealed record Hit(string Title, string Location, double Score, string Text);

    private static readonly Hit[] Hits =
    {
        new("Refund policy", "Notion · Refunds", 0.87,
            "Refunds are issued within 14 days of the request. Original shipping is refunded once per order."),
        new("Support handbook", "PDF · p. 42", 0.74,
            "Agents confirm the refund window with the customer before opening a return."),
        new("Onboarding drive", "Drive · Refund FAQ", 0.61,
            "Billing questions about partial shipments should be routed to the refunds queue.")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var sources = new List<Source>
        {
            new() { Name = "Support handbook.pdf", Kind = "PDF", Icon = "file", Pages = 42, Chunks = 318 },
            new() { Name = "Refund policy", Kind = "Notion", Icon = "layers", Pages = 8, Chunks = 64 },
            new() { Name = "Onboarding drive", Kind = "Drive", Icon = "folder", Pages = 31, Chunks = 0, Step = "embed", Progress = 0.62 }
        };
        var rerank = false;
        var query = "How long do customers have to ask for a refund?";
        var status = K.Caption($"3 sources · {sources.Sum(source => source.Chunks):0} chunks. Retrieval is simulated.");
        var sourceHost = new StackPanel { Spacing = 6 };
        var resultHost = new StackPanel { Spacing = 5 };

        void RenderSources()
        {
            sourceHost.Children.Clear();
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), RowDefinitions = new RowDefinitions("Auto,8,Auto") };
            for (var index = 0; index < sources.Count; index++)
            {
                var source = sources[index];
                var steps = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
                var order = new[] { "parse", "chunk", "embed", "index" };
                var reached = source.Step == "indexed" ? 4 : Math.Max(1, Array.IndexOf(order, source.Step) + 1);
                for (var step = 0; step < 4; step++)
                {
                    steps.Children.Add(new Border
                    {
                        Width = 22,
                        Height = 4,
                        CornerRadius = new CornerRadius(2),
                        Background = step < reached
                            ? K.Brush(source.Step == "indexed" ? ArcTokens.Success : ArcTokens.Accent)
                            : K.Brush(ArcTokens.SurfaceMuted)
                    });
                }

                var body = new StackPanel { Spacing = 3 };
                body.Children.Add(K.Row(6,
                    new ArcIcon { Icon = source.Icon, Size = 12 },
                    K.Ellipsis(source.Name, "arc-caption", 150)));
                body.Children.Add(steps);
                body.Children.Add(K.Caption(source.Step == "indexed"
                    ? $"{source.Pages} pages · {source.Chunks} chunks"
                    : $"{source.Step} · {source.Progress * 100:0}%"));

                var card = new Border
                {
                    Background = K.Brush(ArcTokens.SurfaceMuted),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(9, 6, 9, 7),
                    Child = body
                };
                Grid.SetRow(card, index / 2 == 0 ? 0 : 2);
                Grid.SetColumn(card, index % 2 == 0 ? 0 : 2);
                grid.Children.Add(card);
            }

            sourceHost.Children.Add(grid);
        }

        void RenderResults()
        {
            resultHost.Children.Clear();
            var ordered = rerank
                ? Hits.OrderByDescending(hit => hit.Score).Reverse().ToArray()
                : Hits;
            for (var index = 0; index < ordered.Length; index++)
            {
                var hit = ordered[index];
                var rank = index + 1;
                var before = Array.FindIndex(Hits, candidate => candidate == hit) + 1;
                var body = new StackPanel { Spacing = 3 };
                body.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    Children =
                    {
                        K.Caption(rank.ToString()),
                        K.Row(6,
                            K.Text(hit.Title, "arc-caption"),
                            K.Caption(hit.Location)),
                        K.Caption(hit.Score.ToString("0.00"))
                    }
                });
                if (body.Children[0] is Grid head)
                {
                    head.Children[1].Margin = new Thickness(8, 0, 8, 0);
                    Grid.SetColumn(head.Children[1], 1);
                    Grid.SetColumn(head.Children[2], 2);
                }

                body.Children.Add(K.Text(hit.Text, "arc-caption", 560));
                if (rerank)
                {
                    var moved = before - rank;
                    body.Children.Add(K.Caption(moved == 0 ? "Reranked, same position" : moved > 0 ? $"Up {moved} after reranking" : $"Down {-moved} after reranking"));
                }
                else
                {
                    body.Children.Add(new Border
                    {
                        Width = 160,
                        Height = 4,
                        CornerRadius = new CornerRadius(2),
                        Background = K.Brush(ArcTokens.BorderSubtle),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Child = new Border
                        {
                            Width = 160 * hit.Score,
                            Height = 4,
                            CornerRadius = new CornerRadius(2),
                            Background = K.Brush(ArcTokens.Accent),
                            HorizontalAlignment = HorizontalAlignment.Left
                        }
                    });
                }

                var row = new Border
                {
                    Background = K.Brush(ArcTokens.SurfaceMuted),
                    BorderBrush = rank == 1 && rerank ? K.Tint(ArcTokens.Accent, 0.32) : Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(9, 6, 9, 7),
                    Child = body
                };
                AutomationProperties.SetName(row,
                    $"Result {rank}: {hit.Title}, {hit.Location}, score {hit.Score:0.00}"
                    + (rerank && before != rank ? $", was {before}" : string.Empty));
                resultHost.Children.Add(row);
            }
        }

        var rerankSwitch = K.Toggle("Rerank", false, on =>
        {
            rerank = on;
            RenderResults();
            status.Text = on
                ? "Reranked 3 of 20 candidates. The refund policy chunk moved to the top."
                : "Rerank off. Results are ordered by similarity.";
        });

        var search = K.Btn("Search", ArcButtonVariant.Primary, ArcButtonSize.Sm, null, icon: "search");
        search.MinWidth = 92;
        search.Click += (_, _) =>
        {
            RenderResults();
            status.Text = $"3 chunks returned for the question, top similarity 0.87. Simulated retrieval.";
        };

        var input = new ArcInput { Value = query, Height = 40 };
        AutomationProperties.SetName(input, "Test question");
        var searchRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        searchRow.Children.Add(input);
        Grid.SetColumn(search, 1);
        search.Margin = new Thickness(6, 0, 0, 0);
        searchRow.Children.Add(search);

        var addSource = K.Btn("Add source", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
        {
            sources.Insert(0, new Source
            {
                Name = "Returns playbook.docx", Kind = "File", Icon = "file", Pages = 12, Chunks = 0,
                Step = "parse", Progress = 0.2
            });
            RenderSources();
            status.Text = "Returns playbook.docx added and parsing. Ingestion is simulated.";
        }, icon: "plus");

        RenderSources();
        RenderResults();

        var content = new StackPanel
        {
            Spacing = 9,
            MaxWidth = 800,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        K.Stack(2,
                            K.Text("Support knowledge base", "arc-heading"),
                            K.Caption("3 sources · 382 chunks · 512 token chunks")),
                        K.Row(4, addSource, K.IconBtn("sliders-horizontal", "Chunking settings"))
                    }
                },
                K.Caption("Sources"),
                sourceHost,
                K.Divider(2),

                searchRow,
                K.Row(8, rerankSwitch, K.Caption("Hybrid search on · top 3")),
                resultHost,
                status
            }
        };

        if (content.Children[0] is Grid head)
        {
            Grid.SetColumn(head.Children[1], 1);
        }

        return K.Surface(new Border
        {
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 900
        }, new Thickness(20, 12, 20, 12));
    }
}
