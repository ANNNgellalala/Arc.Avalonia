using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>Shared demo sections, split across four pages so each screenshot fits the gallery viewport.</summary>
internal static class InputsSections
{
    public static Control Input() => DemoUi.Section(
        "Input",
        "Any single-line text value: name, email or URL, with helper and error copy that reword in place.",
        DemoUi.Row(
            new ArcInput
            {
                Width = 280,
                Label = "Email",
                Placeholder = "you@acme.com",
                Description = "We only use this for receipts."
            },
            DemoUi.Field("Workspace", new ArcInput
            {
                Value = "Northwind",
                LeadingIcon = "briefcase",
                Description = "Renaming keeps the URL."
            }),
            DemoUi.Field("Invite by email", new ArcInput
            {
                Value = "maya@acme",
                Error = "Enter a valid email address."
            }),
            DemoUi.Field("Seat count", new ArcInput
            {
                Value = "42",
                TrailingIcon = "users",
                Description = "Up to 500 seats."
            }),
            DemoUi.Field("Company", new ArcInput
            {
                Value = "Acme",
                IsEnabled = false,
                Description = "Set by your administrator."
            }),
            DemoUi.Field("Small", new ArcInput { Placeholder = "36 px", Size = ArcFieldSize.Sm }),
            DemoUi.Field("Large", new ArcInput { Placeholder = "50 px", Size = ArcFieldSize.Lg })));

    public static Control TextareaPassword() => DemoUi.Section(
        "Textarea and password",
        "Multi-line notes get a counter and optional auto-grow; passwords get a reveal toggle and a strength meter.",
        DemoUi.Row(
            DemoUi.Field("Bio", new ArcTextarea
            {
                Placeholder = "A sentence or two about the workspace.",
                Rows = 4,
                Description = "Shown on your public profile."
            }),
            DemoUi.Field("Release notes", new ArcTextarea
            {
                AutoGrow = true,
                Rows = 3,
                Value = "Fixed the import flow\nand tidied the empty state.",
                ShowCounter = true,
                MaxLength = 280,
                Description = "Markdown is supported."
            }),
            DemoUi.Field("Password", new ArcPasswordField
            {
                Value = "correct horse",
                Placeholder = "Enter a password",
                Description = "At least 8 characters."
            }),
            DemoUi.Field("Confirm password", new ArcPasswordField
            {
                Value = "correct hors",
                Revealed = true,
                Error = "Passwords do not match."
            }),
            DemoUi.Field("New password", new ArcPasswordStrength
            {
                Value = "Maya-2026",
                Description = "Rule checks update as you type."
            }),
            DemoUi.Field("Too short", new ArcPasswordStrength
            {
                Value = "maya",
                ShortLength = 8
            })));

    public static Control SearchNumbersMoney() => DemoUi.Section(
        "Search, numbers and money",
        "Filter a visible list in place, nudge a bounded number, or enter an exact amount in minor units.",
        DemoUi.Row(
            DemoUi.Field("Search members", new ArcSearchField
            {
                Placeholder = "Name or email",
                ShortcutHint = "⌘K"
            }),
            DemoUi.Field("Search invoices", new ArcSearchField
            {
                Value = "INV-2041",
                Placeholder = "Invoice number"
            }),
            DemoUi.Field("Seats", new ArcNumberField
            {
                Value = 5,
                Min = 1,
                Max = 500,
                Suffix = " seats",
                Description = "Hold a stepper to repeat."
            }),
            DemoUi.Field("Opacity", new ArcNumberField
            {
                Value = 60,
                Min = 0,
                Max = 100,
                Step = 5,
                Suffix = "%",
                Size = ArcFieldSize.Sm
            }),
            DemoUi.Field("Amount", new ArcMoneyInput
            {
                Value = 125000,
                Currency = "USD",
                Min = 100,
                Description = "Stored as 125000 minor units."
            }),
            DemoUi.Field("Budget", new ArcMoneyInput
            {
                Value = 12345678,
                Currency = "CHF",
                QuickAdd = new[] { 100, 500 }
            })));

    public static Control TagsSelectCombobox() => DemoUi.Section(
        "Tags, select and combobox",
        "Free-form tags, a short fixed list, or a searchable long list: pick the one that fits the value.",
        DemoUi.Row(
            DemoUi.Field("Topics", new ArcTagInput
            {
                Value = new[] { "design", "motion", "tokens" },
                Placeholder = "Add a tag",
                Description = "Enter or comma adds; Backspace removes the last."
            }),
            DemoUi.Field("Region", new ArcSelect
            {
                Value = "eu",
                Options = new[]
                {
                    new ArcSelectOption("us", "United States"),
                    new ArcSelectOption("eu", "Europe"),
                    new ArcSelectOption("ap", "Asia Pacific", disabled: true)
                },
                Description = "The menu glides to the selected row."
            }),
            DemoUi.Field("Sort order", new ArcSelect
            {
                Placeholder = "Select an option",
                Options = new[]
                {
                    new ArcSelectOption("recent", "Recently updated"),
                    new ArcSelectOption("name", "Name")
                }
            }),
            DemoUi.Field("Time zone", new ArcCombobox
            {
                Value = "cet",
                Options = new[]
                {
                    new ArcComboboxOption("utc", "UTC"),
                    new ArcComboboxOption("cet", "Central European", keywords: new[] { "zurich", "berlin" }),
                    new ArcComboboxOption("pst", "Pacific", keywords: new[] { "san francisco" }),
                    new ArcComboboxOption("jst", "Japan", keywords: new[] { "tokyo" })
                },
                Description = "Type to filter, Enter to pick."
            }),
            DemoUi.Field("Timezone", new ArcCombobox
            {
                Placeholder = "Search or select…",
                Options = new[]
                {
                    new ArcComboboxOption("utc", "UTC"),
                    new ArcComboboxOption("cet", "Central European")
                }
            })));

    public static Control MultiSelectChips() => DemoUi.Section(
        "Multi-select and filter chips",
        "Pick several values while the field stays readable, or keep every filter visible as a chip.",
        DemoUi.Row(
            DemoUi.Field("Labels", new ArcMultiSelect
            {
                Value = new[] { "bug", "feature", "docs" },
                MaxVisible = 2,
                Options = new[]
                {
                    new ArcSelectOption("bug", "Bug"),
                    new ArcSelectOption("feature", "Feature"),
                    new ArcSelectOption("docs", "Docs"),
                    new ArcSelectOption("design", "Design")
                },
                Description = "The rest collapse into +N."
            }),
            DemoUi.Field("Assignees", new ArcMultiSelect
            {
                Placeholder = "Select options",
                Options = new[]
                {
                    new ArcSelectOption("maya", "Maya Chen"),
                    new ArcSelectOption("jon", "Jon Bell"),
                    new ArcSelectOption("ravi", "Ravi Patel")
                }
            }),
            DemoUi.Field("Topics", new ArcChipGroup
            {
                Label = "Topics",
                Multiple = true,
                MaxVisible = 4,
                Value = new[] { "design", "motion" },
                Options = new[]
                {
                    new ArcChipOption("design", "Design"),
                    new ArcChipOption("motion", "Motion"),
                    new ArcChipOption("code", "Code"),
                    new ArcChipOption("research", "Research"),
                    new ArcChipOption("writing", "Writing")
                }
            }),
            DemoUi.Field("Status", new ArcChipGroup
            {
                Label = "Status",
                Multiple = false,
                Value = new[] { "open" },
                Options = new[]
                {
                    new ArcChipOption("open", "Open"),
                    new ArcChipOption("closed", "Closed"),
                    new ArcChipOption("archived", "Archived")
                }
            })));

    public static Control Toggles() => DemoUi.Section(
        "Checkbox, radio and switch",
        "A checkbox waits for submit, a radio group shows one visible option, a switch applies at once.",
        DemoUi.Row(
            DemoUi.FixedColumn(280,
                DemoUi.Label("Notifications"),
                new ArcCheckbox
                {
                    Checked = true,
                    Label = "Email notifications",
                    Description = "A daily summary at 9:00."
                },
                new ArcCheckbox
                {
                    Checked = null,
                    Label = "Select all projects",
                    Description = "Some projects are selected."
                },
                new ArcCheckbox
                {
                    Checked = false,
                    Label = "Weekly digest",
                    Description = "Sent on Friday."
                },
                new ArcCheckbox
                {
                    Checked = true,
                    IsEnabled = false,
                    Label = "Security alerts",
                    Description = "Always on for admins."
                }),
            new ArcRadioGroup
            {
                Label = "Plan",
                Value = "team",
                Width = 300,
                Options = new[]
                {
                    new ArcRadioOption("solo", "Solo", "One seat and one workspace."),
                    new ArcRadioOption("team", "Team", "Up to 20 seats with shared labels."),
                    new ArcRadioOption("scale", "Scale", "Unlimited seats and SSO.")
                }
            },
            DemoUi.FixedColumn(280,
                DemoUi.Label("Settings"),
                new ArcSwitch { Label = "Email notifications", Checked = true },
                new ArcSwitch { Label = "Product updates", Checked = false },
                new ArcSwitch { Label = "Usage alerts", Checked = true, IsEnabled = false })));

    public static Control SegmentedSliderRating() => DemoUi.Section(
        "Segmented, slider and rating",
        "Switch between a few views with one gliding highlight, pick an approximate value, or collect a score.",
        DemoUi.Row(
            DemoUi.Column(
                new ArcSegmentedControl
                {
                    Width = 300,
                    Label = "Range",
                    Value = "week",
                    Options = new[]
                    {
                        new ArcSegmentOption("day", "Day"),
                        new ArcSegmentOption("week", "Week"),
                        new ArcSegmentOption("month", "Month")
                    }
                },
                new ArcSegmentedControl
                {
                    Width = 300,
                    Label = "Density",
                    Value = "standard",
                    Options = new[]
                    {
                        new ArcSegmentOption("compact", "Compact"),
                        new ArcSegmentOption("standard", "Standard"),
                        new ArcSegmentOption("comfortable", "Comfortable")
                    }
                }),
            DemoUi.FixedColumn(300,
                new ArcSlider
                {
                    Label = "Monthly budget",
                    Value = 40,
                    Min = 0,
                    Max = 100,
                    Step = 5,
                    Marks = new[] { new ArcSliderMark(0), new ArcSliderMark(50), new ArcSliderMark(100) },
                    Format = value => $"${value:0}",
                    Description = "Arrow keys nudge by 5, PageUp jumps by 10."
                },
                new ArcSlider
                {
                    Label = "Volume",
                    Value = 70,
                    Min = 0,
                    Max = 100,
                    Description = "Drag the thumb or use the keyboard."
                }),
            DemoUi.Column(
                new ArcRating
                {
                    Label = "Overall experience",
                    Value = 4,
                    Description = "Hover previews; Backspace clears."
                },
                new ArcRating
                {
                    Label = "Average rating",
                    Value = 4.5,
                    AllowHalf = true,
                    ReadOnly = true,
                    ShowValue = true,
                    Description = "Read only, shown with half stars."
                })));
}
