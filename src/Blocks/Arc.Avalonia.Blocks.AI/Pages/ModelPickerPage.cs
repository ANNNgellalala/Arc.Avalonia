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
using Arc.Avalonia.Theme;
using K = Arc.Avalonia.Blocks.AI.AiKit;

namespace Arc.Avalonia.Blocks.AI.Pages;

/// <summary>
/// Model picker: one quiet pill that names the current model and effort, with the open menu beside it. Picking
/// a model or an effort updates the pill and the review in place.
/// </summary>
public sealed class ModelPickerPage : ArcItemPage
{
    public override string ItemSlug => "model-picker";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Choosing the model and effort for a chat, an agent or a playground.",
        "A default model setting with a short review of what the model costs and how much context it takes.",
        "Embedding the picker alone in a composer toolbar, without the review."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use model arena to compare two answers side by side, or a plain select for options with no descriptions."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The pill opens on Enter, Space or Arrow down and names the current model and effort in its label.",
        "Model rows are radio items, so a screen reader announces the chosen model; Escape returns focus to the pill.",
        "Each change is announced in a polite status region, and the review under the pill is a labelled section."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The menu opens from the pill edge with a short offset and fade; the pill width follows its new label on a spring.",
        "Reduced motion swaps the menu and the pill label in place and keeps every announcement."
    };

    public override string? ImplementationNotes =>
        "The menu is rendered open beside the pill so the block reads in one screenshot; in a real popover it "
        + "anchors above the pill and closes on Escape. Brand marks are monogram discs (the port has no brand "
        + "glyphs) and non-Anthropic prices are illustrative sample values.";

    private sealed record Spec(string Id, string Name, string Short, string Provider, string Blurb, string Context,
        string Output, string PriceIn, string PriceOut, string[] Efforts, string DefaultEffort);

    private static readonly Spec[] Specs =
    {
        new("claude-opus-5-5", "Claude Opus 5.5", "Opus 5.5", "anthropic", "Deepest reasoning for hard problems",
            "200k", "64k", "$15", "$75", new[] { "low", "medium", "high" }, "high"),
        new("claude-sonnet-5-5", "Claude Sonnet 5.5", "Sonnet 5.5", "anthropic", "Balanced speed and depth",
            "200k", "64k", "$3", "$15", new[] { "low", "medium", "high" }, "medium"),
        new("gpt-6-1-sol", "GPT-6.1 Sol", "GPT-6.1", "openai", "Broad general knowledge",
            "128k", "32k", "$2.50", "$10", new[] { "low", "medium", "high" }, "medium"),
        new("gemini-4-argon", "Gemini 4 Argon", "Gemini 4", "google", "Long context with strong recall",
            "1M", "64k", "$1.25", "$5", new[] { "low", "medium", "high" }, "low"),
        new("claude-haiku-5-5", "Claude Haiku 5.5", "Haiku 5.5", "anthropic", "Fastest answers at the lowest cost",
            "200k", "32k", "$0.80", "$4", new[] { "low", "medium" }, "low"),
        new("mistral-large-4", "Mistral Large 4", "Large 4", "mistral", "Open weights, EU hosted",
            "128k", "16k", "$2", "$6", new[] { "low", "medium", "high" }, "medium")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = K.Caption("Claude Opus 5.5 with high effort. Prices are sample values.");
        var selected = Specs[0];
        var effort = selected.DefaultEffort;
        var effortRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

        var contextValue = K.Text(selected.Context, "arc-body-sm");
        var outputValue = K.Text(selected.Output, "arc-body-sm");
        var priceInValue = K.Text($"{selected.PriceIn} / 1M", "arc-body-sm");
        var priceOutValue = K.Text($"{selected.PriceOut} / 1M", "arc-body-sm");
        var description = K.Text(selected.Blurb + ". Choose it when the question needs the deepest reasoning.",
            "arc-caption", 320);
        var effortValue = K.Text(selected.DefaultEffort, "arc-body-sm");

        var pill = new K.ModelChip(K.Model(selected.Id), showEffort: true, effort: selected.DefaultEffort);
        pill.Button.Click += (_, _) => status.Text = "Menu toggled. In the block it opens beside the pill.";
        var pillHost = pill.Button;

        var rows = new Dictionary<string, ArcButton>();
        var checks = new Dictionary<string, Control>();

        void Select(string id)
        {
            selected = Specs.First(spec => spec.Id == id);
            effort = selected.DefaultEffort;
            pill.Set(K.Model(selected.Id), effort);
            contextValue.Text = selected.Context;
            outputValue.Text = selected.Output;
            priceInValue.Text = $"{selected.PriceIn} / 1M";
            priceOutValue.Text = $"{selected.PriceOut} / 1M";
            description.Text = selected.Blurb + ". Choose it when the question needs the deepest reasoning.";
            effortValue.Text = effort;
            foreach (var pair in checks)
            {
                pair.Value.IsVisible = pair.Key == id;
            }

            RebuildEffort();
            status.Text = $"{selected.Name} chosen with {effort} effort. Review updated.";
        }

        void RebuildEffort()
        {
            effortRow.Children.Clear();
            foreach (var level in selected.Efforts)
            {
                var value = level;
                var chip = K.Chip(level, selected: value == effort);
                chip.Click += (_, _) =>
                {
                    effort = value;
                    effortValue.Text = effort;
                    pill.Set(K.Model(selected.Id), effort);
                    RebuildEffort();
                    status.Text = $"{selected.Name} now uses {effort} effort.";
                };
                effortRow.Children.Add(chip);
            }
        }

        Control MenuRow(Spec spec, bool featured)
        {
            var check = new ArcIcon
            {
                Icon = "check",
                Size = 14,
                Foreground = K.Brush(ArcTokens.Accent),
                IsVisible = spec.Id == selected.Id,
                VerticalAlignment = VerticalAlignment.Center
            };
            checks[spec.Id] = check;

            var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            body.Children.Add(new Border
            {
                Child = K.Mark(spec.Provider, 24),
                Margin = new Thickness(0, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(K.Text(spec.Name, "arc-body-sm"));
            text.Children.Add(K.Caption(featured ? spec.Blurb : $"{spec.Provider} · {spec.Context} context"));
            Grid.SetColumn(text, 1);
            body.Children.Add(text);
            Grid.SetColumn(check, 2);
            body.Children.Add(check);

            var row = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Padding = new Thickness(8, 5, 8, 5),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Content = body
            };
            AutomationProperties.SetName(row, $"{spec.Name}, {spec.Blurb}");
            AutomationProperties.SetHelpText(row, spec.Id == selected.Id ? "Selected" : null);
            row.Click += (_, _) => Select(spec.Id);
            rows[spec.Id] = row;
            return row;
        }

        var featured = new StackPanel { Spacing = 1 };
        featured.Children.Add(K.Caption("Featured"));
        foreach (var spec in Specs.Take(4))
        {
            featured.Children.Add(MenuRow(spec, true));
        }

        featured.Children.Add(K.Divider(6));
        var effortHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        effortHeader.Children.Add(K.Caption("Effort"));
        var effortHint = K.Caption("Reasoning depth");
        Grid.SetColumn(effortHint, 1);
        effortHeader.Children.Add(effortHint);
        featured.Children.Add(effortHeader);
        featured.Children.Add(effortRow);

        featured.Children.Add(K.Divider(6));
        featured.Children.Add(K.Caption("More models"));
        foreach (var spec in Specs.Skip(4))
        {
            featured.Children.Add(MenuRow(spec, false));
        }

        var menu = new Border
        {
            Background = K.Brush(ArcTokens.Surface),
            BorderBrush = K.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(8),
            BoxShadow = ArcResources.Shadows(ArcTokens.ShadowFloating),
            Child = featured
        };

        var effortReview = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        effortReview.Children.Add(K.Stack(1, K.Caption("Effort"), effortValue));
        var reasoning = K.Caption("Reasoning on");
        Grid.SetColumn(reasoning, 1);
        effortReview.Children.Add(reasoning);

        var review = new Border
        {
            Background = K.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14, 12, 14, 12),
            Child = K.Stack(10,
                description,
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    RowDefinitions = new RowDefinitions("Auto,Auto"),
                    ColumnSpacing = 18,
                    RowSpacing = 8,
                    Children =
                    {
                        K.Stack(1, K.Caption("Context"), contextValue),
                        K.Stack(1, K.Caption("Max output"), outputValue),
                        K.Stack(1, K.Caption("Input"), priceInValue),
                        K.Stack(1, K.Caption("Output"), priceOutValue)
                    }
                },
                K.Divider(0),
                effortReview)
        };

        // Place the review's value grid cells in two columns.
        if (review.Child is StackPanel reviewStack && reviewStack.Children[1] is Grid values)
        {
            for (var index = 0; index < values.Children.Count; index++)
            {
                Grid.SetRow(values.Children[index], index / 2);
                Grid.SetColumn(values.Children[index], index % 2);
            }
        }

        RebuildEffort();

        var left = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                K.Caption("Default model"),
                new Border { Child = pillHost, HorizontalAlignment = HorizontalAlignment.Left },
                review,
                K.Caption("The pill morphs to the chosen name and effort; the review below it is optional.")
            }
        };

        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("340,20,*") };
        columns.Children.Add(left);
        Grid.SetColumn(menu, 2);
        columns.Children.Add(menu);

        var root = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                K.Stack(2,
                    K.Text("Choose a model", "arc-heading"),
                    K.Caption("Four featured models, an effort row and every other model by provider.")),
                columns,
                status
            }
        };

        return K.Surface(root, new Thickness(20, 16, 20, 16));
    }
}
