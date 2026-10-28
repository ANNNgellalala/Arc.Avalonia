using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.DataDisplay.Pages;

/// <summary>
/// A project budget hierarchy: three levels, tri-state checks, sorting that stays inside each level,
/// and a lazy row whose children arrive from an async loader.
/// </summary>
public sealed class TreeTablePage : ArcPage
{
    public override string Title => "Tree table";

    public override string Group => "Tables";

    public override string? Description =>
        "Nested rows with columns: tri-state selection, sorting that preserves the hierarchy, and children loaded on demand.";

    public override int Order => 310;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Tree table",
                "Use when hierarchical rows also need columns, like budgets by team, folder sizes, or nested tasks.",
                BuildDemo())
        }
    };

    private static Control BuildDemo()
    {
        var columns = new List<ArcTreeTableColumn>
        {
            new()
            {
                Key = "project",
                Header = "Project",
                Width = 0,
                MinWidth = 240
            },
            new()
            {
                Key = "owner",
                Header = "Owner",
                Width = 150,
                Sortable = false
            },
            new()
            {
                Key = "status",
                Header = "Status",
                Width = 120,
                Sortable = false
            },
            new()
            {
                Key = "budget",
                Header = "Budget",
                Width = 140,
                Align = ArcTreeTableAlign.Right,
                Format = Money
            }
        };

        var table = new ArcTreeTable
        {
            Columns = columns,
            Rows = BuildRows(),
            Label = "Q3 project budgets",
            Selectable = true,
            DefaultExpandedIds = new[] { "product", "atlas", "marketing", "brand", "operations" },
            DefaultSelectedIds = new[] { "atlas", "research", "design-system" },
            DefaultSort = new ArcTreeTableSort { Key = "budget", Descending = true },
            LoadChildren = LoadArchiveAsync,
            MaxBodyHeight = 0,
            ShowSummary = true
        };

        var caption = new TextBlock
        {
            Text = "Budget is sorted high to low inside every level, Product shows the mixed state for its three checked rows, and Cloud archive fetches its children the first time you expand it. Click a leaf to activate it.",
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 1020,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        caption.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);

        table.RowActivated += (_, row) =>
        {
            var name = row["project"]?.ToString() ?? row.Id;
            caption.Text =
                $"{name} activated. Parent rows expand on click; a leaf raises RowActivated, which updates this line.";
        };

        var keyboard = new TextBlock
        {
            Text = "Keyboard: Up and Down move through visible rows, Right and Left expand or collapse, Space checks, Home and End jump, Enter opens a row, and typing a letter finds the next row that starts with it.",
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 1020,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        keyboard.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);

        return new StackPanel
        {
            Width = 1020,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { table, caption, keyboard }
        };
    }

    private static List<ArcTreeTableRow> BuildRows() => new()
    {
        Row("product", "Product", "Maya Chen", "Live", 66750,
            Row("atlas", "Atlas redesign", "Maya Chen", "In progress", 48250,
                Row("research", "Research", "Ines Ma", "Done", 18000),
                Row("design-system", "Design system", "Sara Ruiz", "In progress", 30250)),
            Row("billing", "Billing migration", "Leo Park", "Review", 18500)),
        Row("marketing", "Marketing", "Leo Park", "Live", 63400,
            Row("brand", "Brand refresh", "Ines Ma", "In progress", 41000,
                Row("naming", "Naming", "Ada Lin", "Review", 16000),
                Row("illustration", "Illustration", "Noor Haddad", "In progress", 25000)),
            Row("launch", "Launch site", "Tom Hale", "Planned", 22400)),
        Row("operations", "Operations", "Sara Ruiz", "Live", 40300,
            Row("permissions", "Permissions", "Leo Park", "Review", 27500),
            Row("export", "Data export", "Tom Hale", "In progress", 12800)),
        new ArcTreeTableRow
        {
            Id = "cloud-archive",
            Label = "Cloud archive",
            HasChildren = true,
            ["project"] = "Cloud archive",
            ["owner"] = "—",
            ["status"] = "On demand",
            ["budget"] = null
        }
    };

    private static ArcTreeTableRow Row(
        string id,
        string project,
        string owner,
        string status,
        double budget,
        params ArcTreeTableRow[] children)
    {
        var row = new ArcTreeTableRow { Id = id, Label = project };
        row["project"] = project;
        row["owner"] = owner;
        row["status"] = status;
        row["budget"] = budget;
        row.Children.AddRange(children);
        return row;
    }

    private static async Task<IReadOnlyList<ArcTreeTableRow>> LoadArchiveAsync(ArcTreeTableRow row)
    {
        if (row.Id != "cloud-archive")
        {
            return Array.Empty<ArcTreeTableRow>();
        }

        await Task.Delay(450);
        return new List<ArcTreeTableRow>
        {
            Row("archive-2019", "2019 archive", "Leo Park", "Archived", 4200),
            Row("archive-2020", "2020 archive", "Ines Ma", "Archived", 6100),
            Row("archive-2021", "2021 archive", "Tom Hale", "Archived", 3800)
        };
    }

    private static string Money(object? value)
    {
        if (value is null)
        {
            return "—";
        }

        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture)
                .ToString("C0", CultureInfo.GetCultureInfo("en-US"));
        }
        catch
        {
            return "—";
        }
    }
}
