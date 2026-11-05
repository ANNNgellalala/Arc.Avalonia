using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Glass.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Glass.Pages;

/// <summary>The Sheet stack catalog page: nested sheets with depth and a mode switch to dialogs.</summary>
public sealed class SheetStackPage : ArcItemPage
{
    public override string ItemSlug => "sheet-stack";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-sheet", "state-nested", "variant-toggle-rows", "variant-footer"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Mobile settings where each row opens a deeper page and back should feel spatial.",
        "Flows that should be a bottom sheet on phones and a centred dialog on desktop from one declaration."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Escape closes only the top sheet; Tab and Shift+Tab cycle focus inside it.",
        "Enter or Space runs the back, close and trigger buttons; opening a level moves focus into it.",
        "Each panel is named by its title, only the top level is interactive, and closing a level returns focus to what opened it."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "A new sheet rises while the one below scales back five percent and peeks above it; dialogs ease in at the centre instead.",
        "The top sheet follows a vertical drag with rubber banding upward and pops past a third of its height or a fling.",
        "Reduced motion removes the depth scale and the drag physics; sheets fade in and out."
    };

    public override string? ImplementationNotes =>
        "Avalonia has no shared layout depth animation, so every level's position and scale is one function of its depth and animates with transform transitions. Focus containment uses KeyboardNavigation cycling rather than a custom trap, and the page toggle switches the container width and mode together. No Pro source was used.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var sheets = new ArcSheetStack
        {
            Label = "Settings sheets",
            Mode = ArcSheetStackMode.Sheet,
            Breakpoint = 640,
            InitialStack = new List<string> { "settings", "notifications" },
            Sheets = new List<ArcSheetDef>
            {
                new()
                {
                    Id = "settings",
                    Title = "Settings",
                    Description = "How this workspace reaches you.",
                    Rows = new List<ArcSheetRow>
                    {
                        new() { Label = "Notifications", Detail = "Mentions, deploys and digest", Icon = "bell", TargetSheet = "notifications" },
                        new() { Label = "Quiet hours", Detail = "From 21:00 to 08:00", Icon = "moon", Toggle = true, ToggleDefault = true },
                        new() { Label = "Workspace", Detail = "Members and permissions", Icon = "briefcase", TargetSheet = "workspace" }
                    }
                },
                new()
                {
                    Id = "notifications",
                    Title = "Notifications",
                    Description = "Choose what reaches you while you work.",
                    FooterLabel = "Save",
                    Rows = new List<ArcSheetRow>
                    {
                        new() { Label = "Mentions", Detail = "Always delivered", Toggle = true, ToggleDefault = true },
                        new() { Label = "Deploys", Detail = "Production only", Toggle = true, ToggleDefault = false },
                        new() { Label = "Weekly digest", Detail = "Mondays at 09:00", Toggle = true, ToggleDefault = true }
                    }
                },
                new()
                {
                    Id = "workspace",
                    Title = "Workspace",
                    Description = "Four people, two guests.",
                    Rows = new List<ArcSheetRow>
                    {
                        new() { Label = "Invite people", Detail = "Send an email invite", Icon = "users" },
                        new() { Label = "Permissions", Detail = "Editors can publish", Icon = "lock" },
                        new() { Label = "Archive workspace", Detail = "Keeps the files", Icon = "archive" }
                    }
                }
            }
        };

        var frameWidth = 420d;
        sheets.Width = 420;
        sheets.Height = 430;

        var phone = new Border
        {
            Width = frameWidth,
            Height = 446,
            CornerRadius = new CornerRadius(30),
            Background = ArcGlass.Solid(ArcGlass.Color(ArcTokens.SurfaceMuted)),
            BorderBrush = ArcGlass.Solid(ArcGlass.Color(ArcTokens.BorderStrong)),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Padding = new Thickness(8),
            Child = sheets,
        };

        var pageContent = BuildPageContent();
        sheets.PageContent = pageContent;

        var phoneButton = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm,
            Content = "Phone",
            Height = 34,
            MinHeight = 34
        };
        var desktopButton = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Content = "Desktop",
            Height = 34,
            MinHeight = 34
        };
        desktopButton.Classes.Add("sg-plain");

        phoneButton.Click += (_, _) =>
        {
            phone.Width = 420;
            sheets.Width = 420;
            sheets.Mode = ArcSheetStackMode.Sheet;
            phoneButton.Variant = ArcButtonVariant.Primary;
            desktopButton.Variant = ArcButtonVariant.Ghost;
        };
        desktopButton.Click += (_, _) =>
        {
            phone.Width = 720;
            sheets.Width = 720;
            sheets.Mode = ArcSheetStackMode.Dialog;
            phoneButton.Variant = ArcButtonVariant.Ghost;
            desktopButton.Variant = ArcButtonVariant.Primary;
        };

        var toggle = new StackPanel
        {
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                new TextBlock
                {
                    Text = "Container",
                    Classes = { "arc-caption" },
                    Foreground = ArcGlass.Solid(ArcGlass.PhotoMuted)
                },
                phoneButton,
                desktopButton,
                new TextBlock
                {
                    Text = "Below 640px the\nsame stack becomes\ncentred dialogs.",
                    Classes = { "arc-caption" },
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 150,
                    Foreground = ArcGlass.Solid(ArcGlass.PhotoMuted),
                    Margin = new Thickness(0, 8, 0, 0)
                }
            }
        };

        var togglePanel = ArcGlass.Panel(22, new Thickness(16), floating: true, fillAlpha: 0.55);
        togglePanel.Child = toggle;
        togglePanel.VerticalAlignment = VerticalAlignment.Center;
        togglePanel.HorizontalAlignment = HorizontalAlignment.Left;

        var stageBody = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto"),
            ColumnSpacing = 40,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { togglePanel, phone }
        };
        Grid.SetColumn(phone, 1);

        var stage = GlassDemo.Stage(stageBody, ArcGlassScene.Garden, 470, 20, seed: 9);
        return new StackPanel
        {
            Children =
            {
                stage,
                GlassDemo.Hint("Two levels are open: drag the top sheet's header down to dismiss it, press Escape, or switch to Desktop to see the same stack as dialogs.")
            }
        };
    }

    private static Control BuildPageContent()
    {
        var stack = new StackPanel { Spacing = 10, Margin = new Thickness(20, 24, 20, 0) };
        stack.Children.Add(new TextBlock
        {
            Text = "Settings",
            FontSize = 22,
            FontWeight = FontWeight.Medium,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Jasmine's workspace",
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted))
        });

        foreach (var label in new[] { "Notifications", "Appearance", "Members", "Billing" })
        {
            var row = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Surface)),
                BorderBrush = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Border)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 12),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = label,
                            Classes = { "arc-body-sm" },
                            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
                        },
                        ArcGlass.Icon("chevron-right", 15)
                    }
                }
            };
            if (row.Child is Grid grid && grid.Children.Count > 1)
            {
                Grid.SetColumn(grid.Children[1], 1);
            }

            stack.Children.Add(row);
        }

        var page = new Grid { Children = { new ArcGlassBackdrop { Scene = ArcGlassScene.Slate, Corner = 20, Seed = 5, Vignette = 0.4 }, stack } };
        return page;
    }
}





