using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Glass.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Glass.Pages;

/// <summary>The Glass menu catalog page: a title menu over a photo, opened on the shared box morph.</summary>
public sealed class GlassMenuPage : ArcItemPage
{
    public override string ItemSlug => "glass-menu";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-open", "variant-trigger",
        "variant-groups", "variant-submenu", "variant-checked", "variant-danger", "variant-shortcut"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Actions for a photo, file, or card shown over an image or a colourful canvas.",
        "A title menu in an editor or viewer, where the title itself opens the actions."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Enter, Space or Down opens on the first row; Up opens on the last. Arrows wrap, Home and End jump.",
        "Enter chooses a row; Right opens its submenu and Left, or Escape in a submenu, goes back.",
        "Escape closes the menu and returns focus to the trigger; Tab closes it; letters jump to a matching row.",
        "The trigger and rows carry accessible names and keyboard focus draws a two pixel outline."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Opening is one spring morph from the trigger box to the panel box, and closing morphs back.",
        "Rows fade in a beat after the glass grows; the pill glides on the morph spring while the submenu slides in place.",
        "With reduced motion the panel, submenu and pill appear in their end state."
    };

    public override string? ImplementationNotes =>
        "No backdrop filter exists in Avalonia, so the glass is layered translucency from the surface token. The morph scales the trigger rect into the panel rect, the submenu slides in place, and photos are painted from tokens rather than photography. No Pro source was used.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var photo = BuildPhotoCard();

        var menu = new ArcGlassMenu
        {
            Align = ArcGlassMenuAlign.End,
            PanelWidth = 268,
            Label = "Photo actions",
            TriggerLabel = "Photo actions",
            Trigger = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 9,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    ArcGlass.GlyphTile("image", 24, accent: true, iconSize: 13),
                    new TextBlock
                    {
                        Text = "Pool house",
                        FontSize = 13,
                        FontWeight = FontWeight.Medium,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
                    }
                }
            }
        };

        menu.Groups = new List<ArcGlassMenuGroup>
        {
            new()
            {
                Label = "This photo",
                Items = new List<ArcGlassMenuItem>
                {
                    new() { Id = "duplicate", Label = "Duplicate", Icon = "copy", Shortcut = "Ctrl D" },
                    new()
                    {
                        Id = "move",
                        Label = "Move to",
                        Icon = "folder",
                        Items = new List<ArcGlassMenuItem>
                        {
                            new() { Id = "lisbon", Label = "Lisbon trip", Icon = "folder" },
                            new() { Id = "archive", Label = "Archive", Icon = "archive" },
                            new() { Id = "new-folder", Label = "New folder", Icon = "plus" }
                        }
                    },
                    new() { Id = "rename", Label = "Rename", Icon = "pencil", Shortcut = "F2" },
                    new() { Id = "download", Label = "Download", Icon = "download" }
                }
            },
            new()
            {
                Label = "Organise",
                Items = new List<ArcGlassMenuItem>
                {
                    new() { Id = "favorite", Label = "Add to favourites", Icon = "star", Checked = true, KeepOpen = true }
                }
            },
            new()
            {
                Items = new List<ArcGlassMenuItem>
                {
                    new() { Id = "delete", Label = "Delete", Icon = "trash-2", Danger = true, Shortcut = "Del" }
                }
            }
        };

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ClipToBounds = false
        };
        body.Children.Add(photo);

        menu.Margin = new Thickness(0, 4, 4, 0);
        menu.VerticalAlignment = VerticalAlignment.Top;
        menu.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(menu, 1);
        body.Children.Add(menu);

        menu.IsOpen = true;
        menu.ItemInvoked += (_, item) =>
        {
            AutomationProperties.SetName(menu, $"Photo actions, {item.Label} chosen");
        };

        var stage = GlassDemo.Stage(body, ArcGlassScene.Coast, 440, 22, seed: 3);
        return new StackPanel
        {
            Children =
            {
                stage,
                GlassDemo.Hint("The glass morphs out of the trigger. Down and Up move, Right opens Move to, Left goes back, Escape closes; type a letter to jump to a row.")
            }
        };
    }

    private static Control BuildPhotoCard()
    {
        var backdrop = new ArcGlassBackdrop { Scene = ArcGlassScene.Villa, Corner = 22, Seed = 4, Vignette = 0.4 };
        var caption = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(18, 0, 18, 16)
        };
        caption.Children.Add(new TextBlock
        {
            Text = "Pool house",
            FontSize = 18,
            FontWeight = FontWeight.Medium,
            Foreground = ArcGlass.Solid(ArcGlass.PhotoInk)
        });
        caption.Children.Add(new TextBlock
        {
            Text = "Edited 2 hours ago · 24 photos",
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.PhotoMuted)
        });

        var badge = new Border
        {
            CornerRadius = new CornerRadius(9999),
            Padding = new Thickness(9, 3),
            Background = ArcGlass.Solid(ArcColor.WithAlpha(ArcGlass.Color(ArcTokens.Background), 0.4)),
            BorderBrush = ArcGlass.Solid(ArcColor.WithAlpha(ArcGlass.Color(ArcTokens.Surface), 0.35)),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(14, 14, 0, 0),
            Child = new TextBlock
            {
                Text = "Edited",
                Classes = { "arc-caption" },
                Foreground = ArcGlass.Solid(ArcGlass.PhotoInk)
            }
        };

        var shade = new Border
        {
            Background = ArcGlass.PhotoShade(0.62),
            IsHitTestVisible = false
        };

        var layers = new Panel { Children = { backdrop, shade, badge, caption } };
        var card = new Border
        {
            Width = 356,
            Height = 292,
            CornerRadius = new CornerRadius(22),
            ClipToBounds = true,
            BorderBrush = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Surface, 0.30)),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = layers,
            BoxShadow = ArcGlass.Shadow(ArcTokens.ShadowRaised)
        };
        return card;
    }
}


