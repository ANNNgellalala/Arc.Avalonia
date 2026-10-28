using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Ck = Arc.Avalonia.BlocksCommerce.CommerceKit;

namespace Arc.Avalonia.BlocksCommerce.Pages;

/// <summary>
/// Invoice studio: an editable invoice with client details, line items, live totals and a save draft flow.
/// Lines, rates and the client are sample data; saving validates that every line has a description and runs a
/// short local request so the button shows pending and saved states.
/// </summary>
public sealed class InvoiceStudioPage : ArcItemPage
{
    public override string ItemSlug => "invoice-studio";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Creating or editing an invoice or quote with line items.",
        "Forms that need a live subtotal and total as quantities and rates change."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Read-only invoice history; use sortable-data-table or billing-overview."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Every row input and remove button carries a per-line accessible name, and the total is an output with a live region.",
        "Save progress and validation messages are announced through a polite status line."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Totals update in place on each edit; added and removed lines change the layout without travel.",
        "Reduced motion keeps the same states with no height animation."
    };

    public override string? ImplementationNotes =>
        "Client details and starting lines are sample data and totals are USD only. The reference rolls each total with an animated counter and animates line height; the port updates the figures in place and keeps the live output. Saving is a 700 ms local request, and totals should be recomputed on a server in production.";

    private sealed class Line
    {
        public string Description = string.Empty;
        public double Quantity = 1;
        public double Rate;
        public TextBlock Amount = null!;
    }

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var lines = new List<Line>
        {
            new() { Description = "Design system audit", Quantity = 1, Rate = 2400 },
            new() { Description = "Component build, 12 screens", Quantity = 12, Rate = 180 },
            new() { Description = "Handover workshop", Quantity = 2, Rate = 450 }
        };
        var taxRate = 0.08;
        var status = Ck.Status();
        var linesHost = new StackPanel { Spacing = 6 };
        var subtotalText = Ck.Text(Ck.Money(0, 2), "arc-body-sm");
        var taxText = Ck.Text(Ck.Money(0, 2), "arc-body-sm");
        var totalText = Ck.Text(Ck.Money(0, 2), "arc-heading");
        var saveLabel = new TextBlock
        {
            Text = "Save draft",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Ck.Brush(ArcTokens.AccentForeground)
        };

        double Subtotal() => lines.Sum(line => line.Quantity * line.Rate);

        void Recalculate()
        {
            foreach (var line in lines)
            {
                line.Amount.Text = Ck.Money(line.Quantity * line.Rate, 2);
            }

            var subtotal = Subtotal();
            subtotalText.Text = Ck.Money(subtotal, 2);
            taxText.Text = Ck.Money(subtotal * taxRate, 2);
            totalText.Text = Ck.Money(subtotal * (1 + taxRate), 2);
        }

        void BuildLines()
        {
            linesHost.Children.Clear();
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var rowNumber = index + 1;

                var description = Ck.Input("Describe the work", line.Description, value => line.Description = value);
                description.HorizontalAlignment = HorizontalAlignment.Stretch;

                var quantity = new ArcInput
                {
                    Value = line.Quantity.ToString("0"),
                    Width = 84,
                    ShowClearButton = false
                };
                quantity.ValueChanged += (_, _) =>
                {
                    if (double.TryParse(quantity.Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
                    {
                        line.Quantity = parsed;
                        Recalculate();
                    }
                };

                var rate = new ArcInput
                {
                    Value = line.Rate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                    Width = 88,
                    ShowClearButton = false
                };
                rate.ValueChanged += (_, _) =>
                {
                    if (double.TryParse(rate.Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
                    {
                        line.Rate = parsed;
                        Recalculate();
                    }
                };
                line.Amount = Ck.Text(Ck.Money(line.Quantity * line.Rate, 2), "arc-body-sm arc-medium");
                line.Amount.HorizontalAlignment = HorizontalAlignment.Right;
                line.Amount.VerticalAlignment = VerticalAlignment.Center;

                var remove = Ck.IconBtn("trash-2", $"Remove line {rowNumber}", () =>
                {
                    lines.Remove(line);
                    BuildLines();
                    Recalculate();
                    Ck.Say(status, $"Line {rowNumber} removed. {lines.Count} lines left.");
                }, 28);

                var grid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,92,100,92,30"),
                    ColumnSpacing = 8,
                    Children =
                    {
                        description,
                        Place(quantity, 1),
                        Place(rate, 2),
                        Place(line.Amount, 3),
                        Place(remove, 4)
                    }
                };
                linesHost.Children.Add(grid);
            }
        }

        BuildLines();
        Recalculate();

        var saveButton = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Md,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                Children =
                {
                    new ArcIcon { Icon = "save", Size = 15, Foreground = Ck.Brush(ArcTokens.AccentForeground) },
                    saveLabel
                }
            }
        };
        saveButton.Click += (_, _) =>
        {
            if (lines.Any(line => string.IsNullOrWhiteSpace(line.Description)))
            {
                Ck.Say(status, "Every line needs a description before this draft can be saved.");
                return;
            }

            saveButton.IsEnabled = false;
            saveButton.Loading = true;
            saveLabel.Text = "Saving";

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                saveButton.Loading = false;
                saveButton.IsEnabled = true;
                saveLabel.Text = "Saved";
                Ck.Say(status, $"Draft saved with {lines.Count} lines, {Ck.Money(Subtotal() * (1 + taxRate), 2)} total.");
            };
            timer.Start();
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Ck.Row(8, Ck.Heading("Draft 0241", 380, "arc-subtitle"), Ck.Pill("Draft", ArcBadgeTone.Warning)),
                Ck.Caption("Acme Studio to Northwind · due 30 November 2026")
            }
        });
        var headerRight = Ck.Row(8,
            Ck.Btn("Preview", ArcButtonVariant.Ghost, ArcButtonSize.Md, () => Ck.Say(status, "The reference opens a print preview.")),
            saveButton);
        headerRight.HorizontalAlignment = HorizontalAlignment.Right;
        headerRight.VerticalAlignment = VerticalAlignment.Top;
        header.Children.Add(Place(headerRight, 1));

        var parties = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,16,*"),
            Children =
            {
                Party("From", "Acme Studio", "14 Bridge Street, Lisbon · VAT PT123456789"),
                Place(Party("To", "Northwind", "billing@northwind.co · 22 Harbour Road, Porto"), 2)
            }
        };

        var columnLabels = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,92,100,92,30"),
            ColumnSpacing = 8,
            Children =
            {
                Ck.Caption("Description"),
                Place(Ck.Caption("Qty"), 1),
                Place(Ck.Caption("Rate"), 2),
                Place(Ck.Caption("Amount"), 3)
            }
        };

        var subtotalRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        subtotalRow.Children.Add(Ck.Body("Subtotal", 200));
        subtotalRow.Children.Add(Place(subtotalText, 1));

        var taxRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        taxRow.Children.Add(Ck.Body("Tax, 8%", 200));
        taxRow.Children.Add(Place(taxText, 1));

        var totals = new StackPanel
        {
            Spacing = 6,
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                subtotalRow,
                taxRow,
                Ck.Divider(),
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children = { Ck.Label("Total, USD"), Place(totalText, 1) }
                }
            }
        };

        var addLine = Ck.Link("Add a line", () =>
        {
            lines.Add(new Line { Description = string.Empty, Quantity = 1, Rate = 0 });
            BuildLines();
            Recalculate();
            Ck.Say(status, "Line added. Give it a description and a rate.");
        }, "plus");

        var editor = Ck.Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                parties,
                Ck.Divider(),
                columnLabels,
                linesHost,
                addLine,
                Ck.Divider(),
                totals
            }
        }, 16);

        return Ck.Specimen(new StackPanel
        {
            Spacing = 12,
            Children = { header, editor, status }
        }, 16);

        Control Party(string label, string name, string detail)
        {
            return new Border
            {
                Background = Ck.Brush(ArcTokens.SurfaceMuted),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Child = new StackPanel
                {
                    Spacing = 2,
                    Children = { Ck.Caption(label), Ck.Text(name, "arc-body-sm arc-medium"), Ck.Caption(detail) }
                }
            };
        }

        static T Place<T>(T control, int column) where T : Control
        {
            Grid.SetColumn(control, column);
            return control;
        }
    }
}
