using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Metadata;
using Avalonia.Layout;
using Arc.Avalonia.Controls;

namespace Arc.Avalonia.Navigation.Controls;

/// <summary>
/// A path of links where the last item is the current page. Chevron separators are decorative; the
/// current page is never a link.
/// </summary>
[TemplatePart("PART_Crumbs", typeof(WrapPanel))]
public sealed class ArcBreadcrumb : TemplatedControl
{
    public static readonly StyledProperty<IList<ArcBreadcrumbItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcBreadcrumb, IList<ArcBreadcrumbItem>?>(nameof(Items));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcBreadcrumb, string?>(nameof(Label), "Breadcrumb");

    private WrapPanel? _crumbs;

    static ArcBreadcrumb()
    {
        ItemsProperty.Changed.AddClassHandler<ArcBreadcrumb>((crumb, _) => crumb.Build());
        LabelProperty.Changed.AddClassHandler<ArcBreadcrumb>((crumb, _) =>
        {
            if (!string.IsNullOrWhiteSpace(crumb.Label))
            {
                AutomationProperties.SetName(crumb, crumb.Label!);
            }
        });
    }

    public IList<ArcBreadcrumbItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Accessible name for the navigation landmark.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _crumbs = e.NameScope.Find<WrapPanel>("PART_Crumbs");
        AutomationProperties.SetName(this, Label ?? "Breadcrumb");
        Build();
    }

    private void Build()
    {
        if (_crumbs is null)
        {
            return;
        }

        _crumbs.Children.Clear();
        var items = Items?.ToList() ?? new List<ArcBreadcrumbItem>();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var current = i == items.Count - 1;

            if (i > 0)
            {
                var separator = new ArcIcon
                {
                    Icon = "chevron-right",
                    Size = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0)
                };
                separator.Classes.Add("crumb-separator");
                AutomationProperties.SetName(separator, string.Empty);
                _crumbs.Children.Add(separator);
            }

            if (current)
            {
                var label = new TextBlock
                {
                    Text = item.Label,
                    Classes = { "arc-label" },
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0)
                };
                label.Classes.Add("crumb-current");
                AutomationProperties.SetName(label, $"{item.Label}, current page");
                _crumbs.Children.Add(label);
            }
            else
            {
                var link = new ArcButton
                {
                    Variant = ArcButtonVariant.Ghost,
                    Size = ArcButtonSize.Sm,
                    MinHeight = 32,
                    Padding = new Thickness(6, 0),
                };
                link.Classes.Add("crumb-link");
                link.Content = ArcActionVisuals.InheritText(item.Label, link);
                ArcActionVisuals.SetName(link, item.Label);
                if (item.Action is not null)
                {
                    link.Click += (_, _) => item.Action();
                }

                _crumbs.Children.Add(link);
            }
        }
    }
}