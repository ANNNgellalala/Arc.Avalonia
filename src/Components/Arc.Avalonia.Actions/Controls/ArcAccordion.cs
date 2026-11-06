using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Controls;

public enum ArcAccordionSize
{
    Md,
    Lg
}

/// <summary>
/// Questions and answers revealed in place: one row open at a time, height and opacity moving together
/// on the smooth spring with no overshoot, and the chevron turning as the row opens.
/// </summary>
[TemplatePart("PART_Rows", typeof(StackPanel))]
public sealed class ArcAccordion : TemplatedControl
{
    public static readonly StyledProperty<IList<ArcAccordionItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcAccordion, IList<ArcAccordionItem>?>(nameof(Items));

    public static readonly StyledProperty<int> DefaultOpenProperty =
        AvaloniaProperty.Register<ArcAccordion, int>(nameof(DefaultOpen));

    public static readonly StyledProperty<ArcAccordionSize> SizeProperty =
        AvaloniaProperty.Register<ArcAccordion, ArcAccordionSize>(nameof(Size));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcAccordion, string?>(nameof(Label));

    private readonly List<Row> _rows = new();
    private StackPanel? _host;
    private int _open;
    private bool _built;

    private sealed record Row(ArcButton Header, Border Body, ContentPresenter Presenter, ArcIcon Chevron);

    static ArcAccordion()
    {
        ItemsProperty.Changed.AddClassHandler<ArcAccordion>((accordion, _) => accordion.Build());
        DefaultOpenProperty.Changed.AddClassHandler<ArcAccordion>((accordion, _) => accordion.ResetOpen());
        SizeProperty.Changed.AddClassHandler<ArcAccordion>((accordion, _) => accordion.Build());
    }

    private double _measuredWidth;

    public ArcAccordion()
    {
        _open = -1;
        LayoutUpdated += (_, _) =>
        {
            if (_open >= 0 && Math.Abs(Bounds.Width - _measuredWidth) > 1)
            {
                ApplyOpen(_open, animate: false);
            }
        };
    }

    public IList<ArcAccordionItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Index of the row open at rest; -1 starts with every row closed.</summary>
    public int DefaultOpen
    {
        get => GetValue(DefaultOpenProperty);
        set => SetValue(DefaultOpenProperty, value);
    }

    public ArcAccordionSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<StackPanel>("PART_Rows");
        if (!string.IsNullOrWhiteSpace(Label))
        {
            AutomationProperties.SetName(this, Label!);
        }

        Build();
    }

    private void ResetOpen()
    {
        _open = DefaultOpen;
        _built = false;
        Build();
    }

    private void Build()
    {
        if (_host is null)
        {
            return;
        }

        var items = Items?.ToList() ?? new List<ArcAccordionItem>();
        var opening = _built ? _open : Math.Clamp(DefaultOpen, -1, items.Count - 1);

        _host.Children.Clear();
        _rows.Clear();

        for (var i = 0; i < items.Count; i++)
        {
            var index = i;
            var item = items[i];

            if (i > 0)
            {
                _host.Children.Add(new Border { Classes = { "accordion-divider" } });
            }

            var chevron = new ArcIcon
            {
                Icon = "chevron-down",
                Size = 18,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            chevron.Classes.Add("accordion-chevron");

            var headerContent = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var title = new TextBlock
            {
                Text = item.Title,
                Classes = { "arc-body" },
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            if (Size == ArcAccordionSize.Lg)
            {
                title.FontSize = 16;
                title.FontWeight = FontWeight.Medium;
            }

            Grid.SetColumn(title, 0);
            headerContent.Children.Add(title);
            Grid.SetColumn(chevron, 1);
            headerContent.Children.Add(chevron);

            var header = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Content = headerContent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(4, 0),
                MinHeight = Size == ArcAccordionSize.Lg ? 68 : 56
            };
            header.Classes.Add("accordion-header");
            ArcActionVisuals.SetName(header, item.Title);
            header.Click += (_, _) => Toggle(index);

            var presenter = new ContentPresenter
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Content = item.Content is string text
                    ? new TextBlock
                    {
                        Text = text,
                        Classes = { "arc-body-sm", "arc-secondary" },
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        MaxWidth = Size == ArcAccordionSize.Lg ? 620 : 640
                    }
                    : item.Content
            };

            var body = new Border
            {
                ClipToBounds = true,
                Height = 0,
                Opacity = 0,
                Padding = new Thickness(4, 0, 4, 18),
                Child = presenter
            };
            body.Classes.Add("accordion-body");
            if (!ArcThemeManager.ReducedMotion)
            {
                body.Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = HeightProperty,
                        Duration = ArcMotion.SpringSmoothDuration,
                        Easing = ArcMotion.SpringSmooth
                    },
                    new DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = ArcMotion.Standard,
                        Easing = ArcMotion.EaseStandard
                    }
                };
            }

            var row = new Row(header, body, presenter, chevron);
            _rows.Add(row);
            _host.Children.Add(header);
            _host.Children.Add(body);
        }

        _built = true;
        ApplyOpen(opening, animate: false);
    }

    private void Toggle(int index)
    {
        ApplyOpen(_open == index ? -1 : index, animate: true);
    }

    private void ApplyOpen(int index, bool animate)
    {
        _open = index;
        if (Bounds.Width > 0)
        {
            _measuredWidth = Bounds.Width;
        }
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var open = i == index;
            row.Header.Classes.Set("open", open);
            ArcActionVisuals.Rotate(row.Chevron, open);

            if (open)
            {
                row.Presenter.Measure(new Size(Math.Max(0, Bounds.Width - 16), double.PositiveInfinity));
                var height = row.Presenter.DesiredSize.Height + row.Body.Padding.Top + row.Body.Padding.Bottom;
                if (height <= 0)
                {
                    height = 80;
                }

                row.Body.Height = height;
                row.Body.Opacity = 1;
            }
            else
            {
                row.Body.Height = 0;
                row.Body.Opacity = 0;
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_rows.Count == 0 || e.Handled)
        {
            return;
        }

        var index = _rows.FindIndex(row => row.Header.IsFocused);
        if (index < 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                _rows[(index + 1) % _rows.Count].Header.Focus();
                e.Handled = true;
                break;
            case Key.Up:
                _rows[(index - 1 + _rows.Count) % _rows.Count].Header.Focus();
                e.Handled = true;
                break;
            case Key.Home:
                _rows[0].Header.Focus();
                e.Handled = true;
                break;
            case Key.End:
                _rows[^1].Header.Focus();
                e.Handled = true;
                break;
        }
    }
}