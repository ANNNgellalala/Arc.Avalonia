using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksData.Pages;

/// <summary>
/// Choropleth explorer: an equal-area tile map that shades every region by revenue, growth or penetration on
/// a stepped scale, with a country switch, a hoverable legend, a synced ranked list and a camera zoom. All
/// values are seeded sample data. An independent Avalonia implementation of Arc's public choropleth-explorer
/// block; no Pro source was used.
/// </summary>
public sealed class ChoroplethExplorerPage : ArcItemPage
{
    public override string ItemSlug => "choropleth-explorer";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A regional revenue, growth or penetration map where the ranking matters as much as the geography.",
        "Explorers where people switch country and metric while keeping the same colour key."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a table when exact figures matter; the tiles are a scanning aid, not a ledger.",
        "Use a bar chart for a handful of regions on one axis."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The map is one focusable group: arrows move the selection in rank order, plus and minus zoom, Escape clears.",
        "Every tile has a code and a value, and the ranked list and legend repeat the numbers as text."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Tiles and the ranked list are replaced when the country or metric changes; the reference rewashes the scale on a spring, while this port swaps it on one short fade."
    };

    public override string? ImplementationNotes =>
        "An equal-area tile cartogram, not a polygon projection: no shapefile is bundled, each country ships 4 to 9 regions, values are seeded, and zoom is a tile-size factor with no pan.";

    private sealed record Country(string Key, string Label, IReadOnlyList<MapRegion> Regions, double Scale);

    private static readonly Country[] Countries =
    {
        new("us", "United States", new[]
        {
            new MapRegion("PAC", "Pacific", 0, 1), new MapRegion("MTN", "Mountain", 1, 1),
            new MapRegion("WNC", "West North Central", 2, 1), new MapRegion("ENC", "East North Central", 3, 0),
            new MapRegion("NEN", "New England", 4, 0), new MapRegion("MAT", "Middle Atlantic", 4, 1),
            new MapRegion("SAT", "South Atlantic", 4, 2), new MapRegion("ESC", "East South Central", 3, 2),
            new MapRegion("WSC", "West South Central", 2, 2)
        }, 1.0),
        new("ca", "Canada", new[]
        {
            new MapRegion("BC", "British Columbia", 0, 1), new MapRegion("AB", "Alberta", 1, 1),
            new MapRegion("PR", "Prairies", 2, 1), new MapRegion("ON", "Ontario", 3, 2),
            new MapRegion("QC", "Quebec", 4, 1), new MapRegion("AT", "Atlantic", 4, 2)
        }, 0.42),
        new("br", "Brazil", new[]
        {
            new MapRegion("N", "North", 1, 0), new MapRegion("NE", "Northeast", 3, 0),
            new MapRegion("CO", "Center-West", 2, 1), new MapRegion("SE", "Southeast", 3, 2),
            new MapRegion("S", "South", 2, 3)
        }, 0.28),
        new("gb", "United Kingdom", new[]
        {
            new MapRegion("SCT", "Scotland", 0, 0), new MapRegion("NTH", "North", 1, 0),
            new MapRegion("MID", "Midlands", 0, 1), new MapRegion("STH", "South", 1, 1)
        }, 0.31),
        new("fr", "France", new[]
        {
            new MapRegion("IDF", "Île-de-France", 1, 0), new MapRegion("NOR", "North", 1, 1),
            new MapRegion("EST", "East", 2, 1), new MapRegion("OUA", "West", 0, 1),
            new MapRegion("SUD", "South", 1, 2)
        }, 0.26),
        new("de", "Germany", new[]
        {
            new MapRegion("N", "North", 1, 0), new MapRegion("W", "West", 0, 1),
            new MapRegion("C", "Central", 1, 1), new MapRegion("E", "East", 2, 1),
            new MapRegion("S", "South", 1, 2)
        }, 0.34),
        new("it", "Italy", new[]
        {
            new MapRegion("NOR", "North", 1, 0), new MapRegion("LOM", "Lombardy", 0, 1),
            new MapRegion("CEN", "Center", 1, 1), new MapRegion("SUD", "South", 2, 2),
            new MapRegion("ISO", "Islands", 1, 3)
        }, 0.21),
        new("au", "Australia", new[]
        {
            new MapRegion("WA", "Western Australia", 0, 1), new MapRegion("NT", "Northern Territory", 1, 0),
            new MapRegion("QLD", "Queensland", 2, 1), new MapRegion("NSW", "New South Wales", 2, 2),
            new MapRegion("VIC", "Victoria", 1, 2)
        }, 0.24)
    };

    private static readonly (string Key, string Label, string Unit)[] Metrics =
    {
        ("revenue", "Revenue", "revenue"),
        ("growth", "Growth", "growth"),
        ("penetration", "Penetration", "penetration")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var countryKey = "us";
        var metricKey = "revenue";
        var zoom = 1.0;
        var mapHost = new ContentControl();
        var listHost = new ContentControl();
        var legendHost = new ContentControl();
        var statsHost = new ContentControl();
        var status = DataKit.Status();
        var caption = DataKit.Caption(string.Empty);
        var selected = (string?)null;

        Country ActiveCountry() => Countries.First(item => item.Key == countryKey);

        string ActiveLabel() => Metrics.First(item => item.Key == metricKey).Label;

        Dictionary<string, double> Values(Country country)
        {
            var values = new Dictionary<string, double>();
            for (var index = 0; index < country.Regions.Count; index++)
            {
                var region = country.Regions[index];
                var random = DataKit.Random(country.Key.Length * 131 + region.Code.Length * 17 + metricKey.Length * 7 + index);
                var baseValue = 240_000 * country.Scale * (0.45 + random() * 1.4);
                values[region.Code] = metricKey switch
                {
                    "growth" => Math.Round(2.4 + random() * 26, 1),
                    "penetration" => Math.Round(8 + random() * 52, 1),
                    _ => Math.Round(baseValue / 1000) * 1000
                };
            }

            return values;
        }

        Func<double, string> Formatter() => metricKey switch
        {
            "growth" => value => DataKit.Signed(value, "%", 1),
            "penetration" => value => DataKit.Percent(value / 100, 1),
            _ => DataKit.MoneyCompact
        };

        void Refresh()
        {
            var country = ActiveCountry();
            var values = Values(country);
            var format = Formatter();
            var ranked = country.Regions
                .Select(region => (Region: region, Value: values[region.Code]))
                .OrderByDescending(item => item.Value)
                .ToList();
            var max = ranked.Max(item => item.Value);
            caption.Text = $"{country.Label} · {ranked.Count} regions · {ActiveLabel().ToLowerInvariant()}";

            var map = new ArcChoroplethMap(country.Regions, values, ActiveLabel(), format, zoom) { Height = 268 };
            map.SelectionChanged += (_, code) =>
            {
                selected = code;
                DataKit.Say(status, code is null
                    ? "Region selection cleared."
                    : $"{country.Regions.First(region => region.Code == code).Name} selected: {format(values[code])}.");
                RefreshList();
            };
            mapHost.Content = map;

            void RefreshList()
            {
                var rows = new StackPanel { Spacing = 2 };
                foreach (var item in ranked)
                {
                    var isSelected = selected == item.Region.Code;
                    var row = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("34,*,44,Auto"),
                        ColumnSpacing = 8,
                        Height = 30,
                        Children =
                        {
                            DataKit.Text(item.Region.Code, "arc-caption"),
                            DataKit.AtColumn(DataKit.Text(item.Region.Name, "arc-body-sm", wrap: TextWrapping.NoWrap), 1),
                            DataKit.AtColumn(new Grid
                            {
                                Height = 4,
                                VerticalAlignment = VerticalAlignment.Center,
                                ColumnDefinitions = new ColumnDefinitions($"{Math.Clamp(item.Value / Math.Max(1e-9, max), 0.03, 1):0.###}*,{1 - Math.Clamp(item.Value / Math.Max(1e-9, max), 0.03, 1):0.###}*"),
                                Children =
                                {
                                    new Border
                                    {
                                        Background = Ink.Alpha(DataKit.Brush(ArcTokens.Series1), 0.8),
                                        CornerRadius = new CornerRadius(2)
                                    }
                                }
                            }, 2),
                            DataKit.AtColumn(DataKit.Text(format(item.Value), "arc-body-sm arc-medium"), 3)
                        }
                    };
                    if (isSelected)
                    {
                        row.Children.Insert(0, DataKit.Dot(DataKit.Brush(ArcTokens.Accent), 5));
                    }

                    rows.Children.Add(row);
                }

                listHost.Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        DataKit.Label("Ranked regions"),
                        rows,
                        DataKit.Caption("Hover a tile for its value · arrows move the selection · + / − zoom")
                    }
                };
            }

            RefreshList();

            var total = values.Values.Sum();
            var leader = ranked[0];
            var median = ranked[ranked.Count / 2].Value;
            statsHost.Content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*"),
                Children =
                {
                    DataKit.Stat(Metrics.First(item => item.Key == metricKey).Label,
                        metricKey == "revenue" ? DataKit.MoneyCompact(total) : format(median),
                        metricKey == "revenue" ? "across all regions" : "median region"),
                    DataKit.AtColumn(DataKit.Stat("Leader", leader.Region.Name, format(leader.Value)), 2),
                    DataKit.AtColumn(DataKit.Stat("Regions", ranked.Count.ToString(), country.Label), 4)
                }
            };

            var scale = new StepScale(format, values.Values.Max());
            legendHost.Content = scale.Build();
        }

        var countrySelector = DataKit.Select(Countries.Select(item => (item.Key, item.Label)), countryKey, key =>
        {
            countryKey = key;
            selected = null;
            Refresh();
            DataKit.Say(status, $"Country switched to {ActiveCountry().Label}.");
        }, 176);

        var metricSelector = DataKit.Segmented(Metrics.Select(item => (item.Key, item.Label)), metricKey, key =>
        {
            metricKey = key;
            selected = null;
            Refresh();
            DataKit.Say(status, $"Metric switched to {ActiveLabel()}.");
        });

        Refresh();

        void SetZoom(double value)
        {
            zoom = Math.Clamp(value, 0.7, 2.2);
            if (mapHost.Content is ArcChoroplethMap map)
            {
                map.Zoom = zoom;
                map.InvalidateVisual();
            }

            DataKit.Say(status, $"Camera zoom {zoom:0.00}x.");
        }

        var zoomIn = DataKit.IconBtn("zoom-in", "Zoom into the map", () => SetZoom(zoom + 0.2), 26);
        var zoomOut = DataKit.IconBtn("zoom-out", "Zoom out of the map", () => SetZoom(zoom - 0.2), 26);

        var header = DataKit.Header("Revenue by region", "Equal-area tiles · one stepped scale · sample data",
            DataKit.Row(10, countrySelector, metricSelector));

        var mapCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                DataKit.Row(8, DataKit.Label("Map"), caption),
                mapHost,
                DataKit.Row(14, legendHost, zoomOut, zoomIn, DataKit.Caption("Camera zoom"))
            }
        });

        var listCard = DataKit.Card(listHost, width: 316);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,12,316"),
            Children = { mapCard, DataKit.AtColumn(listCard, 2) }
        };

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { header, statsHost, body, status }
        }, 16);
    }

    /// <summary>The four-step key: swatches plus the numeric range each step covers.</summary>
    private sealed class StepScale
    {
        private readonly Func<double, string> _format;
        private readonly double _max;

        public StepScale(Func<double, string> format, double max)
        {
            _format = format;
            _max = max;
        }

        public Control Build()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            for (var step = 0; step < 4; step++)
            {
                var from = _max * step / 4;
                row.Children.Add(DataKit.Row(4,
                    new Border
                    {
                        Width = 12,
                        Height = 12,
                        CornerRadius = new CornerRadius(3),
                        Background = Ink.Alpha(Ink.Series(0), 0.16 + step * 0.22)
                    },
                    DataKit.Caption(_format(from))));
            }

            return row;
        }
    }
}
