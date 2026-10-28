using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;
using K = Arc.Avalonia.Blocks.AI.AiKit;

namespace Arc.Avalonia.Blocks.AI.Pages;

/// <summary>
/// Document extraction: an invoice on the left and the typed fields on the right. Selecting a field lights the
/// span it came from, and Accept all confirms every value. The extraction is sample data.
/// </summary>
public sealed class DocumentExtractPage : ArcItemPage
{
    public override string ItemSlug => "document-extract";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Accounts payable, logistics or back office tools that turn invoices and forms into records a person accepts.",
        "Any extraction flow where a reviewer must see where each value came from and fix the unsure ones.",
        "Settings or demos for a structured output feature, with the typed result one glance away."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use AI answer for a written answer with citations, or knowledge base to search across many documents."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Every value is a button labelled with its field, value and confidence, and selecting it lights the source span.",
        "Confidence is a percentage with an icon for the states that matter; a flagged value carries the model's reason.",
        "The run's state is announced in a polite region: extracting, done with how many need a check, failed or stopped."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Extract sweeps one soft bar of light down the page while cited spans take a faint tint.",
        "Reduced motion removes the sweep and the tint flash, keeps the selection and still announces every state."
    };

    public override string? ImplementationNotes =>
        "The scan sweep, the curved connector and the typed parse are simplified: selecting a field lights both "
        + "ends with a tint. The document is a sample invoice rendered on the page.";

    private sealed record Field(string Key, string Label, string Value, double Confidence, string Group, string Span);

    private static readonly Field[] Fields =
    {
        new("number", "Invoice no.", "INV-2026-0418", 0.99, "Invoice", "number"),
        new("date", "Issue date", "14 October 2026", 0.97, "Invoice", "date"),
        new("vendor", "Vendor", "Larkspur Studio", 0.95, "Vendor", "vendor"),
        new("terms", "Payment terms", "Net 30 days", 0.62, "Vendor", "terms"),
        new("total", "Total", "$1,400.00", 0.94, "Amounts", "total")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = K.Caption("Extraction finished. 1 value needs a check before it can be accepted.");
        var selected = "total";
        var accepted = false;
        var spans = new Dictionary<string, Border>();
        var rows = new Dictionary<string, Border>();

        void Refresh()
        {
            foreach (var pair in spans)
            {
                var active = pair.Key == selected;
                pair.Value.Background = active
                    ? K.Tint(ArcTokens.Accent, 0.24)
                    : K.Tint(K.SeriesToken(Fields.First(field => field.Span == pair.Key).Group.Length % 4), 0.12);
            }

            foreach (var pair in rows)
            {
                pair.Value.BorderBrush = pair.Key == selected ? K.Brush(ArcTokens.Accent) : K.Brush(ArcTokens.BorderSubtle);
                pair.Value.BorderThickness = new Thickness(pair.Key == selected ? 2 : 1);
            }
        }

        Control Span(string key, string text, int slot)
        {
            var span = new Border
            {
                Background = K.Tint(K.SeriesToken(slot), 0.12),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(3, 0, 3, 0),
                Child = new TextBlock { Text = text, FontSize = 11, Classes = { "arc-secondary" } }
            };
            spans[key] = span;
            return span;
        }

        Control FieldRow(Field field)
        {
            var confidenceTone = field.Confidence < 0.8 ? ArcProgressTone.Warning : ArcProgressTone.Success;
            var main = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            main.Children.Add(K.Stack(1, K.Caption(field.Label), K.Text(field.Value, "arc-body-sm")));
            var meter = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            var percent = K.Caption($"{field.Confidence * 100:0}%");
            percent.HorizontalAlignment = HorizontalAlignment.Right;
            meter.Children.Add(percent);
            var bar = K.Bar(field.Confidence * 100, 100, ArcProgressSize.Sm, confidenceTone);
            bar.Width = 70;
            meter.Children.Add(bar);
            Grid.SetColumn(meter, 1);
            main.Children.Add(meter);

            var body = new StackPanel { Spacing = 3, Children = { main } };
            if (field.Confidence < 0.8)
            {
                body.Children.Add(K.Row(6,
                    K.Caption("Inferred from the payment terms"),
                    K.Chip("Looks right", icon: "check")));
                if (body.Children[1] is StackPanel reasonRow && reasonRow.Children[1] is ArcButton looksRight)
                {
                    looksRight.Click += (_, _) => status.Text = $"{field.Label} marked as checked by a person.";
                }
            }

            var row = new Border
            {
                Background = K.Brush(ArcTokens.Surface),
                BorderBrush = K.Brush(ArcTokens.BorderSubtle),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(9, 5, 9, 5),
                Child = body
            };
            rows[field.Key] = row;
            row.Focusable = true;
            row.Cursor = new Cursor(StandardCursorType.Hand);
            AutomationProperties.SetName(row,
                $"{field.Label}: {field.Value}, {field.Confidence * 100:0} percent"
                + (field.Confidence < 0.8 ? ", needs a check" : string.Empty));
            row.PointerPressed += (_, _) =>
            {
                selected = field.Key;
                Refresh();
                status.Text = $"{field.Label} selected. Its source is lit on the document.";
            };
            row.KeyDown += (_, args) =>
            {
                if (args.Key is Key.Enter or Key.Space)
                {
                    selected = field.Key;
                    Refresh();
                    args.Handled = true;
                }
            };
            return row;
        }

        var formRows = new StackPanel { Spacing = 4 };
        foreach (var group in Fields.GroupBy(field => field.Group))
        {
            formRows.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Margin = new Thickness(0, 5, 0, 0),
                Children =
                {
                    K.Row(5, K.Dot(K.SeriesToken(group.Key.Length % 4), 6), K.Label(group.Key)),
                    K.Caption(group.Count() == 1 ? "1 field" : $"{group.Count()} fields")
                }
            });
            if (formRows.Children[^1] is Grid groupHead)
            {
                Grid.SetColumn(groupHead.Children[1], 1);
            }

            foreach (var field in group)
            {
                formRows.Children.Add(FieldRow(field));
            }
        }

        Control Document()
        {
            var sheet = new StackPanel { Spacing = 8 };
            sheet.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                Children =
                {
                    new Border
                    {
                        Width = 26, Height = 26, CornerRadius = new CornerRadius(6),
                        Background = K.Tint(ArcTokens.Series3, 0.2),
                        Child = new TextBlock
                        {
                            Text = "L", FontSize = 13, FontWeight = FontWeight.Medium,
                            Foreground = K.Brush(ArcTokens.Series3),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    },
                    K.Stack(0,
                        K.Text("LARKSPUR studio", "arc-body-sm"),
                        K.Caption("18 Quay Street, Bristol")),
                    K.Text("INVOICE", "arc-label")
                }
            });
            if (sheet.Children[^1] is Grid masthead)
            {
                masthead.Children[1].Margin = new Thickness(8, 0, 0, 0);
                Grid.SetColumn(masthead.Children[1], 1);
                Grid.SetColumn(masthead.Children[2], 2);
            }

            sheet.Children.Add(K.Divider(2));
            sheet.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                Children =
                {
                    K.Stack(2, K.Caption("Invoice no."), Span("number", "INV-2026-0418", 0)),
                    K.Stack(2, K.Caption("Issue date"), Span("date", "14 October 2026", 1))
                }
            });
            if (sheet.Children[^1] is Grid meta)
            {
                Grid.SetColumn(meta.Children[1], 1);
            }

            sheet.Children.Add(K.Stack(2,
                K.Caption("Billed to"),
                K.Text("Northwind Supply Co.\n42 Harbour Road, Cardiff", "arc-body-sm")));

            var lines = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
                RowSpacing = 4
            };
            var items = new[]
            {
                ("Candle set, amber", "$840.00"),
                ("Shipping and handling", "$120.00"),
                ("Studio packaging", "$440.00")
            };
            lines.Children.Add(K.Caption("Description"));
            var amountHead = K.Caption("Amount");
            Grid.SetColumn(amountHead, 1);
            lines.Children.Add(amountHead);
            for (var index = 0; index < items.Length; index++)
            {
                var description = K.Text(items[index].Item1, "arc-body-sm");
                Grid.SetRow(description, index + 1);
                lines.Children.Add(description);
                var amount = K.Text(items[index].Item2, "arc-body-sm");
                Grid.SetRow(amount, index + 1);
                Grid.SetColumn(amount, 1);
                lines.Children.Add(amount);
            }

            sheet.Children.Add(lines);
            sheet.Children.Add(K.Divider(4));
            sheet.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    K.Row(5, K.Label("Total"), Span("total", "$1,400.00", 0)),
                    K.Stack(2, K.Caption("Payment terms"), Span("terms", "Net 30 days", 2))
                }
            });
            if (sheet.Children[^1] is Grid totalRow)
            {
                Grid.SetColumn(totalRow.Children[1], 1);
            }

            sheet.Children.Add(K.Caption("Please pay by 13 November 2026. Thank you."));

            return new Border
            {
                Background = K.Brush(ArcTokens.Background),
                BorderBrush = K.Brush(ArcTokens.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20, 18, 20, 18),
                Child = sheet
            };
        }

        var accept = K.Btn("Accept all", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
        {
            accepted = true;
            status.Text = "All 6 values accepted and handed to the page. Nothing was saved anywhere else.";
            foreach (var row in rows.Values)
            {
                row.BorderBrush = K.Brush(ArcTokens.Success);
            }
        }, icon: "check");

        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        toolbar.Children.Add(K.Stack(2,
            K.Text("larkspur-invoice-0418.pdf", "arc-label"),
            K.Caption("Invoice · 1 of 2 pages · 100%")));
        toolbar.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                K.IconBtn("zoom-out", "Zoom out"),
                K.IconBtn("zoom-in", "Zoom in"),
                K.Chip("Fit", icon: "maximize")
            }
        });
        if (toolbar.Children[1] is Control toolbarRight)
        {
            Grid.SetColumn(toolbarRight, 1);
        }

        var left = K.Stack(7, toolbar, Document());
        var right = new StackPanel
        {
            Spacing = 7,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        K.Stack(2,
                            K.Text("Extracted fields", "arc-label"),
                            K.Caption("1 of 5 need a check")),
                        K.Pill(accepted ? "Accepted" : "Simulated", accepted ? ArcBadgeTone.Success : ArcBadgeTone.Neutral)
                    }
                },
                formRows,
                K.Divider(2),
                K.Row(6, accept, K.IconBtn("refresh-cw", "Extract again")),
                K.Caption("Claude Haiku 5.5 · 1,840 tokens · $0.002"),
                status
            }
        };

        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,360") };
        columns.Children.Add(left);
        Grid.SetColumn(right, 2);
        columns.Children.Add(right);

        if (right.Children[0] is Grid formHead)
        {
            Grid.SetColumn(formHead.Children[1], 1);
        }

        Refresh();
        return K.Surface(columns, new Thickness(18, 14, 18, 14));
    }
}
