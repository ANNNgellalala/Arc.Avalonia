using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery;

/// <summary>
/// Generates the API section from the control type itself: public properties and events, their types and
/// default values. Nothing is hand-listed, so the table cannot drift from the implementation.
/// </summary>
public static class DocApiTable
{
    private static readonly HashSet<string> Noise = new(StringComparer.Ordinal)
    {
        "Dispatcher", "TemplatedParent", "Parent", "LogicalParent", "VisualParent", "NameScope",
        "StyleKey", "Classes", "Styles", "Resources", "DataContext", "Clip", "Effect", "Item"
    };

    public static Control Build(Type type, ArcDocApi api)
    {
        var exclude = new HashSet<string>(api.Exclude, StringComparer.Ordinal);
        var properties = Properties(type).Where(property => !exclude.Contains(property.Name)).ToList();
        var events = Events(type).Where(evt => !exclude.Contains(evt.Name)).ToList();

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = $"公开 API 自 {type.Name} 反射生成：{properties.Count} 个属性、{events.Count} 个事件。",
            Classes = { "arc-caption" }
        });

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("220,150,120,*") };
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        AddHeader(grid, 0, "属性", "类型", "默认值", "说明");

        var row = 1;
        foreach (var property in properties)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var defaultValue = DefaultOf(type, property.Name);
            AddCell(grid, row, 0, property.Name, "arc-mono", mono: true);
            AddCell(grid, row, 1, TypeName(property.PropertyType), "arc-caption");
            AddCell(grid, row, 2, defaultValue, "arc-caption");
            var description = api.Descriptions.TryGetValue(property.Name, out var text) ? text : "—";
            AddCell(grid, row, 3, description, "arc-body-sm");
            row++;
        }

        if (events.Count > 0)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddHeader(grid, row++, "事件", "委托", "", "说明");
            foreach (var evt in events)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                AddCell(grid, row, 0, evt.Name, "arc-mono", mono: true);
                AddCell(grid, row, 1, TypeName(evt.EventHandlerType), "arc-caption");
                AddCell(grid, row, 2, string.Empty, "arc-caption");
                var description = api.Descriptions.TryGetValue(evt.Name, out var text) ? text : "—";
                AddCell(grid, row, 3, description, "arc-body-sm");
                row++;
            }
        }

        panel.Children.Add(new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = DemoUi.Brush(ArcTokens.Border),
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            Child = grid
        });

        return panel;
    }

    private static IEnumerable<PropertyInfo> Properties(Type type)
    {
        // Declared properties of the control and its Arc/Button/Content/Templated bases, stopping before
        // Avalonia's Control (which contributes dozens of layout properties nobody sets per component).
        for (var current = type; current is not null && current != typeof(Control); current = current.BaseType)
        {
            foreach (var property in current.GetProperties(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (property.GetIndexParameters().Length == 0
                    && property.GetGetMethod() is not null
                    && !Noise.Contains(property.Name))
                {
                    yield return property;
                }
            }
        }
    }

    private static IEnumerable<EventInfo> Events(Type type)
    {
        for (var current = type; current is not null && current != typeof(Control); current = current.BaseType)
        {
            foreach (var evt in current.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                yield return evt;
            }
        }
    }

    private static string DefaultOf(Type type, string propertyName)
    {
        foreach (var property in AvaloniaPropertyRegistry.Instance.GetRegistered(type))
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var value = property.GetMetadata(type).GetType().GetProperty("DefaultValue")?.GetValue(property.GetMetadata(type));
                return value switch
                {
                    null => "null",
                    string text when text.Length == 0 => "\"\"",
                    string text => "\"" + text + "\"",
                    bool flag => flag ? "true" : "false",
                    IConvertible convertible => convertible.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _ => value.ToString() ?? "—"
                };
            }
            catch
            {
                return "—";
            }
        }

        return "—";
    }

    private static string TypeName(Type? type)
    {
        if (type is null)
        {
            return "—";
        }

        if (type == typeof(string)) return "string";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(int)) return "int";
        if (type == typeof(double)) return "double";
        if (type == typeof(void)) return "void";

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            return TypeName(underlying) + "?";
        }

        if (type.IsEnum)
        {
            return type.Name;
        }

        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name;
        var tick = name.IndexOf('`');
        if (tick > 0)
        {
            name = name[..tick];
        }

        return name + "<" + string.Join(", ", type.GetGenericArguments().Select(TypeName)) + ">";
    }

    private static void AddHeader(Grid grid, int row, string first, string second, string third, string fourth)
    {
        AddCell(grid, row, 0, first, "arc-caption", header: true);
        AddCell(grid, row, 1, second, "arc-caption", header: true);
        AddCell(grid, row, 2, third, "arc-caption", header: true);
        AddCell(grid, row, 3, fourth, "arc-caption", header: true);
    }

    private static void AddCell(
        Grid grid,
        int row,
        int column,
        string text,
        string classes,
        bool mono = false,
        bool header = false)
    {
        var cell = new TextBlock
        {
            Text = text,
            Classes = { classes },
            TextWrapping = header ? TextWrapping.NoWrap : TextWrapping.Wrap,
            Margin = new Thickness(12, 9),
            FontWeight = header ? FontWeight.Medium : FontWeight.Normal
        };
        if (mono)
        {
            cell.Classes.Add("arc-mono");
            cell.FontSize = 12.5;
        }

        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }
}
