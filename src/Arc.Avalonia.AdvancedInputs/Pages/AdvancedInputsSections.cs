using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.AdvancedInputs.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.AdvancedInputs.Pages;

/// <summary>Demo sections shared by the advanced input pages, split so each screenshot fits 1400 px.</summary>
internal static class AdvancedInputsSections
{

    /// <summary>A label, control and hint column at the given width; DemoUi.Field is fixed at 280.</summary>
    private static Control Field(string label, Control control, string? hint = null, double width = 280)
    {
        var panel = new StackPanel { Spacing = 6, Width = width, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new TextBlock { Text = label, Classes = { "arc-label" } });
        panel.Children.Add(control);
        if (!string.IsNullOrWhiteSpace(hint))
        {
            panel.Children.Add(new TextBlock
            {
                Text = hint,
                Classes = { "arc-caption" },
                TextWrapping = TextWrapping.Wrap
            });
        }

        return panel;
    }

    private static readonly List<ArcMentionPerson> People = new()
    {
        new ArcMentionPerson("u1", "Maya Chen", "Design lead"),
        new ArcMentionPerson("u2", "Theo Park", "Engineer"),
        new ArcMentionPerson("u3", "Priya Raman", "Product manager"),
        new ArcMentionPerson("u4", "Sam Okafor", "Support")
    };

    private static readonly List<ArcMentionChannel> Channels = new()
    {
        new ArcMentionChannel("c1", "launch", "Release planning", 14),
        new ArcMentionChannel("c2", "design-system", "Tokens and components", 32),
        new ArcMentionChannel("c3", "support", "Customer reports", 8)
    };

    /// <summary>Runs page-side setup once the control is laid out, so states that need a template can show.</summary>
    internal static T Later<T>(T control, Action<T> action)
        where T : Control
    {
        control.AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(() => action(control), DispatcherPriority.Background);
        return control;
    }

    public static Control InlineEdit()
    {
        var editing = Later(
            new ArcInlineEdit { Width = 300, Label = "Workspace", Value = "Northwind" },
            edit => edit.BeginEdit());

        var invalid = Later(
            new ArcInlineEdit
            {
                Width = 300,
                Label = "Team name",
                Validate = draft => string.IsNullOrWhiteSpace(draft) ? "Name can't be empty" : null
            },
            edit =>
            {
                edit.BeginEdit();
                edit.Commit();
            });

        var saving = new ArcInlineEdit { Width = 300, Label = "Board name", Value = "Roadmap" };
        var pending = new TaskCompletionSource();
        saving.Save += (_, args) => args.Pending = pending.Task;
        Later(saving, edit =>
        {
            edit.BeginEdit();
            edit.Commit();
        });

        return DemoUi.Section(
            "Inline edit",
            "For a single value read far more often than edited, such as a project name: click or press Enter to rename, Enter saves, Escape rolls back.",
            DemoUi.Row(
                Field(
                    "Title",
                    new ArcInlineEdit { Width = 300, Label = "Project name", Value = "Northwind redesign" },
                    "Saves on its own.", width: 300),
                Field(
                    "Description",
                    new ArcInlineEdit
                    {
                        Width = 300,
                        Label = "Project description",
                        Variant = ArcInlineEditVariant.Body,
                        Multiline = true,
                        Value = "A quieter dashboard for the finance team, shipping in March."
                    },
                    "Multiline grows with the text.", width: 300),
                Field(
                    "Empty",
                    new ArcInlineEdit { Width = 300, Label = "Release name", Placeholder = "Untitled release" }, width: 300),
                Field("Validation", invalid, "Blocked saves keep the field open.", width: 300),
                Field("Saving", saving, "A pending save shows a spinner.", width: 300),
                Field("Editing", editing, "Text becomes a field in place.", width: 300)));
    }

    public static Control ScrubInput()
    {
        var width = new ArcScrubInput
        {
            Width = 176,
            Label = "W",
            Value = 240,
            Min = 0,
            Max = 1000,
            Step = 1,
            Units = new List<string> { "px", "%" },
            Unit = "px",
            ResetValue = 240
        };

        var height = new ArcScrubInput
        {
            Width = 176,
            Label = "H",
            Value = 160,
            Min = 0,
            Max = 1000,
            Units = new List<string> { "px", "%" },
            Unit = "px"
        };

        var opacity = new ArcScrubInput
        {
            Width = 176,
            Label = "Opacity",
            Value = 100,
            Min = 0,
            Max = 100,
            Step = 1,
            Unit = "%",
            Size = ArcScrubInputSize.Sm
        };

        var rotation = new ArcScrubInput
        {
            Width = 176,
            Label = "R",
            Value = 0,
            Min = 0,
            Max = 360,
            Wrap = true,
            Step = 1,
            FineStep = 0.1,
            Unit = "°",
            Units = new List<string> { "°" },
            ResetValue = 0,
            Size = ArcScrubInputSize.Sm
        };

        var typed = new ArcScrubInput
        {
            Width = 176,
            Label = "Gap",
            Value = 12,
            Min = 0,
            Max = 200,
            Unit = "px",
            Precision = 2
        };

        var disabled = new ArcScrubInput
        {
            Width = 176,
            Label = "Locked",
            Value = 32,
            Min = 0,
            Max = 100,
            Unit = "px",
            IsEnabled = false
        };

        return DemoUi.Section(
            "Scrub input",
            "For inspector values tuned by feel: drag the label to scrub (Shift is coarse, Alt is fine), click to type a value with a unit or arithmetic such as (100-24)/2.",
            DemoUi.Canvas(
                new StackPanel
                {
                    Spacing = 14,
                    Children =
                    {
                        new TextBlock { Text = "Frame", Classes = { "arc-caption" } },
                        DemoUi.Row(width, height, opacity, rotation),
                        new TextBlock { Text = "Typed values", Classes = { "arc-caption" }, Margin = new Thickness(0, 4, 0, 0) },
                        DemoUi.Row(typed, disabled)
                    }
                }));
    }

    public static Control PhoneInput()
    {
        var open = new ArcPhoneInput
        {
            Width = 300,
            Label = "Country picker",
            DefaultCountry = "US",
            Description = "The list grows out of the flag button."
        };
        Later(open, input => input.OpenCountryPicker());

        var empty = new ArcPhoneInput
        {
            Width = 300,
            Label = "Phone number",
            DefaultCountry = "US",
            Required = true,
            Description = "We only text about your order."
        };

        var filled = new ArcPhoneInput
        {
            Width = 300,
            Label = "Mobile",
            DefaultCountry = "GB",
            Value = "+447911123456",
            PreferredCountries = new List<string> { "GB", "IE", "US" }
        };

        var invalid = new ArcPhoneInput
        {
            Width = 300,
            Label = "Work number",
            DefaultCountry = "DE",
            Value = "+491511",
            Error = "That number is too short."
        };

        return DemoUi.Section(
            "Phone input",
            "For sign up, checkout and two factor setup: pick the country, format as you type, and read the value back as E.164.",
            DemoUi.Row(open, empty, filled, invalid));
    }

    public static Control ShortcutRecorder()
    {
        var recording = Later(
            new ArcShortcutRecorder
            {
                Width = 280,
                Label = "Open search",
                Description = "Press a chord. Escape cancels."
            },
            recorder => recorder.BeginRecording());

        var recorded = new ArcShortcutRecorder
        {
            Width = 280,
            Label = "Toggle sidebar",
            Gesture = "Ctrl+Shift+K",
            ResetValue = "Ctrl+Shift+K",
            Bindings = new List<ArcShortcutBinding>
            {
                new("Ctrl+K", "Open search"),
                new("Ctrl+P", "Print")
            },
            Description = "Ctrl+K and Ctrl+P are taken."
        };

        var emptyRecorder = new ArcShortcutRecorder
        {
            Width = 280,
            Label = "Duplicate frame",
            Placeholder = "Record shortcut"
        };

        var disabled = new ArcShortcutRecorder
        {
            Width = 280,
            Label = "Managed by your admin",
            Gesture = "Ctrl+Alt+R",
            IsEnabled = false
        };

        return DemoUi.Section(
            "Shortcut recorder",
            "For settings pages that remap keys: record a chord into key caps, clear it with Delete, and warn before taking a combination already in use.",
            DemoUi.Row(recorded, emptyRecorder, recording, disabled));
    }

    public static Control MentionInput()
    {
        const string text = "Thanks @Maya Chen for the review, shipping in #launch.";
        var mentions = new List<ArcMention>();
        var person = text.IndexOf("@Maya Chen", StringComparison.Ordinal);
        if (person >= 0)
        {
            mentions.Add(new ArcMention(ArcMentionKind.Person, "u1", "Maya Chen", person, person + "@Maya Chen".Length));
        }

        var channel = text.IndexOf("#launch", StringComparison.Ordinal);
        if (channel >= 0)
        {
            mentions.Add(new ArcMention(ArcMentionKind.Channel, "c1", "launch", channel, channel + "#launch".Length));
        }

        var filled = new ArcMentionInput
        {
            Width = 360,
            Label = "Comment",
            MinRows = 2,
            People = People,
            Channels = Channels,
            Value = new ArcMentionValue(text, mentions),
            Description = "Mentions are tokens with ids behind the text."
        };

        var suggesting = new ArcMentionInput
        {
            Width = 360,
            Label = "Reply",
            MinRows = 2,
            People = People,
            Channels = Channels,
            Description = "Type @ or # to tag someone."
        };
        Later(suggesting, input =>
        {
            input.Insert("Looks good, ");
            input.OpenSuggestions(ArcMentionKind.Person);
        });

        var composer = new ArcMentionInput
        {
            Width = 360,
            Label = "Quick note",
            MinRows = 1,
            MaxRows = 4,
            SubmitOnEnter = true,
            Placeholder = "Write a note, @ to mention",
            People = People,
            Channels = Channels
        };

        return DemoUi.Section(
            "Mention input",
            "For comment boxes and task descriptions: typing @ lists people and # lists channels at the caret, and the value keeps ids next to the plain text.",
            DemoUi.Row(filled, suggesting, composer));
    }

    public static Control ExpandingSearch()
    {
        var items = new List<ArcExpandingSearchItem>
        {
            new("p1", "Arc website", "Projects", "Updated today", new[] { "marketing", "site" }),
            new("p2", "Northwind dashboard", "Projects", "3 days ago", new[] { "finance" }),
            new("d1", "Motion tokens", "Docs", "Spring, duration", new[] { "spring", "motion" }),
            new("d2", "Focus rings", "Docs", "Accessibility", new[] { "keyboard", "focus" }),
            new("d3", "Release checklist", "Docs", "Shipping", new[] { "release" })
        };

        var suggestions = new List<ArcExpandingSearchItem>
        {
            new("s1", "Arc website", "Recent", "Yesterday"),
            new("s2", "Motion tokens", "Recent", "Last week")
        };

        var collapsed = new ArcExpandingSearch
        {
            Width = 420,
            Label = "Search projects and docs",
            Items = items,
            Suggestions = suggestions,
            Anchor = ArcExpandingSearchAnchor.Start,
            ExpandedWidth = 400
        };

        var expanded = new ArcExpandingSearch
        {
            Width = 420,
            Label = "Search projects and docs",
            Items = items,
            Suggestions = suggestions,
            ExpandedWidth = 400
        };
        Later(expanded, search => search.Expand());

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 24,
            Children =
            {
                new Border
                {
                    Width = 420,
                    Height = 200,
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = "Collapsed, in a toolbar", Classes = { "arc-caption" } },
                            collapsed
                        }
                    }
                },
                new Border
                {
                    Width = 420,
                    Height = 200,
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = "Expanded, with recent items", Classes = { "arc-caption" } },
                            expanded
                        }
                    }
                }
            }
        };

        return DemoUi.Section(
            "Expanding search",
            "For a header where a full field would crowd the layout: the icon morphs into a field on a width spring, and Escape folds it back and clears it.",
            DemoUi.Canvas(row, 20));
    }

    public static Control MorphSelect()
    {
        var closed = new ArcMorphSelect
        {
            Width = 200,
            Label = "Time zone",
            Value = "europe/zurich",
            Items = Zones()
        };

        var closedNoValue = new ArcMorphSelect
        {
            Width = 200,
            Label = "Assignee",
            Placeholder = "Unassigned",
            Searchable = true,
            Items = new List<ArcMorphSelectItem>
            {
                new ArcMorphSelectOption("u1", "Maya Chen", "Design"),
                new ArcMorphSelectOption("u2", "Theo Park", "Engineering"),
                new ArcMorphSelectOption("u3", "Priya Raman", "Product")
            }
        };

        var open = new ArcMorphSelect
        {
            Width = 200,
            Label = "Region",
            Value = "america/new_york",
            PanelWidth = 272,
            IsOpen = true,
            Items = Zones()
        };

        return DemoUi.Section(
            "Morph select",
            "For a compact property picker: the trigger itself grows into the list with a gliding highlight, type-ahead, and no separate popup.",
            DemoUi.Canvas(
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 28,
                    Children =
                    {
                        new Border
                        {
                            Width = 320,
                            Height = 360,
                            Child = new StackPanel
                            {
                                Spacing = 8,
                                Children =
                                {
                                    new TextBlock { Text = "Open, grown from the trigger", Classes = { "arc-caption" } },
                                    open
                                }
                            }
                        },
                        new Border
                        {
                            Width = 240,
                            Child = new StackPanel
                            {
                                Spacing = 8,
                                Children =
                                {
                                    new TextBlock { Text = "Closed", Classes = { "arc-caption" } },
                                    closed,
                                    new TextBlock { Text = "Searchable", Classes = { "arc-caption" }, Margin = new Thickness(0, 10, 0, 0) },
                                    closedNoValue
                                }
                            }
                        }
                    }
                }));
    }

    private static List<ArcMorphSelectItem> Zones() => new()
    {
        new ArcMorphSelectGroup(
            "Europe",
            new ArcMorphSelectOption("europe/london", "London", "UTC+0", "united kingdom"),
            new ArcMorphSelectOption("europe/zurich", "Zurich", "UTC+1", "switzerland"),
            new ArcMorphSelectOption("europe/lisbon", "Lisbon", "UTC+0", "portugal")),
        new ArcMorphSelectGroup(
            "Americas",
            new ArcMorphSelectOption("america/new_york", "New York", "UTC−5", "united states eastern"),
            new ArcMorphSelectOption("america/chicago", "Chicago", "UTC−6", "central"),
            new ArcMorphSelectOption("america/los_angeles", "Los Angeles", "UTC−8", "pacific"))
    };

    public static Control ElasticSlider()
    {
        var volume = new ArcElasticSlider
        {
            Width = 300,
            Label = "Volume",
            Value = 62,
            Min = 0,
            Max = 100,
            Step = 1,
            Marks = new List<ArcSliderMark>
            {
                new(0),
                new(50, "50%"),
                new(100)
            },
            StartIcon = "minus",
            EndIcon = "plus",
            Format = value => value.ToString("0") + "%"
        };

        var speed = new ArcElasticSlider
        {
            Width = 300,
            Label = "Playback speed",
            Value = 1.25,
            Min = 0.5,
            Max = 2,
            Step = 0.25,
            Marks = new List<ArcSliderMark>
            {
                new(1, "1x"),
                new(1.5, "1.5x"),
                new(2, "2x")
            },
            Format = value => value.ToString("0.##") + "x"
        };

        var quiet = new ArcElasticSlider
        {
            Width = 300,
            Label = "Brightness",
            Value = 30,
            ShowValue = true
        };

        return DemoUi.Section(
            "Elastic slider",
            "For one setting people drag to taste: the track stretches past its ends with resistance, the tag sways with the drag, and marks act as soft detents.",
            DemoUi.Row(volume, speed, quiet));
    }

    public static Control RangeHistogramSlider()
    {
        var random = new Random(7);
        var prices = new List<double>();
        for (var i = 0; i < 420; i++)
        {
            var value = 60 + random.NextDouble() * 340;
            if (random.NextDouble() < 0.35)
            {
                value = 120 + random.NextDouble() * 90;
            }

            prices.Add(Math.Round(value));
        }

        var price = new ArcRangeHistogramSlider
        {
            Width = 420,
            Label = "Price per night",
            Data = prices,
            Min = 0,
            Max = 500,
            Step = 5,
            Bins = 28,
            Value = new ArcRange(80, 320),
            Format = value => "$" + value.ToString("0")
        };

        var distance = new ArcRangeHistogramSlider
        {
            Width = 420,
            Label = "Distance",
            Data = prices,
            Min = 0,
            Max = 500,
            Step = 5,
            Bins = 20,
            SnapToBins = false,
            Value = new ArcRange(40, 220),
            ThumbLabels = new List<string> { "Nearest", "Furthest" },
            Format = value => value.ToString("0") + " km"
        };

        return DemoUi.Section(
            "Range histogram slider",
            "For price, size or distance filters: the histogram shows where the results sit, bars inside the range light up, and released thumbs settle on bar edges.",
            DemoUi.Row(price, distance));
    }

    public static Control BillingToggle()
    {
        var options = new List<ArcBillingToggleOption>
        {
            new("monthly", "Monthly"),
            new("yearly", "Yearly", "Save 20%", "You save $48")
        };

        var yearlyToggle = new ArcBillingToggle { Options = options, Value = "yearly" };
        var monthlyToggle = new ArcBillingToggle { Options = options, Value = "monthly" };

        var yearlyPrice = new ArcBillingPrice
        {
            Amount = 16,
            Was = 20,
            Period = "per month, billed yearly",
            Width = 300
        };

        var monthlyPrice = new ArcBillingPrice
        {
            Amount = 20,
            Period = "per month",
            Width = 300
        };

        var large = new ArcBillingToggle
        {
            Options = options,
            Value = "monthly",
            Size = ArcBillingToggleSize.Lg
        };

        var yearlyCard = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = "Pro", Classes = { "arc-label" } },
                yearlyToggle,
                yearlyPrice,
                new TextBlock { Text = "Unlimited projects and history.", Classes = { "arc-caption" } }
            }
        };

        var monthlyCard = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = "Starter", Classes = { "arc-label" } },
                monthlyToggle,
                monthlyPrice,
                new TextBlock { Text = "Everything to get going.", Classes = { "arc-caption" } }
            }
        };

        return DemoUi.Section(
            "Billing toggle",
            "For the period switch above pricing cards: one thumb glides between the options, the savings note rewrites itself in place, and the prices roll together.",
            new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    DemoUi.Row(
                        new ArcCard { Content = yearlyCard, Width = 320 },
                        new ArcCard { Content = monthlyCard, Width = 320 },
                        DemoUi.Column(
                            new TextBlock { Text = "Large, md text", Classes = { "arc-caption" } },
                            large))
                }
            });
    }

    public static Control RadioCards()
    {
        var plans = new List<ArcRadioCardOption>
        {
            new("starter", "Starter", "One project and community support", "$0", "zap"),
            new("growth", "Growth", "Unlimited projects, 10 seats", "$24", "sparkles"),
            new("scale", "Scale", "SSO and an audit log", "$96", "users", disabled: true,
                disabledReason: "Available on the enterprise plan")
        };

        var grid = new ArcRadioCards
        {
            Width = 720,
            Layout = ArcRadioCardsLayout.Grid,
            MinColumnWidth = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
            Value = "growth",
            Options = plans
        };

        var shipping = new List<ArcRadioCardOption>
        {
            new("standard", "Standard", "4 to 6 business days", "Free"),
            new("express", "Express", "2 business days", "$12"),
            new("overnight", "Overnight", "Tomorrow morning", "$29", disabled: true,
                disabledReason: "Not available for this address")
        };

        var list = new ArcRadioCards
        {
            Width = 560,
            Layout = ArcRadioCardsLayout.List,
            Value = "express",
            HorizontalAlignment = HorizontalAlignment.Left,
            Options = shipping
        };

        return DemoUi.Section(
            "Radio cards",
            "For three to six choices that each need a description or a price: one ring springs to the selected card and the group keeps radio keyboard behaviour.",
            new StackPanel
            {
                Spacing = 24,
                Children =
                {
                    grid,
                    list
                }
            });
    }
}
