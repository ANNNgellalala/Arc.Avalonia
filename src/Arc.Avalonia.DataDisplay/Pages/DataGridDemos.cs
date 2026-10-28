using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Feedback.Pages;
using Arc.Avalonia.Internal;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.DataDisplay.Pages;

/// <summary>Layout helpers for the data grid page.</summary>
internal static class DataGridDemos
{
public static Control DataGrid()
    {
        var columns = GridColumns();
        var rows = GridRows();
        var grid = new ArcDataGrid
        {
            Columns = columns,
            Rows = rows,
            Label = "Q3 budget",
            DefaultSort = new ArcGridSort { Key = "budget", Descending = true },
            SelectedIds = new[] { "r1", "r7", "r8" },
            Zebra = true,
            MaxBodyHeight = 400,
            ShowTotals = true
        };

        var caption = PageLayout.Caption(
            "Project is widened to 260 px, Budget is sorted descending, and rows 3 to 5 are checked. Double-click a cell, or press Enter on it, to edit.");
        grid.CellEdited += (_, edit) =>
        {
            caption.Text =
                $"{edit.Column.Header} for {edit.Row["project"]}: {Money(edit.OldValue)} → {Money(edit.NewValue)}. " +
                "The footer total follows the edit, and the sort holds its order until you change it.";
        };

        return PageLayout.Narrow(1020, PageLayout.Stack(12, grid, caption));
    }

    public static Control EmptyGrid()
    {
        var grid = new ArcDataGrid
        {
            Columns = GridColumns(),
            Rows = new List<ArcGridRow>(),
            Label = "Empty budget",
            EmptyMessage = "No rows match",
            EmptyDescription = "Try a shorter search, or clear the status filter to see every project again.",
            Selectable = true,
            Zebra = true,
            MaxBodyHeight = 240
        };

        return PageLayout.Narrow(1020, grid);
    }

    private static List<ArcGridColumn> GridColumns() => new()
    {
        new ArcGridColumn
        {
            Key = "project",
            Header = "Project",
            Width = 260,
            MinWidth = 140
        },
        new ArcGridColumn { Key = "owner", Header = "Owner", Width = 150 },
        new ArcGridColumn { Key = "status", Header = "Status", Width = 120 },
        new ArcGridColumn
        {
            Key = "budget",
            Header = "Budget",
            Width = 140,
            Align = ArcGridAlign.Right,
            Format = value => value is null ? "—" : Money(value)
        },
        new ArcGridColumn
        {
            Key = "progress",
            Header = "Progress",
            Width = 110,
            Align = ArcGridAlign.Right,
            ShowTotal = false,
            Format = value => value is IConvertible
                ? $"{Convert.ToDouble(value, CultureInfo.InvariantCulture):0}%"
                : "—"
        },
        new ArcGridColumn { Key = "updated", Header = "Updated", Width = 0, MinWidth = 120 }
    };

    private static List<ArcGridRow> GridRows() => new()
    {
        Row("r1", "Atlas redesign", "Maya Chen", "In progress", 48250, 64, "Today"),
        Row("r2", "Billing migration", "Leo Park", "Review", 18500, 82, "Today"),
        Row("r3", "Checkout copy", "Sara Ruiz", "Planned", 86000, 35, "Yesterday"),
        Row("r4", "Data export", "Tom Hale", "Blocked", 12800, 48, "Yesterday"),
        Row("r5", "Design tokens", "Ines Ma", "In progress", 54000, 76, "2 days ago"),
        Row("r6", "Mobile nav", "Noor Haddad", "Done", 9700, 100, "3 days ago"),
        Row("r7", "Search index", "Ada Lin", "Planned", 42000, 20, "4 days ago"),
        Row("r8", "Onboarding", "Maya Chen", "In progress", 31000, 58, "5 days ago"),
        Row("r9", "Permissions", "Leo Park", "Review", 27500, 90, "1 week ago"),
        Row("r10", "Release notes", "Sara Ruiz", "Done", 6500, 100, "1 week ago")
    };

    private static ArcGridRow Row(
        string id,
        string project,
        string owner,
        string status,
        double budget,
        double progress,
        string updated) => new()
    {
        Id = id,
        Values =
        {
            ["project"] = project,
            ["owner"] = owner,
            ["status"] = status,
            ["budget"] = budget,
            ["progress"] = progress,
            ["updated"] = updated
        }
    };

    private static string Money(object? value)
    {
        if (value is null || !TryNumber(value, out var number))
        {
            return "—";
        }

        return number.ToString("C0", CultureInfo.GetCultureInfo("en-US"));
    }

    private static bool TryNumber(object? value, out double number)
    {
        number = 0;
        if (value is null)
        {
            return false;
        }

        try
        {
            number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
