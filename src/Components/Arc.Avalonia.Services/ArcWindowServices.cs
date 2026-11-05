using System;

namespace Arc.Avalonia.Services;

/// <summary>
/// The registration point exposed by <see cref="ArcWindow"/>: a thin, typed facade over
/// <see cref="ArcServiceProvider"/> so a window can swap a service without touching global statics directly.
/// </summary>
public sealed class ArcWindowServices
{
    /// <summary>Registers (or replaces) the implementation for <typeparamref name="T"/>.</summary>
    public void Register<T>(T service)
        where T : class => ArcServiceProvider.Register(service);

    /// <summary>Looks up the current implementation for <typeparamref name="T"/>.</summary>
    public bool TryGet<T>(out T? service)
        where T : class => ArcServiceProvider.TryGet(out service);

    /// <summary>Returns the current implementation for <typeparamref name="T"/>, or null.</summary>
    public T? Get<T>()
        where T : class => ArcServiceProvider.Get<T>();

    /// <summary>True when <typeparamref name="T"/> has an implementation.</summary>
    public bool IsRegistered<T>()
        where T : class => ArcServiceProvider.IsRegistered<T>();

    /// <summary>Removes the implementation for <typeparamref name="T"/>.</summary>
    public bool Unregister<T>()
        where T : class => ArcServiceProvider.Unregister<T>();

    /// <summary>Registers a default implementation only when nothing is registered yet.</summary>
    public void EnsureRegistered<T>(Func<T> factory)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (!ArcServiceProvider.IsRegistered<T>())
        {
            ArcServiceProvider.Register(factory());
        }
    }
}
