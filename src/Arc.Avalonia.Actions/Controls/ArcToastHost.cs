using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Controls;

/// <summary>
/// The stacked container for toasts: newest at the bottom right of the panel it is placed in. A page
/// drops one host into a panel and calls <see cref="Show"/>, and the host takes care of stacking and
/// removal. Every toast dismisses itself after its duration.
/// </summary>
public sealed class ArcToastHost : TemplatedControl
{
    private readonly List<ArcToast> _pending = new();
    private StackPanel? _stack;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _stack = e.NameScope.Find<StackPanel>("Stack");
        AutomationProperties.SetName(this, "Notifications");

        if (_stack is not null && _pending.Count > 0)
        {
            foreach (var toast in _pending)
            {
                _stack.Children.Add(toast);
            }

            _pending.Clear();
        }
    }

    /// <summary>Adds a toast to the stack and returns it.</summary>
    public ArcToast Show(string title, string? description = null, string? actionLabel = null, Action? action = null, int duration = 4500)
    {
        var toast = new ArcToast
        {
            Title = title,
            Description = description,
            ActionLabel = actionLabel,
            Action = action,
            Duration = duration
        };

        if (_stack is null)
        {
            _pending.Add(toast);
        }
        else
        {
            _stack.Children.Add(toast);
        }

        toast.Closed += (_, _) => _stack?.Children.Remove(toast);

        if (!ArcThemeManager.ReducedMotion)
        {
            toast.Opacity = 0;
            toast.RenderTransform = TransformOperations.Parse("translateY(16px) scale(0.96)");
            toast.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = ArcMotion.Standard,
                    Easing = ArcMotion.EaseEnter
                },
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = ArcMotion.SpringMorphDuration,
                    Easing = ArcMotion.SpringMorph
                }
            };
            Dispatcher.UIThread.Post(() =>
            {
                toast.Opacity = 1;
                toast.RenderTransform = TransformOperations.Parse("translateY(0px) scale(1)");
            }, DispatcherPriority.Render);
        }

        return toast;
    }

    /// <summary>Removes every toast immediately.</summary>
    public void Clear()
    {
        if (_stack is null)
        {
            return;
        }

        foreach (var child in _stack.Children)
        {
            if (child is ArcToast toast)
            {
                toast.Close();
            }
        }

        _stack.Children.Clear();
    }
}
