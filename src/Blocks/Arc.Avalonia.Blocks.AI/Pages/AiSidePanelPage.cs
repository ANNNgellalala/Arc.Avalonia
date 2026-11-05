using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using K = Arc.Avalonia.Blocks.AI.AiKit;

namespace Arc.Avalonia.Blocks.AI.Pages;

/// <summary>
/// AI side panel: an assistant docked beside the document it reads. It proposes an edit as a word diff; Apply
/// updates the page block in place and Undo puts it back. The page and the change are sample data.
/// </summary>
public sealed class AiSidePanelPage : ArcItemPage
{
    public override string ItemSlug => "ai-side-panel";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-page",
        "variant-change-card",
        "variant-context-chips",
        "variant-composer"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "An assistant next to a document, spec or record that can summarize it and propose edits.",
        "Copilot style help where edits must be approved, applied visibly and undone.",
        "Asking about a selected passage without leaving the page."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use AI chat for a standalone conversation, or AI composer for a prompt box on its own."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The panel is a labelled complementary region; its close and reopen controls report their expanded state.",
        "Diffs use distinguishing marks and words, so removed and added text never differ by colour alone.",
        "A polite status announces reading, applied with undo available, undone, discarded and stopped."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Apply sweeps a soft accent tint across the changed block once while the new text crossfades in.",
        "Reduced motion keeps the tint change but drops the sweep, the chunk fade and the panel slide."
    };

    public override string? ImplementationNotes =>
        "The word diff is precomputed for the sample edit rather than computed with an LCS pass, and Apply/Undo "
        + "only change the page copy kept on the page. Below 640 px the reference turns the panel into a bottom "
        + "sheet; at gallery width it always docks beside the page. Selecting text in the page to add context is "
        + "represented by a fixed context chip.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        const string original = "Refunds are issued within 30 days of the request, and original shipping is never refunded.";
        const string proposed = "Refunds are issued within 14 days of the request, and original shipping is refunded once.";

        var refundText = K.Text(original, "arc-body-sm", 560);
        var pageTint = new Border
        {
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 5, 8, 5),
            Margin = new Thickness(-8, 0, 0, 0),
            Child = refundText
        };
        var status = K.Caption("Reading the page. The assistant can change one block at a time.");
        var undoLabel = K.Caption("Applied to the page");
        var undoButton = K.IconBtn("undo-2", "Undo the applied edit", size: 12);
        undoButton.Content = K.Row(5,
            new ArcIcon { Icon = "undo-2", Size = 12, Foreground = K.Brush(ArcTokens.Accent) },
            new TextBlock { Text = "Undo", FontSize = 12 });
        var undoPill = new Border
        {
            IsVisible = false,
            Background = K.Tint(ArcTokens.Accent, 0.1),
            CornerRadius = new CornerRadius(9999),
            Padding = new Thickness(10, 3, 3, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = K.Row(6,
                new ArcIcon { Icon = "circle-check", Size = 12, Foreground = K.Brush(ArcTokens.Accent) },
                undoLabel,
                undoButton)
        };

        void Apply()
        {
            refundText.Text = proposed;
            pageTint.Background = K.Tint(ArcTokens.Success, 0.1);
            undoPill.IsVisible = true;
            status.Text = "Applied to the page. Undo is available.";
        }

        void Undo()
        {
            refundText.Text = original;
            pageTint.Background = Brushes.Transparent;
            undoPill.IsVisible = false;
            status.Text = "Undone. The page is back to the previous version.";
        }

        undoButton.Click += (_, _) => Undo();

        var diff = new StackPanel { Spacing = 3 };
        diff.Children.Add(K.WordDiff(
            "Refunds within |-30 days|+14 days|; shipping is |-never refunded|+refunded once|."));

        Border changeCard = null!;
        changeCard = new Border
        {
            Background = K.Brush(ArcTokens.SurfaceMuted),
            BorderBrush = K.Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 10, 12, 10),
            Child = K.Stack(8,
                K.Row(6,
                    K.Disc("pencil", 20, iconSize: 11),
                    K.Label("Change in Refunds")),
                diff,
                K.Row(6,
                    K.Btn("Apply", ArcButtonVariant.Primary, ArcButtonSize.Sm, Apply, icon: "check"),
                    K.Btn("Discard", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () =>
                    {
                        changeCard.IsVisible = false;
                        status.Text = "Change discarded. The page is unchanged.";
                    }),
                    K.Pill("1 block", ArcBadgeTone.Neutral)))
        };

        var composer = new ArcInput { Placeholder = "Ask about this page", Height = 42 };
        AutomationProperties.SetName(composer, "Message");
        var send = K.IconBtn("arrow-up", "Send message", variant: ArcButtonVariant.Primary);
        send.Width = 34;
        send.Height = 34;
        send.Padding = new Thickness(0);
        send.Click += (_, _) =>
        {
            status.Text = string.IsNullOrWhiteSpace(composer.Value)
                ? "Type a question first."
                : "Answer ready. Simulated reply for the sample page.";
        };

        var composerRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        composerRow.Children.Add(composer);
        Grid.SetColumn(send, 1);
        send.Margin = new Thickness(6, 0, 0, 0);
        composerRow.Children.Add(send);

        var plans = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 18,
            RowSpacing = 3
        };
        var headerCells = new Control[]
        {
            K.Caption("Plan"), K.Caption("Seats"), K.Caption("Price")
        };
        for (var column = 0; column < headerCells.Length; column++)
        {
            Grid.SetColumn(headerCells[column], column);
            plans.Children.Add(headerCells[column]);
        }

        var rows = new[]
        {
            new[] { "Team", "5 to 100", "$12 / seat" },
            new[] { "Business", "25 to 500", "$22 / seat" }
        };
        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var cell = K.Text(rows[row][column], "arc-body-sm");
                Grid.SetRow(cell, row + 1);
                Grid.SetColumn(cell, column);
                plans.Children.Add(cell);
            }
        }

        var page = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        K.Stack(2,
                            K.Text("Northwind pricing", "arc-heading"),
                            K.Caption("Draft · edited 2 minutes ago")),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 4,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Children =
                            {
                                K.IconBtn("eye", "Preview page"),
                                K.IconBtn("more-horizontal", "Page actions")
                            }
                        }
                    }
                },
                K.Divider(2),
                K.Stack(4,
                    K.Caption("Intro"),
                    K.Text("Northwind bills monthly in arrears. Plans are priced per seat, and annual billing "
                        + "includes two months free.", "arc-body-sm", 560)),
                K.Stack(4,
                    K.Caption("Refunds"),
                    pageTint),
                K.Stack(6,
                    K.Caption("Plans"),
                    plans),
                K.Stack(4,
                    K.Caption("Terms"),
                    K.Text("Annual plans renew on the purchase date. Downgrades take effect at the end of the "
                        + "current term, and unused seats are credited on the next invoice.", "arc-body-sm", 560))
            }
        };

        var panel = new Border
        {
            Background = K.Brush(ArcTokens.Surface),
            BorderBrush = K.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14, 12, 14, 12),
            Child = K.Stack(10,
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        K.Row(7,
                            K.Disc("sparkles", 22, true, 12),
                            K.Label("Assistant"),
                            K.Caption("Sonnet 5.5")),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Children = { K.IconBtn("panel-right", "Close assistant") }
                        }
                    }
                },
                K.RowWrap(6,
                    K.Chip("This page", icon: "check"),
                    K.Chip("Selection: Refunds", icon: "quote")),
                K.Text("The refund window is stricter than the plan terms. I can tighten this paragraph so both "
                    + "say fourteen days.", "arc-body-sm"),
                changeCard,
                undoPill,
                new Border
                {
                    Background = K.Brush(ArcTokens.SurfaceMuted),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(9, 5, 6, 5),
                    Child = composerRow
                },
                status)
        };

        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,360") };
        columns.Children.Add(K.Panel(page, 16, 18));
        Grid.SetColumn(panel, 2);
        columns.Children.Add(panel);

        return K.Surface(columns, new Thickness(20, 18, 20, 18));
    }
}
