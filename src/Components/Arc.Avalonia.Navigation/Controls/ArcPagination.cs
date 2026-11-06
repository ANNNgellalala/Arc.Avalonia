using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Controls.Metadata;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Navigation.Controls;

/// <summary>
/// Previous and next arrows around a five page window, with the current page in the accent and ellipses
/// where the window skips. Fully controlled: keep the page in your own state and react to the change.
/// </summary>
[TemplatePart("PART_Buttons", typeof(StackPanel))]
public sealed class ArcPagination : TemplatedControl
{
    public static readonly StyledProperty<int> PageProperty =
        AvaloniaProperty.Register<ArcPagination, int>(nameof(Page), 1);

    public static readonly StyledProperty<int> PageCountProperty =
        AvaloniaProperty.Register<ArcPagination, int>(nameof(PageCount));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcPagination, string?>(nameof(Label), "Pagination");

    private StackPanel? _buttons;

    static ArcPagination()
    {
        PageProperty.Changed.AddClassHandler<ArcPagination>((pager, _) => pager.Build());
        PageCountProperty.Changed.AddClassHandler<ArcPagination>((pager, _) => pager.Build());
    }

    public ArcPagination()
    {
        LayoutUpdated += (_, _) => Build();
    }

    /// <summary>Raised with the requested page.</summary>
    public event EventHandler<int>? PageChanged;

    /// <summary>Bubbled copy of <see cref="PageChanged"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcPaginationPageChangedEventArgs> PageChangedEvent =
        RoutedEvent.Register<ArcPagination, ArcPaginationPageChangedEventArgs>("PageChanged", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="PageChanged"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcPaginationPageChangedEventArgs>? PageChangedRouted
    {
        add => AddHandler(PageChangedEvent, value);
        remove => RemoveHandler(PageChangedEvent, value);
    }

    /// <summary>Current page, 1-based; clamped to the range.</summary>
    public int Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    public int PageCount
    {
        get => GetValue(PageCountProperty);
        set => SetValue(PageCountProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _buttons = e.NameScope.Find<StackPanel>("PART_Buttons");
        AutomationProperties.SetName(this, Label ?? "Pagination");
        Build();
    }

    private void Build()
    {
        if (_buttons is null)
        {
            return;
        }

        var count = Math.Max(0, PageCount);
        var page = count == 0 ? 0 : Math.Clamp(Page, 1, count);
        var signature = $"{page}:{count}";
        if (signature == _built && _buttons.Children.Count > 0)
        {
            return;
        }

        _built = signature;
        _buttons.Children.Clear();

        if (count == 0)
        {
            return;
        }

        _buttons.Children.Add(Arrow("chevron-left", "Previous page", page > 1, () => Request(page - 1)));

        foreach (var token in Tokens(page, count))
        {
            if (token is int number)
            {
                var button = new ArcButton
                {
                    Variant = ArcButtonVariant.Ghost,
                    Size = ArcButtonSize.Sm,
                    Compact = true,
                    Width = 36,
                    Height = 36,
                    MinHeight = 36,
                };
                button.CornerRadius = ArcActionVisuals.Radius(ArcTokens.RadiusControl);
                button.Classes.Add("page-button");
                button.Content = ArcActionVisuals.InheritText(number.ToString(), button, medium: true);
                button.Classes.Set("current", number == page);
                AutomationProperties.SetName(button, number == page ? $"Page {number}, current page" : $"Page {number}");
                if (number == page)
                {
                    AutomationProperties.SetHelpText(button, "Current page");
                }

                button.Click += (_, _) => Request(number);
                _buttons.Children.Add(button);
            }
            else
            {
                var gap = new TextBlock
                {
                    Text = "…",
                    Classes = { "arc-caption" },
                    Width = 28,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                _buttons.Children.Add(gap);
            }
        }

        _buttons.Children.Add(Arrow("chevron-right", "Next page", page < count, () => Request(page + 1)));
    }

    private string _built = string.Empty;

    private ArcButton Arrow(string icon, string name, bool enabled, Action action)
    {
        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Compact = true,
            Width = 36,
            Height = 36,
            MinHeight = 36,
            IsEnabled = enabled,
            Content = new ArcIcon { Icon = icon, Size = 16 }
        };
        button.CornerRadius = ArcActionVisuals.Radius(ArcTokens.RadiusControl);
        button.Classes.Add("page-arrow");
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => action();
        return button;
    }

    private void Request(int page)
    {
        var count = Math.Max(1, PageCount);
        var target = Math.Clamp(page, 1, count);
        if (target == Page)
        {
            return;
        }

        SetCurrentValue(PageProperty, target);
                var pageChangedArgs = new ArcPaginationPageChangedEventArgs(PageChangedEvent) { PageIndex = target };
        PageChanged?.Invoke(this, target);
        RaiseEvent(pageChangedArgs);
    }

    private static IEnumerable<object> Tokens(int page, int count)
    {
        if (count <= 7)
        {
            for (var i = 1; i <= count; i++)
            {
                yield return i;
            }

            yield break;
        }

        yield return 1;
        var start = Math.Max(2, page - 1);
        var end = Math.Min(count - 1, page + 1);
        if (start > 2)
        {
            yield return "gap";
        }

        for (var i = start; i <= end; i++)
        {
            yield return i;
        }

        if (end < count - 1)
        {
            yield return "gap";
        }

        yield return count;
    }
}