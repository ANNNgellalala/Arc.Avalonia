using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Feedback.Controls;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>Demo compositions for the data gallery pages.</summary>
internal static class DataDemos
{
    public static Control Filters()
    {
        var toolbar = new ArcFilterToolbar
        {
            Label = "Project filters",
            SearchText = string.Empty,
            SearchPlaceholder = "Search projects",
            ResultCount = 128,
            ResultLabel = "projects",
            SortKey = "updated",
            Filters = new[]
            {
                new ArcFilterChip { Id = "status", Label = "Status", Value = "Open" },
                new ArcFilterChip { Id = "owner", Label = "Owner", Value = "Maya" }
            },
            Fields = new[]
            {
                new ArcFilterField
                {
                    Id = "status",
                    Label = "Status",
                    Options = new List<ArcFilterOption>
                    {
                        new() { Value = "Open" },
                        new() { Value = "Closed" },
                        new() { Value = "Blocked" }
                    }
                },
                new ArcFilterField
                {
                    Id = "owner",
                    Label = "Owner",
                    Options = new List<ArcFilterOption>
                    {
                        new() { Value = "Maya" },
                        new() { Value = "Leo" },
                        new() { Value = "Sara" },
                        new() { Value = "Tom" }
                    }
                },
                new ArcFilterField
                {
                    Id = "priority",
                    Label = "Priority",
                    Options = new List<ArcFilterOption>
                    {
                        new() { Value = "High", Label = "High priority" },
                        new() { Value = "Medium", Label = "Medium priority" },
                        new() { Value = "Low", Label = "Low priority" }
                    }
                }
            },
            SortOptions = new[]
            {
                new ArcSortOption { Key = "updated", Label = "Last updated" },
                new ArcSortOption { Key = "name", Label = "Name" },
                new ArcSortOption { Key = "budget", Label = "Budget" }
            }
        };

        return PageLayout.Narrow(1000, toolbar);
    }

    public static Control Table()
    {
        var columns = new[]
        {
            new ArcTableColumn { Key = "name", Label = "Project" },
            new ArcTableColumn { Key = "owner", Label = "Owner", Width = 150, Sortable = false },
            new ArcTableColumn { Key = "status", Label = "Status", Width = 110, Sortable = false },
            new ArcTableColumn
            {
                Key = "budget",
                Label = "Budget",
                Width = 130,
                Numeric = true,
                Format = value => value is IConvertible
                    ? Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("C0", CultureInfo.GetCultureInfo("en-US"))
                    : "—"
            },
            new ArcTableColumn { Key = "updated", Label = "Updated", Width = 120, Sortable = false }
        };

        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["id"] = "p1", ["name"] = "Harbour", ["owner"] = "Maya Chen", ["status"] = "Open", ["budget"] = 42000, ["updated"] = "2h ago" },
            new() { ["id"] = "p2", ["name"] = "Atlas", ["owner"] = "Leo Park", ["status"] = "Blocked", ["budget"] = 18500, ["updated"] = "4h ago" },
            new() { ["id"] = "p3", ["name"] = "Harbour analytics", ["owner"] = "Sara Ruiz", ["status"] = "Open", ["budget"] = 86000, ["updated"] = "Yesterday" },
            new() { ["id"] = "p4", ["name"] = "Northwind", ["owner"] = "Tom Hale", ["status"] = "Closed", ["budget"] = 12800, ["updated"] = "2d ago" },
            new() { ["id"] = "p5", ["name"] = "Lattice", ["owner"] = "Ines Ma", ["status"] = "Open", ["budget"] = 54000, ["updated"] = "3d ago" },
            new() { ["id"] = "p6", ["name"] = "Orchard", ["owner"] = "Leo Park", ["status"] = "Closed", ["budget"] = 9700, ["updated"] = "5d ago" }
        };

        var table = new ArcSortableDataTable
        {
            Rows = rows,
            Columns = columns,
            Caption = "Projects",
            Selectable = true,
            SelectedKeys = new[] { "p2" },
            DefaultSort = new ArcTableSort { Key = "name", Direction = ArcSortDirection.Asc },
            ItemName = new ArcTableItemName { One = "project", Other = "projects" }
        };

        var empty = new ArcSortableDataTable
        {
            Rows = Array.Empty<object>(),
            Columns = columns,
            Caption = "Archived projects",
            EmptyMessage = "No archived projects"
        };

        return PageLayout.Narrow(860, PageLayout.Stack(20, table, empty));
    }

    public static Control Tree()
    {
        var nodes = new List<ArcTreeNode>
        {
            new()
            {
                Id = "src",
                Label = "src",
                Children = new List<ArcTreeNode>
                {
                    new()
                    {
                        Id = "components",
                        Label = "components",
                        Children = new List<ArcTreeNode>
                        {
                            new() { Id = "button", Label = "button.tsx" },
                            new() { Id = "card", Label = "card.tsx" },
                            new() { Id = "dialog", Label = "dialog.tsx" }
                        }
                    },
                    new()
                    {
                        Id = "app",
                        Label = "app",
                        Children = new List<ArcTreeNode>
                        {
                            new() { Id = "page", Label = "page.tsx" },
                            new() { Id = "layout", Label = "layout.tsx" }
                        }
                    },
                    new()
                    {
                        Id = "lib",
                        Label = "lib",
                        Children = new List<ArcTreeNode>
                        {
                            new() { Id = "format", Label = "format.ts" }
                        }
                    }
                }
            },
            new()
            {
                Id = "public",
                Label = "public",
                Children = new List<ArcTreeNode>
                {
                    new() { Id = "favicon", Label = "favicon.ico" }
                }
            },
            new() { Id = "package", Label = "package.json" },
            new() { Id = "readme", Label = "README.md" }
        };

        var tree = new ArcTreeView
        {
            Nodes = nodes,
            Label = "Files",
            DefaultExpandedIds = new[] { "src", "components", "app" },
            SelectedId = "card"
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 32,
            Children =
            {
                new ArcCard { Content = tree, Padding = new Thickness(14), Width = 440 },
                PageLayout.Narrow(
                    400,
                    PageLayout.Caption("Keyboard: ArrowUp and ArrowDown move, ArrowRight opens or enters, ArrowLeft closes or goes to the parent, Enter selects and toggles."))
            }
        };
    }

    public static Control Code()
    {
        const string snippet = """
            import { useCallback, useState } from "react";

            type Upload = { id: string; name: string; progress: number };

            export function useUploads() {
              const [uploads, setUploads] = useState<Upload[]>([]);

              const start = useCallback((file: File) => {
                const id = crypto.randomUUID();
                setUploads((current) => [
                  ...current,
                  { id, name: file.name, progress: 0 },
                ]);
                return id;
              }, []);

              const update = useCallback((id: string, progress: number) => {
                setUploads((current) =>
                  current.map((item) => (item.id === id ? { ...item, progress } : item)),
                );
              }, []);

              return { uploads, start, update };
            }
            """;

        var block = new ArcCodeBlock
        {
            Code = snippet,
            Filename = "use-uploads.ts",
            Language = "tsx",
            MaxLines = 12
        };

        return PageLayout.Narrow(780, block);
    }

    public static Control Json()
    {
        const string payload = """
            {
              "event": "invoice.paid",
              "created_at": "2026-09-22T08:41:07Z",
              "livemode": true,
              "attempt": 3,
              "data": {
                "amount": 4200,
                "currency": "usd",
                "customer": {
                  "id": "cus_N4x",
                  "name": "Maya Chen",
                  "email": "maya@acme.com",
                  "plan": "scale"
                },
                "lines": [
                  { "description": "Seat", "quantity": 12, "unit_amount": 300 },
                  { "description": "Storage", "quantity": 1, "unit_amount": 200 },
                  { "description": "Support", "quantity": 1, "unit_amount": 400 },
                  { "description": "Audit log", "quantity": 1, "unit_amount": 200 },
                  { "description": "Exports", "quantity": 2, "unit_amount": 50 }
                ]
              }
            }
            """;

        var viewer = new ArcJsonViewer
        {
            Data = payload,
            RootName = "event",
            DefaultExpandDepth = 2,
            PageSize = 3,
            Query = "maya",
            Label = "Webhook payload",
            ScrollHeight = 0,
            ShowPath = true,
            MaxWidth = 800,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        return new ArcCard
        {
            Content = viewer,
            Padding = new Thickness(18),
            Width = 820,
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }

    public static Control Heatmap()
    {
        var random = new Random(7);
        var days = new List<ArcHeatmapDay>();
        var start = new DateOnly(DateTime.Today.Year, 1, 1);
        var end = DateOnly.FromDateTime(DateTime.Today);
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var count = weekend ? random.Next(0, 5) : random.Next(0, 13);
            if (random.Next(0, 100) < 18)
            {
                count = 0;
            }

            days.Add(new ArcHeatmapDay { Date = date, Count = count });
        }

        var selected = days.TakeLast(9).FirstOrDefault(day => day.Count > 4) ?? days[^1];

        var heatmap = new ArcActivityHeatmap
        {
            Days = days,
            Label = "Activity heatmap",
            Period = start.Year.ToString(CultureInfo.InvariantCulture),
            Unit = "contributions",
            WeekStartsOn = 1,
            SelectedDate = selected.Date,
            Thresholds = new double[] { 2, 5, 9 },
            Actions = new TextBlock
            {
                Text = "Jan to today",
                Classes = { "arc-caption" }
            },
            Width = 900
        };

        return PageLayout.Narrow(940, heatmap);
    }
}
