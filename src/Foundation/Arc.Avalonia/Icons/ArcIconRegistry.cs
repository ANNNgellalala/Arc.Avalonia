using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Arc.Avalonia.Icons;

/// <summary>
/// Runtime icon source plugged in through <see cref="ArcIconRegistry.Register(IArcIconProvider)"/>: lets an
/// application, host or design tool supply its own icon set (or a themed/generated subset) without
/// patching the built-in table.
/// </summary>
public interface IArcIconProvider
{
    /// <summary>Returns <c>true</c> and the Lucide-style path data when this provider owns <paramref name="name"/>.</summary>
    bool TryGetPath(string name, out string? pathData);
}

/// <summary>
/// Read-through registry for icon path data (M2-G3g). Lookup order:
/// runtime registrations (<see cref="Register(string, string)"/>, overriding built-ins) → registered
/// <see cref="IArcIconProvider"/>s in registration order → <see cref="ArcIconData.Paths"/>.
/// Everything here is thread-safe; <see cref="ArcIcon"/> keeps its own parsed-geometry cache and
/// invalidates it when registrations change.
/// </summary>
public static class ArcIconRegistry
{
    private static readonly ConcurrentDictionary<string, string> Registered = new(StringComparer.Ordinal);
    private static readonly object ProviderGate = new();
    private static IArcIconProvider[] _providers = Array.Empty<IArcIconProvider>();

    /// <summary>Registers or overrides a single icon path at runtime.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="pathData"/> is blank.</exception>
    public static void Register(string name, string pathData)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Icon name must not be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(pathData))
        {
            throw new ArgumentException($"Icon path data for '{name}' must not be empty.", nameof(pathData));
        }

        Registered[name] = pathData;
    }

    /// <summary>Removes a runtime registration. Returns <c>false</c> when the name was not registered.</summary>
    public static bool Unregister(string name) => Registered.TryRemove(name, out _);

    /// <summary>Registers a provider consulted after runtime registrations, before the built-in table.</summary>
    public static void Register(IArcIconProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        lock (ProviderGate)
        {
            var next = new IArcIconProvider[_providers.Length + 1];
            _providers.CopyTo(next, 0);
            next[^1] = provider;
            _providers = next;
        }
    }

    /// <summary>Removes a previously registered provider. Returns <c>false</c> when it was not registered.</summary>
    public static bool Unregister(IArcIconProvider provider)
    {
        lock (ProviderGate)
        {
            var index = Array.IndexOf(_providers, provider);
            if (index < 0)
            {
                return false;
            }

            var next = new IArcIconProvider[_providers.Length - 1];
            Array.Copy(_providers, 0, next, 0, index);
            Array.Copy(_providers, index + 1, next, index, _providers.Length - index - 1);
            _providers = next;
            return true;
        }
    }

    /// <summary>Removes every provider registration (used by tests and by hosts that rebuild their icon set).</summary>
    public static void ClearProviders()
    {
        lock (ProviderGate)
        {
            _providers = Array.Empty<IArcIconProvider>();
        }
    }

    /// <summary>Names registered at runtime, excluding built-ins and provider-owned names.</summary>
    public static IReadOnlyCollection<string> RegisteredNames => Registered.Keys.ToArray();

    /// <summary>Resolves a name through the full lookup chain.</summary>
    public static bool TryGetPath(string name, out string? pathData)
    {
        pathData = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (Registered.TryGetValue(name, out pathData))
        {
            return true;
        }

        IArcIconProvider[] providers;
        lock (ProviderGate)
        {
            providers = _providers;
        }

        foreach (var provider in providers)
        {
            if (provider.TryGetPath(name, out pathData) && !string.IsNullOrWhiteSpace(pathData))
            {
                return true;
            }
        }

        return ArcIconData.Paths.TryGetValue(name, out pathData);
    }
}
