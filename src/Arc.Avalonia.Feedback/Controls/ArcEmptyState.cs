using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

/// <summary>
/// A useful next step when there is nothing to show: a quiet illustration slot, a title, one or
/// two sentences of explanation, and at most two actions.
/// </summary>
public class ArcEmptyState : TemplatedControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ArcEmptyState, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<ArcEmptyState, string>(nameof(Description), string.Empty);

    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<ArcEmptyState, string?>(nameof(Icon), "folder");

    public static readonly StyledProperty<object?> ActionProperty =
        AvaloniaProperty.Register<ArcEmptyState, object?>(nameof(Action));

    public static readonly StyledProperty<object?> SecondaryActionProperty =
        AvaloniaProperty.Register<ArcEmptyState, object?>(nameof(SecondaryAction));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcEmptyState, string?>(nameof(Label));

    private Panel? _host;

    static ArcEmptyState()
    {
        TitleProperty.Changed.AddClassHandler<ArcEmptyState>((state, _) => state.Rebuild());
        DescriptionProperty.Changed.AddClassHandler<ArcEmptyState>((state, _) => state.Rebuild());
        IconProperty.Changed.AddClassHandler<ArcEmptyState>((state, _) => state.Rebuild());
        ActionProperty.Changed.AddClassHandler<ArcEmptyState>((state, _) => state.Rebuild());
        SecondaryActionProperty.Changed.AddClassHandler<ArcEmptyState>((state, _) => state.Rebuild());
        LabelProperty.Changed.AddClassHandler<ArcEmptyState>((state, _) => state.UpdateAutomation());
    }

    /// <summary>Headline.</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>One or two sentences on why it is empty and what to do.</summary>
    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Lucide icon name for the illustration slot.</summary>
    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Primary call to action, usually an ArcButton.</summary>
    public object? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    /// <summary>Secondary action, quieter than the primary one.</summary>
    public object? SecondaryAction
    {
        get => GetValue(SecondaryActionProperty);
        set => SetValue(SecondaryActionProperty, value);
    }

    /// <summary>Accessible name for the section region.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        Rebuild();
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Label) ? Title : Label);
    }

    private void Rebuild()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();

        var root = new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 420
        };

        var illustration = new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(28),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = FeedbackUi.Icon(Icon ?? "folder", 24, ArcTokens.TextSecondary)
        };
        illustration.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
        root.Children.Add(illustration);

        var title = FeedbackUi.Text(Title, "arc-heading");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.TextAlignment = TextAlignment.Center;
        title.TextWrapping = TextWrapping.Wrap;
        title.Margin = new Thickness(0, 6, 0, 0);
        root.Children.Add(title);

        if (!string.IsNullOrWhiteSpace(Description))
        {
            var description = FeedbackUi.Text(Description, "arc-body-sm", "arc-secondary");
            description.HorizontalAlignment = HorizontalAlignment.Center;
            description.TextAlignment = TextAlignment.Center;
            description.TextWrapping = TextWrapping.Wrap;
            description.MaxWidth = 320;
            root.Children.Add(description);
        }

        if (Action is not null || SecondaryAction is not null)
        {
            var actions = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };

            if (Action is not null)
            {
                actions.Children.Add(Slot(Action, 0));
            }

            if (SecondaryAction is not null)
            {
                actions.Children.Add(Slot(SecondaryAction, 8));
            }

            root.Children.Add(actions);
        }

        _host.Children.Add(root);
        UpdateAutomation();
    }

    private static Control Slot(object content, double left)
    {
        var presenter = new ContentControl { Content = content, VerticalAlignment = VerticalAlignment.Center };
        presenter.Margin = new Thickness(left, 0, 0, 0);
        return presenter;
    }
}
