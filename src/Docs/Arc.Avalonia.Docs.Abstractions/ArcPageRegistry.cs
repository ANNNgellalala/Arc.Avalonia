using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Arc.Avalonia.Demo;

/// <summary>Discovers every <see cref="ArcPage"/> in the Arc assemblies loaded next to the gallery.</summary>
public static class ArcPageRegistry
{
    private static IReadOnlyList<ArcPage>? _pages;

    public static IReadOnlyList<ArcPage> Discover(bool refresh = false)
    {
        if (_pages is not null && !refresh)
        {
            return _pages;
        }

        var directory = AppContext.BaseDirectory;
        foreach (var path in Directory.GetFiles(directory, "Arc.Avalonia*.dll"))
        {
            try
            {
                Assembly.LoadFrom(path);
            }
            catch
            {
                // A native or resource-only assembly: ignore.
            }
        }

        var pages = new List<ArcPage>();
        // Android (and other bundled hosts) have no loose .dll files on disk: the assemblies live inside
        // the APK/app bundle, so load every referenced Arc assembly explicitly before enumerating them.
        foreach (var reference in Assembly.GetEntryAssembly()?.GetReferencedAssemblies() ?? Array.Empty<AssemblyName>())
        {
            if (reference.Name is null || !reference.Name.StartsWith("Arc.Avalonia", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                _ = Assembly.Load(reference);
            }
            catch
            {
                // Already loaded or not available in this host; the loop below simply skips it.
            }
        }
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;
            if (name is null || !name.StartsWith("Arc.Avalonia", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var type in TypesOf(assembly))
            {
                if (type.IsAbstract || !typeof(ArcPage).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) is null)
                {
                    continue;
                }

                try
                {
                    pages.Add((ArcPage)Activator.CreateInstance(type)!);
                }
                catch
                {
                    // A page that needs dependencies is skipped rather than breaking the gallery.
                }
            }
        }

        _pages = pages.OrderBy(page => page.Order).ThenBy(page => page.Title, StringComparer.Ordinal).ToList();
        return _pages;
    }

    private static IEnumerable<Type> TypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }
}

