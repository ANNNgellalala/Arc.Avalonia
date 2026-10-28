using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>Demo compositions for the round 3 Pro pages: presence stack and data grid.</summary>
internal static class ProDemos
{
    private const string Maya = "avares://Arc.Avalonia.Feedback/Assets/portrait-maya.png";
    private const string Sara = "avares://Arc.Avalonia.Feedback/Assets/portrait-sara.png";
    private const string Leo = "avares://Arc.Avalonia.Feedback/Assets/portrait-leo.png";

    private static List<PresenceMember> People() => new()
    {
        new PresenceMember { Id = "maya", Name = "Maya Chen", Image = Maya, Status = ArcPresenceStatus.Available },
        new PresenceMember { Id = "leo", Name = "Leo Park", Image = Leo, Status = ArcPresenceStatus.Busy },
        new PresenceMember { Id = "jon", Name = "Jon Bell", Status = ArcPresenceStatus.Typing, IsTyping = true },
        new PresenceMember { Id = "sara", Name = "Sara Ruiz", Image = Sara, Status = ArcPresenceStatus.Away },
        new PresenceMember { Id = "tom", Name = "Tom Hale", Status = ArcPresenceStatus.Idle },
        new PresenceMember { Id = "ines", Name = "Ines Ma", Status = ArcPresenceStatus.Available }
    };

    // ---- Presence ---------------------------------------------------------

    public static Control Presence()
    {
        var small = new ArcPresenceStack
        {
            Members = People(),
            Max = 2,
            Size = ArcPresenceSize.Sm,
            ShowNames = true,
            ShowOthers = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            Label = "Reviewers here now"
        };

        var live = People();
        var medium = new ArcPresenceStack
        {
            Members = live,
            Max = 5,
            Size = ArcPresenceSize.Md,
            ShowNames = true,
            ShowOthers = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            Label = "People here now"
        };

        var large = new ArcPresenceStack
        {
            Members = People().GetRange(0, 4),
            Max = 4,
            Size = ArcPresenceSize.Lg,
            ShowNames = true,
            Fanned = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            Label = "Design crit attendees"
        };

        var cards = PageLayout.EqualRow(
            PresenceCard("Small", "28 px, Max 2, +4 overflow", small),
            PresenceCard("Medium", "36 px, Max 5, Jon is typing", medium),
            PresenceCard("Large", "44 px, held fanned with the name showing", large));

        var status = PageLayout.Caption($"{live.Count} people here");
        var join = new ArcButton
        {
            Content = "Someone joins",
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm
        };
        var leave = new ArcButton
        {
            Content = "Someone leaves",
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm
        };
        var pool = new Queue<PresenceMember>(new[]
        {
            new PresenceMember { Id = "ada", Name = "Ada Lin", Status = ArcPresenceStatus.Available },
            new PresenceMember { Id = "noor", Name = "Noor Haddad", Status = ArcPresenceStatus.Away }
        });

        void Update()
        {
            status.Text = $"{live.Count} people here";
            join.IsEnabled = pool.Count > 0;
            leave.IsEnabled = live.Count > 5;
        }

        join.Click += (_, _) =>
        {
            if (pool.Count == 0)
            {
                return;
            }

            live.Add(pool.Dequeue());
            medium.Refresh();
            Update();
        };
        leave.Click += (_, _) =>
        {
            if (live.Count <= 5)
            {
                return;
            }

            live.RemoveAt(live.Count - 1);
            medium.Refresh();
            Update();
        };
        Update();

        var liveRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        liveRow.Children.Add(status);
        liveRow.Children.Add(join);
        liveRow.Children.Add(leave);

        var content = new StackPanel { Spacing = 18 };
        content.Children.Add(cards);
        content.Children.Add(liveRow);
        content.Children.Add(Legend());
        return content;
    }

    private static Control PresenceCard(string title, string note, ArcPresenceStack stack) => new ArcCard
    {
        Padding = new Thickness(20),
        Content = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock { Text = title, Classes = { "arc-label" } },
                new TextBlock
                {
                    Text = note,
                    Classes = { "arc-caption" },
                    TextWrapping = TextWrapping.Wrap
                },
                new Border
                {
                    Padding = new Thickness(0, 20, 0, 32),
                    Child = stack
                }
            }
        }
    };

    private static Control Legend()
    {
        var items = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var status in new[]
                 {
                     ArcPresenceStatus.Available,
                     ArcPresenceStatus.Busy,
                     ArcPresenceStatus.Away,
                     ArcPresenceStatus.Typing,
                     ArcPresenceStatus.Idle
                 })
        {
            items.Children.Add(LegendItem(StatusLabel(status), StatusToken(status)));
        }

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(items);
        panel.Children.Add(new TextBlock
        {
            Text = "The dots are a secondary cue: the who's here list repeats every status in words.",
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap
        });
        return panel;
    }

    private static Control LegendItem(string label, string token)
    {
        var dot = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = DemoUi.Brush(token),
            VerticalAlignment = VerticalAlignment.Center
        };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 18, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(dot);
        row.Children.Add(new TextBlock
        {
            Text = label,
            Classes = { "arc-caption" },
            VerticalAlignment = VerticalAlignment.Center
        });
        return row;
    }

    public static Control WhosHere()
    {
        var people = People();
        var columns = new List<StackPanel>();
        for (var index = 0; index < 3; index++)
        {
            var column = new StackPanel { Spacing = 0 };
            for (var row = 0; row < 2; row++)
            {
                var person = people[index + row * 3];
                column.Children.Add(PersonRow(person));
            }

            column.Margin = new Thickness(0, 0, index == 2 ? 0 : 24, 0);
            columns.Add(column);
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
        for (var index = 0; index < columns.Count; index++)
        {
            Grid.SetColumn(columns[index], index);
            grid.Children.Add(columns[index]);
        }

        var content = new StackPanel { Spacing = 14 };
        content.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Everyone here now", Classes = { "arc-label" } },
                new TextBlock
                {
                    Text = $"{people.Count} people · updated just now",
                    Classes = { "arc-caption" },
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        });
        content.Children.Add(grid);
        return new ArcCard { Padding = new Thickness(20), Content = content };
    }

    private static Control PersonRow(PresenceMember member)
    {
        var avatar = new ArcAvatar
        {
            DisplayName = member.Name,
            Src = member.Image,
            Size = ArcAvatarSize.Sm
        };
        var details = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        details.Children.Add(new TextBlock { Text = member.Name, Classes = { "arc-body-sm" } });
        var statusRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center
        };
        statusRow.Children.Add(new Border
        {
            Width = 7,
            Height = 7,
            CornerRadius = new CornerRadius(4),
            Background = DemoUi.Brush(StatusToken(member.Status)),
            VerticalAlignment = VerticalAlignment.Center
        });
        statusRow.Children.Add(new TextBlock
        {
            Text = StatusLabel(member.Status),
            Classes = { "arc-caption" },
            VerticalAlignment = VerticalAlignment.Center
        });
        details.Children.Add(statusRow);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), MinHeight = 44 };
        row.Children.Add(avatar);
        Grid.SetColumn(details, 1);
        row.Children.Add(details);
        row.Margin = new Thickness(0, 0, 0, 12);
        return row;
    }

    private static string StatusLabel(ArcPresenceStatus status) => status switch
    {
        ArcPresenceStatus.Busy => "Busy",
        ArcPresenceStatus.Away => "Away",
        ArcPresenceStatus.Typing => "Typing",
        ArcPresenceStatus.Idle => "Idle",
        _ => "Available"
    };

    private static string StatusToken(ArcPresenceStatus status) => status switch
    {
        ArcPresenceStatus.Busy => ArcTokens.Danger,
        ArcPresenceStatus.Away => ArcTokens.Warning,
        ArcPresenceStatus.Typing => ArcTokens.Accent,
        ArcPresenceStatus.Idle => ArcTokens.TextMuted,
        _ => ArcTokens.Success
    };

    // ---- Data grid --------------------------------------------------------

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
