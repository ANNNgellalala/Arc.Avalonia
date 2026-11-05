using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data.Pages;

/// <summary>
/// Exploded view: a live phone screen built from five layers that separate in depth as a live slider moves,
/// with labels that follow each layer, per-layer visibility and a detail panel for the selected layer.
/// An independent Avalonia implementation of Arc's public exploded-view block; no Pro source was used.
/// </summary>
public sealed class ExplodedViewPage : ArcItemPage
{
    public override string ItemSlug => "exploded-view";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "layout-exploded", "variant-layers", "variant-labels", "variant-separation", "variant-detail-panel"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A product or hardware page that explains what a screen is made of, layer by layer.",
        "Feature reveals where depth should read as structure: glass, interface, content, chassis."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a diagram or an annotated screenshot when the layers are not literally stacked.",
        "Use layout-morph when the cards themselves rearrange rather than separate."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The stage is one focusable group: arrows move between layers, Home and End jump to the ends.",
        "Each layer has its name and note in text, and the toggles say shown or hidden."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Separation follows the slider directly; the reference drives it from page scroll, which a gallery page cannot own.",
        "Reduced motion snaps the layers to the new separation with no easing."
    };

    public override string? ImplementationNotes =>
        "The reference is scroll driven and uses CSS 3D; this port drives separation from a local slider and skews flat parallelograms, so there is no perspective divide.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var layers = new List<ExplodedLayer>
        {
            new("Display", "6.7 inch OLED, 120 Hz, 1,600 nits peak", 0),
            new("Cover glass", "Ceramic shield, oleophobic coating", 1),
            new("Interface", "Navigation, status bar and the live sheet", 2),
            new("Content", "The screen being explained, layer by layer", 3),
            new("Chassis", "Titanium frame, antenna bands, USB-C", 1)
        };

        var separation = 0.55;
        var selected = 2;
        var stageHost = new ContentControl();
        var panelHost = new ContentControl();
        var chipsHost = new ContentControl();
        var sliderHost = new ContentControl();
        var status = DataKit.Status();
        var stage = new ArcExplodedLayers(layers, separation) { Height = 300 };
        stageHost.Content = stage;

        void RefreshPanel()
        {
            var layer = layers[stage.Selected];
            panelHost.Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    DataKit.Pill($"Layer {stage.Selected + 1} of {layers.Count}", ArcBadgeTone.Neutral),
                    DataKit.Text(layer.Name, "arc-subtitle"),
                    DataKit.Body(layer.Note),
                    DataKit.Divider(),
                    DataKit.Caption(layer.Visible ? "Shown in the stack." : "Hidden from the stack."),
                    DataKit.Link(layer.Visible ? "Hide this layer" : "Show this layer", () =>
                    {
                        layer.Visible = !layer.Visible;
                        stage.InvalidateVisual();
                        RefreshPanel();
                        RefreshChips();
                        DataKit.Say(status, $"{layer.Name} {(layer.Visible ? "shown" : "hidden")}.");
                    }, icon: layer.Visible ? "eye-off" : "eye"),
                    DataKit.Caption("Order runs from the display at the front to the chassis at the back.")
                }
            };
        }

        void RefreshChips()
        {
            var row = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var layer in layers)
            {
                var chip = DataKit.Chip(layer.Name, layer.Visible, () =>
                {
                    layer.Visible = !layer.Visible;
                    stage.InvalidateVisual();
                    RefreshChips();
                    RefreshPanel();
                    DataKit.Say(status, $"{layer.Name} {(layer.Visible ? "shown" : "hidden")}.");
                });
                chip.Margin = new Thickness(0, 0, 6, 6);
                row.Children.Add(chip);
            }

            chipsHost.Content = row;
        }

        stage.SelectionChanged += (_, index) =>
        {
            selected = index;
            RefreshPanel();
            DataKit.Say(status, $"{layers[index].Name} selected.");
        };

        RefreshPanel();
        RefreshChips();

        sliderHost.Content = DataKit.Slider("Separation", separation, 0, 1, 0.05, value =>
        {
            separation = value;
            stage.Separation = value;
        }, showValue: true);

        var header = DataKit.Header("Exploded phone", "Five layers · one screen · drag separation to separate them",
            DataKit.Pill("Exploded view", ArcBadgeTone.Accent));

        var stageCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                DataKit.Row(8, DataKit.Label("Layers"), DataKit.Caption("select a layer for its note")),
                stageHost,
                sliderHost,
                chipsHost
            }
        });

        var panelCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children = { DataKit.Label("Selected layer"), panelHost }
        }, width: 268);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,12,268"),
            Children = { stageCard, DataKit.AtColumn(panelCard, 2) }
        };

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { header, body, status }
        }, 16);
    }
}
