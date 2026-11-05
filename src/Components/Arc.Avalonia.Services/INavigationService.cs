using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;

namespace Arc.Avalonia.Services;

/// <summary>Raised after a navigation request changed the current page.</summary>
public sealed class ArcNavigationChangedEventArgs : EventArgs
{
    public ArcNavigationChangedEventArgs(object? previous, object? current, IReadOnlyList<object> stack)
    {
        Previous = previous;
        Current = current;
        Stack = stack;
    }

    /// <summary>The page that was current before the change.</summary>
    public object? Previous { get; }

    /// <summary>The page that is current now.</summary>
    public object? Current { get; }

    /// <summary>A snapshot of the stack from oldest to newest.</summary>
    public IReadOnlyList<object> Stack { get; }
}

/// <summary>
/// A minimal page frame: a stack, push/replace/back and a <see cref="ContentControl"/> host. It is the
/// smallest thing a business page needs; applications that already own their shell can keep it and still
/// expose this interface to Arc components.
/// </summary>
public interface INavigationService
{
    /// <summary>The page currently shown by the host, if any.</summary>
    object? Current { get; }

    /// <summary>The stack from oldest to newest.</summary>
    IReadOnlyList<object> Stack { get; }

    /// <summary>Raised after the current page or the stack changed.</summary>
    event EventHandler<ArcNavigationChangedEventArgs>? Changed;

    /// <summary>Binds the service to a content host and shows the current page.</summary>
    void Attach(ContentControl host);

    /// <summary>Pushes a page on top of the stack and shows it.</summary>
    void Navigate(object page);

    /// <summary>Replaces the current page in place.</summary>
    void Replace(object page);

    /// <summary>Pops the current page; returns false when already at the root.</summary>
    bool GoBack();

    /// <summary>Clears the stack and empties the host.</summary>
    void Clear();
}

/// <summary>The default minimal <see cref="INavigationService"/> over a <see cref="ContentControl"/>.</summary>
public sealed class ArcNavigationService : INavigationService
{
    private readonly List<object> _stack = new();
    private ContentControl? _host;

    /// <inheritdoc />
    public object? Current => _stack.Count > 0 ? _stack[^1] : null;

    /// <inheritdoc />
    public IReadOnlyList<object> Stack => _stack.ToList();

    /// <inheritdoc />
    public event EventHandler<ArcNavigationChangedEventArgs>? Changed;

    /// <inheritdoc />
    public void Attach(ContentControl host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _host.Content = Current;
    }

    /// <inheritdoc />
    public void Navigate(object page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var previous = Current;
        _stack.Add(page);
        Apply(previous);
    }

    /// <inheritdoc />
    public void Replace(object page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var previous = Current;
        if (_stack.Count == 0)
        {
            _stack.Add(page);
        }
        else
        {
            _stack[^1] = page;
        }

        Apply(previous);
    }

    /// <inheritdoc />
    public bool GoBack()
    {
        if (_stack.Count <= 1)
        {
            return false;
        }

        var previous = Current;
        _stack.RemoveAt(_stack.Count - 1);
        Apply(previous);
        return true;
    }

    /// <inheritdoc />
    public void Clear()
    {
        var previous = Current;
        _stack.Clear();
        if (_host is not null)
        {
            _host.Content = null;
        }

        Changed?.Invoke(this, new ArcNavigationChangedEventArgs(previous, null, Array.Empty<object>()));
    }

    private void Apply(object? previous)
    {
        if (_host is not null)
        {
            _host.Content = Current;
        }

        Changed?.Invoke(this, new ArcNavigationChangedEventArgs(previous, Current, Stack));
    }
}
