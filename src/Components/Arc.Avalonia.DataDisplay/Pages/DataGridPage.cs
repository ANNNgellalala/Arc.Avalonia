using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.DataDisplay.Pages;

/// <summary>An editable, sortable, resizable data grid with checked rows and totals.</summary>
public sealed class DataGridPage : ArcPage
{
    public override string Title => "Data grid";

    public override string Group => "Tables";

    public override string? Description =>
        "Edit values in place with sorting, resizing, range checks, and totals. This port leaves out clipboard ranges, the fill handle, grouping, export, and virtualisation.";

    public override int Order => 309;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Budget grid",
                "Use when people edit many values in place, like a budget, price list, or inventory, not for one record at a time.",
                DataGridDemos.DataGrid()),
            DemoUi.Section(
                "Empty state",
                "Use when a filter or a new workspace leaves the grid with nothing to show, and offer the way back.",
                DataGridDemos.EmptyGrid())
        }
    };
}
