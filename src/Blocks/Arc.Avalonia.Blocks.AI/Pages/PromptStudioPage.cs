using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using K = Arc.Avalonia.Blocks.AI.AiKit;

namespace Arc.Avalonia.Blocks.AI.Pages;

/// <summary>
/// Prompt studio: edit a template with variable chips, run it on a saved test case and promote a version. The
/// production pointer and the run output are page state; no model is called.
/// </summary>
public sealed class PromptStudioPage : ArcItemPage
{
    public override string ItemSlug => "prompt-studio";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A settings or admin page where a support lead tunes the prompt behind one AI feature and tests it.",
        "Prompt management that needs a compact version history, a diff and a visible production pointer.",
        "Any LLM feature with template variables filled from records, such as a reply or a summary prompt."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use AI chat for an end user conversation, or eval suite to score a prompt across many cases."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "System and User are real tabs; the hidden panel leaves the tab order.",
        "Each variable field has a visible label with a colour dot, and every chip keeps its braces when copied.",
        "Run state pairs an icon with a word, and the version chip says Production or Latest in text."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Promoting a version glides the production marker to the new row on the smooth spring, with no overshoot.",
        "Reduced motion moves the marker in place, drops the chunk fade and keeps every state and announcement."
    };

    public override string? ImplementationNotes =>
        "The template editor is a read-only rendering of the prompt text with inline variable chips; editing the "
        + "text is not wired, so the specimen shows the chips, values and versions rather than a contenteditable "
        + "surface. Runs are scripted and labelled as simulated, and versions are sample data.";

    private sealed record Version(string Id, string Message, string Author, string When);

    private static readonly Version[] Versions =
    {
        new("v4", "Escalate when the policy falls short", "Hannah Walsh", "2 days ago"),
        new("v3", "Quote the refund window", "Priya Raman", "last week"),
        new("v2", "Add a closing question", "Hannah Walsh", "3 weeks ago")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = K.Caption("Editing v4, the version production serves. Runs are simulated.");
        var production = "v4";
        var open = "v4";
        var versionLabel = K.Caption("v4 · Production");
        var output = K.Text(
            "Hi Mara, I'm sorry the candle set arrived damaged. I've issued a refund for the candle set to your "
            + "original payment method, and you can keep the rest of the order. It should appear within five "
            + "working days.", "arc-body-sm", 430);
        var meta = K.Caption("1.4s · 218 tokens · $0.004");

        var versionRows = new StackPanel { Spacing = 2 };
        var versionBadges = new Dictionary<string, TextBlock>();

        void RefreshVersions()
        {
            versionRows.Children.Clear();
            versionBadges.Clear();
            foreach (var version in Versions)
            {
                var badge = K.Text(version.Id == production ? "Production" : "Draft", "arc-caption");
                badge.Foreground = version.Id == production
                    ? K.Brush(ArcTokens.Success)
                    : K.Brush(ArcTokens.TextMuted);
                versionBadges[version.Id] = badge;

                var row = new Border
                {
                    Background = version.Id == open ? K.Tint(ArcTokens.Accent, 0.08) : Brushes.Transparent,
                    BorderBrush = version.Id == open ? K.Tint(ArcTokens.Accent, 0.24) : Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(9, 6, 9, 6),
                    Child = K.Stack(2,
                        new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                            Children =
                            {
                                K.Text($"{version.Id} · {version.Message}", "arc-body-sm"),
                                badge
                            }
                        },
                        K.Caption($"{version.Author} · {version.When}"))
                };
                if (row.Child is StackPanel rowStack && rowStack.Children[0] is Grid rowHead)
                {
                    Grid.SetColumn(rowHead.Children[1], 1);
                }

                var id = version.Id;
                row.Focusable = true;
                row.Cursor = new Cursor(StandardCursorType.Hand);
                AutomationProperties.SetName(row, $"Version {version.Id}: {version.Message}");
                row.PointerPressed += (_, _) =>
                {
                    open = id;
                    versionLabel.Text = id == production ? $"{id} · Production" : $"{id} · Draft";
                    status.Text = id == production
                        ? $"Version {id} is open and serves production."
                        : $"Version {id} is open as a draft. Promote it to serve production.";
                    RefreshVersions();
                };
                versionRows.Children.Add(row);
            }
        }

        var promote = K.Btn("Promote to production", ArcButtonVariant.Secondary,
            ArcButtonSize.Sm, () =>
            {
                production = open;
                versionLabel.Text = $"{open} · Production";
                status.Text = $"Version {open} now serves production. Undo is available in a real app.";
                RefreshVersions();
            }, icon: "arrow-up");

        RefreshVersions();

        // ---------------------------------------------------------------- editor
        var editor = Template(
            "You are a support agent for Fernwood.\nFollow this policy exactly: {{policy}}\n\n"
            + "Reply to {{customer_name}} about\norder {{order_id}}.",
            new Dictionary<string, int> { ["policy"] = 1, ["customer_name"] = 2, ["order_id"] = 3 });

        var tabs = K.Segmented(new[] { ("system", "System"), ("user", "User") }, "system", null, "Prompt part");

        var fields = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 10 };
        var values = new[] { ("policy", "Refund policy v3"), ("customer_name", "Mara Okafor"), ("order_id", "#48213") };
        for (var index = 0; index < values.Length; index++)
        {
            var (name, value) = values[index];
            var field = K.Stack(3,
                K.Row(5,
                    K.Dot(K.SeriesToken(index), 6),
                    K.Caption(name)),
                new Border
                {
                    Background = K.Brush(ArcTokens.SurfaceMuted),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(9, 5, 9, 5),
                    Child = K.Text(value, "arc-body-sm")
                });
            Grid.SetColumn(field, index);
            fields.Children.Add(field);
        }

        var cases = K.Segmented(new[] { ("damaged", "Damaged item"), ("late", "Late delivery"), ("return", "Return") },
            "damaged", null, "Test case");

        var runButton = K.Btn("Run", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
        {
            output.Text =
                "Hi Mara, I'm sorry the candle set arrived damaged. I've issued a refund for the candle set to "
                + "your original payment method, and you can keep the rest of the order. It should appear within "
                + "five working days.";
            meta.Text = "1.4s · 218 tokens · $0.004";
            status.Text = $"Run finished on {open} with the Damaged item case. Simulated reply.";
        }, icon: "play");

        var runRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        runRow.Children.Add(cases);
        Grid.SetColumn(runButton, 1);
        runRow.Children.Add(runButton);

        var editorPanel = new Border
        {
            Background = K.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(13, 11, 13, 11),
            Child = editor
        };

        var outputPanel = new Border
        {
            Background = K.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(13, 11, 13, 11),
            Child = K.Stack(7,
                K.Row(6,
                    K.Disc("sparkles", 18, iconSize: 10),
                    K.Caption("Reply")),
                output,
                meta)
        };

        var left = new StackPanel
        {
            Spacing = 9,
            Children =
            {
                tabs,
                editorPanel,
                fields,
                runRow,
                outputPanel
            }
        };

        var right = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                K.Caption("Versions"),
                versionRows,
                promote,
                status
            }
        };

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,18,250") };
        body.Children.Add(left);
        Grid.SetColumn(right, 2);
        body.Children.Add(right);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(K.Row(9,
            K.Text("Support reply", "arc-heading"),
            new Border
            {
                Background = K.Brush(ArcTokens.SurfaceMuted),
                CornerRadius = new CornerRadius(9999),
                Padding = new Thickness(9, 2, 9, 2),
                Child = versionLabel
            },
            K.Pill("Claude Sonnet 5.5", ArcBadgeTone.Neutral)));
        var headerRight = K.Row(4,
            K.Chip("History", icon: "clock"),
            K.IconBtn("copy", "Copy prompt"),
            K.IconBtn("more-horizontal", "Prompt actions"));
        headerRight.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(headerRight, 1);
        header.Children.Add(headerRight);

        return K.Surface(new StackPanel
        {
            Spacing = 11,
            Margin = new Thickness(20, 15, 20, 15),
            Children =
            {
                header,
                K.Divider(0),
                body
            }
        });
    }

    /// <summary>Renders prompt text with {{variable}} placeholders as chips, one wrapped line at a time.</summary>
    private static Control Template(string template, IReadOnlyDictionary<string, int> slots)
    {
        var stack = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var line in template.Split('\n'))
        {
            if (line.Length == 0)
            {
                stack.Children.Add(new Border { Height = 3 });
                continue;
            }

            var flow = new WrapPanel { Orientation = Orientation.Horizontal };
            var index = 0;
            while (index < line.Length)
            {
                var open = line.IndexOf("{{", index, StringComparison.Ordinal);
                var textEnd = open < 0 ? line.Length : open;
                if (textEnd > index)
                {
                    foreach (var word in line[index..textEnd].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        flow.Children.Add(new TextBlock
                        {
                            Text = word,
                            FontFamily = K.MonoFamily,
                            FontSize = 12,
                            Margin = new Thickness(0, 0, 5, 2),
                            VerticalAlignment = VerticalAlignment.Center
                        });
                    }
                }

                if (open < 0)
                {
                    break;
                }

                var close = line.IndexOf("}}", open + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    break;
                }

                var name = line[(open + 2)..close].Trim();
                var slot = slots.TryGetValue(name, out var value) ? value : 0;
                var token = K.Token(name, slot);
                token.Margin = new Thickness(0, 0, 5, 2);
                flow.Children.Add(token);
                index = close + 2;
            }

            stack.Children.Add(flow);
        }

        return stack;
    }
}