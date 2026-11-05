using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Sample.Data;

/// <summary>Data screen: a sortable Arc data table with typed columns and a selection model.</summary>
public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Arc for Avalonia - Invoices";
        Width = 900;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = ArcResources.Brush(ArcTokens.Background);

        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["invoice"] = "INV-1042", ["client"] = "Northwind", ["amount"] = 4200.0, ["status"] = "Paid" },
            new() { ["invoice"] = "INV-1043", ["client"] = "Acme", ["amount"] = 1180.5, ["status"] = "Open" },
            new() { ["invoice"] = "INV-1044", ["client"] = "Globex", ["amount"] = 8600.0, ["status"] = "Overdue" },
            new() { ["invoice"] = "INV-1045", ["client"] = "Initech", ["amount"] = 340.0, ["status"] = "Draft" }
        };

        var columns = new List<ArcTableColumn>
        {
            new() { Key = "invoice", Label = "Invoice", Width = 150 },
            new() { Key = "client", Label = "Client" },
            new()
            {
                Key = "amount",
                Label = "Amount",
                Numeric = true,
                Width = 140,
                Format = value => value is double amount ? amount.ToString("C0", CultureInfo.InvariantCulture) : "-"
            },
            new() { Key = "status", Label = "Status", Width = 120 }
        };

        var table = new ArcSortableDataTable
        {
            Rows = rows,
            Columns = columns,
            Caption = "Invoices",
            RowKey = row => ((Dictionary<string, object?>)row)["invoice"]?.ToString() ?? string.Empty,
            Selectable = true,
            Height = 380
        };

        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = "Invoices", Classes = { "arc-heading" } });
        panel.Children.Add(new TextBlock
        {
            Text = "A sortable data table: sortable headers, right-aligned numeric columns and row selection.",
            Classes = { "arc-body-sm", "arc-secondary" },
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(table);

        Content = new Border
        {
            Padding = new Thickness(48),
            Child = new ArcCard { Padding = new Thickness(32), Content = panel  } };
    }
}