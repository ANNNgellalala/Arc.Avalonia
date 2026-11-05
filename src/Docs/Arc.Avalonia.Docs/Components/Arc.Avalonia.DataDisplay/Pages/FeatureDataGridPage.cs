using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Arc.Avalonia.Controls;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feature.DataGrid;

namespace Arc.Avalonia.DataDisplay.Pages;

/// <summary>
/// Scenario page: the opt-in <c>Arc.Avalonia.Feature.DataGrid</c> package. The official Avalonia DataGrid
/// supplies row/column virtualisation, editing and clipboard; <see cref="ArcDataGridHost"/> adds the Arc
/// shell, a page bar and page slicing, and the Feature theme skins the official parts with Arc tokens.
/// <para>
/// Division of labour: <see cref="ArcDataGrid"/> (design-system, small data, not virtualised) stays the
/// default for design-critical tables; this Feature host is the large-data option and is the only project
/// allowed to reference <c>Avalonia.Controls.DataGrid</c> (R7). Not a catalog item: a plain
/// <see cref="ArcPage"/> so the 294-entry documentation audit is untouched.
/// </para>
/// </summary>
public sealed class FeatureDataGridPage : ArcPage
{
    private const int TotalRows = 10_000;

    public override string Title => "Feature DataGrid · 10k paged";

    public override string Group => "Data · scale";

    public override string? Description =>
        "Large-data option: Avalonia's official DataGrid (row/column virtualisation) wrapped by " +
        "ArcDataGridHost for the Arc shell, page bar and page slicing, with an Arc skin over the official " +
        "templates. Small design-critical tables keep using ArcDataGrid.";

    public override int Order => 9203;

    public override Control Build(ArcDemoContext context)
    {
        var rows = CreateRows();
        var host = new ArcDataGridHost
        {
            Width = 1_040,
            Height = 452,
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemsSource = rows,
            PageSize = 100,
            TotalItems = -1
        };
        ArcUi.Name(
            host,
            "Feature DataGrid host with 10,000 paged rows",
            "The official DataGrid virtualises the 100 rows of the current page and the columns; the host slices the 10,000 rows.");

        var status = DemoUi.Caption(
            $"Page 1 / {host.PageCount} · {TotalRows.ToString("N0", CultureInfo.InvariantCulture)} rows · " +
            "host.FirstPage/PreviousPage/NextPage/LastPage/GoToPage(…) drive the page bar and raise PageRequested.");
        host.PageRequested += (_, args) =>
            status.Text = $"Page {args.PageIndex + 1} / {host.PageCount} · requested page {args.PageIndex}";

        var small = SmallDesignTable();

        var root = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Left };

        // The official DataGrid theme is embedded in Avalonia.Controls.DataGrid and is not auto-loaded; the
        // demo includes it at page scope, then the Arc skin on top. Consumers do the same two lines in App.axaml.
        root.Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.DataGrid/"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml")
        });
        root.Styles.Add(new StyleInclude(new Uri("avares://Arc.Avalonia.Feature.DataGrid/"))
        {
            Source = new Uri("avares://Arc.Avalonia.Feature.DataGrid/Themes/ArcFeatureDataGridTheme.axaml")
        });

        root.Children.Add(DemoUi.Section(
            "Large data: Feature DataGrid host",
            null,
            host));
        root.Children.Add(status);
        root.Children.Add(DemoUi.Section(
            "Small data: the design-system ArcDataGrid is still the default",
            "ArcDataGrid is fully Arc themed and keeps inline editing, range selection, column menus and the " +
            "footer summary; it renders rows eagerly, which is the right trade for design-critical tables " +
            "under a few hundred rows. Use the Feature host above when the row count is unbounded.",
            small));
        return root;
    }

    private static Control SmallDesignTable()
    {
        var rows = new List<ArcGridRow>();
        for (var index = 0; index < 6; index++)
        {
            var row = new ArcGridRow { Id = $"small-{index}" };
            row["name"] = $"Workspace {index + 1}";
            row["seats"] = (index + 1) * 4;
            row["plan"] = index % 2 == 0 ? "Team" : "Business";
            rows.Add(row);
        }

        return new ArcDataGrid
        {
            Width = 720,
            HorizontalAlignment = HorizontalAlignment.Left,
            Label = "Workspaces",
            MaxBodyHeight = 210,
            Columns = new List<ArcGridColumn>
            {
                new() { Key = "name", Header = "Workspace" },
                new() { Key = "seats", Header = "Seats", Align = ArcGridAlign.Right, Width = 100 },
                new() { Key = "plan", Header = "Plan", Width = 140 }
            },
            Rows = rows
        };
    }

    private static List<FeatureRow> CreateRows()
    {
        var rows = new List<FeatureRow>(TotalRows);
        for (var index = 0; index < TotalRows; index++)
        {
            var number = index + 1;
            rows.Add(new FeatureRow(
                number,
                $"Record {number.ToString("N0", CultureInfo.InvariantCulture)}",
                number % 3 == 0 ? "Platform" : number % 3 == 1 ? "Design" : "Growth",
                Math.Round(60 + (number % 400) / 10d, 1),
                number % 11 == 0 ? "Needs review" : "Active"));
        }

        return rows;
    }
}

/// <summary>One row of the Feature DataGrid scenario; public properties drive DataGrid auto columns.</summary>
public sealed class FeatureRow
{
    public FeatureRow(int id, string name, string team, double score, string status)
    {
        Id = id;
        Name = name;
        Team = team;
        Score = score;
        Status = status;
    }

    public int Id { get; }

    public string Name { get; }

    public string Team { get; }

    public double Score { get; }

    public string Status { get; }
}
