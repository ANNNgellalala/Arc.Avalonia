using System;
using System.Collections.Generic;
using System.Linq;

namespace Arc.Avalonia.Services;

/// <summary>
/// The opt-in service registry for Arc components. It is a small thread-safe map keyed by the registered
/// interface type, deliberately not a DI container: an application calls <see cref="Register{T}"/> once at
/// startup (or on an <see cref="ArcWindow"/>'s <see cref="ArcWindow.Services"/>), components call
/// <see cref="TryGet{T}"/>, and anything unregistered keeps its historical behaviour.
/// </summary>
public static class ArcServiceProvider
{
    private static readonly object Gate = new();
    private static readonly Dictionary<Type, object> Services = new();

    /// <summary>Registers (or replaces) the implementation for <typeparamref name="T"/>.</summary>
    public static void Register<T>(T service)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        lock (Gate)
        {
            Services[typeof(T)] = service;
        }
    }

    /// <summary>Registers an instance under its concrete runtime type.</summary>
    public static void Register(object service)
    {
        ArgumentNullException.ThrowIfNull(service);
        lock (Gate)
        {
            Services[service.GetType()] = service;
        }
    }

    /// <summary>Looks up the registered implementation for <typeparamref name="T"/>.</summary>
    public static bool TryGet<T>(out T? service)
        where T : class
    {
        lock (Gate)
        {
            if (Services.TryGetValue(typeof(T), out var found) && found is T typed)
            {
                service = typed;
                return true;
            }
        }

        service = null;
        return false;
    }

    /// <summary>Returns the registered implementation, or null when nothing is registered.</summary>
    public static T? Get<T>()
        where T : class => TryGet<T>(out var service) ? service : null;

    /// <summary>True when an implementation for <typeparamref name="T"/> is registered.</summary>
    public static bool IsRegistered<T>()
        where T : class
    {
        lock (Gate)
        {
            return Services.ContainsKey(typeof(T));
        }
    }

    /// <summary>Removes the registration for <typeparamref name="T"/>; returns whether one existed.</summary>
    public static bool Unregister<T>()
        where T : class
    {
        lock (Gate)
        {
            return Services.Remove(typeof(T));
        }
    }

    /// <summary>Clears every registration. Used by tests and by hosts that need a clean scope.</summary>
    public static void Reset()
    {
        lock (Gate)
        {
            Services.Clear();
        }
    }

    /// <summary>A snapshot of the currently registered service types (diagnostics).</summary>
    public static IReadOnlyList<Type> RegisteredTypes
    {
        get
        {
            lock (Gate)
            {
                return Services.Keys.OrderBy(type => type.FullName, StringComparer.Ordinal).ToList();
            }
        }
    }
}
