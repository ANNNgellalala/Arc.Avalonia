using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Arc.Avalonia.Demo;

/// <summary>One document that could not be loaded, reported by <c>--audit</c> instead of failing silently.</summary>
public sealed record ArcDocLoadError(string Source, string Message);

/// <summary>
/// Loads every embedded <c>Docs/&lt;slug&gt;.json</c> document that ships with an Arc.Avalonia* assembly.
/// Documents travel with the project that owns the component, so adding a component never requires editing
/// a central registry. Results are cached; call <see cref="Discover(bool)"/> with refresh to re-read.
/// </summary>
public static class ArcDocStore
{
    private static readonly object Gate = new();
    private static Dictionary<string, ArcDoc>? _docs;
    private static List<ArcDocLoadError>? _errors;
    private static List<string>? _sources;

    /// <summary>Assembly name prefixes scanned for embedded documents.</summary>
    private const string AssemblyPrefix = "Arc.Avalonia";

    private const string ResourceMarker = ".Docs.";

    /// <summary>Every loaded document, keyed by catalog slug (ordinal).</summary>
    public static IReadOnlyDictionary<string, ArcDoc> All
    {
        get
        {
            Ensure();
            return _docs!;
        }
    }

    /// <summary>Documents that failed to parse, with their resource name.</summary>
    public static IReadOnlyList<ArcDocLoadError> Errors
    {
        get
        {
            Ensure();
            return _errors!;
        }
    }

    /// <summary>Assembly names that contributed at least one document (diagnostics).</summary>
    public static IReadOnlyList<string> Sources
    {
        get
        {
            Ensure();
            return _sources!;
        }
    }

    public static ArcDoc? Get(string? slug) =>
        !string.IsNullOrWhiteSpace(slug) && All.TryGetValue(slug, out var doc) ? doc : null;

    public static int Count => All.Count;

    /// <summary>Discovers documents, optionally re-reading resources after a rebuild.</summary>
    public static void Discover(bool refresh = false)
    {
        if (refresh)
        {
            lock (Gate)
            {
                _docs = null;
                _errors = null;
                _sources = null;
            }
        }

        Ensure();
    }

    private static void Ensure()
    {
        if (_docs is not null)
        {
            return;
        }

        lock (Gate)
        {
            if (_docs is not null)
            {
                return;
            }

            var docs = new Dictionary<string, ArcDoc>(StringComparer.Ordinal);
            var errors = new List<ArcDocLoadError>();
            var sources = new List<string>();
            foreach (var assembly in Assemblies())
            {
                string[] names;
                try
                {
                    names = assembly.GetManifestResourceNames();
                }
                catch
                {
                    continue;
                }

                var contributed = false;
                foreach (var name in names)
                {
                    if (!name.Contains(ResourceMarker, StringComparison.Ordinal)
                        || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string json;
                    try
                    {
                        using var stream = assembly.GetManifestResourceStream(name);
                        if (stream is null)
                        {
                            errors.Add(new ArcDocLoadError(name, "资源读取失败"));
                            continue;
                        }

                        using var reader = new StreamReader(stream);
                        json = reader.ReadToEnd();
                    }
                    catch (Exception exception)
                    {
                        errors.Add(new ArcDocLoadError(name, exception.Message));
                        continue;
                    }

                    var doc = ArcDoc.Parse(json, name, out var error);
                    if (doc is null)
                    {
                        errors.Add(new ArcDocLoadError(name, error ?? "未知解析错误"));
                        continue;
                    }

                    if (!string.Equals(doc.Slug, SlugOf(name), StringComparison.Ordinal))
                    {
                        errors.Add(new ArcDocLoadError(
                            name,
                            $"slug \"{doc.Slug}\" 与文件名 \"{SlugOf(name)}\" 不一致"));
                        continue;
                    }

                    if (!docs.TryAdd(doc.Slug, doc))
                    {
                        errors.Add(new ArcDocLoadError(
                            name,
                            $"slug \"{doc.Slug}\" 被多个项目重复定义（已保留 {docs[doc.Slug].Source}）"));
                        continue;
                    }

                    contributed = true;
                }

                if (contributed)
                {
                    sources.Add(assembly.GetName().Name ?? nameof(assembly));
                }
            }

            _docs = docs;
            _errors = errors;
            _sources = sources;
        }
    }

    private static string SlugOf(string resourceName)
    {
        var file = resourceName[(resourceName.LastIndexOf(ResourceMarker, StringComparison.Ordinal) + ResourceMarker.Length)..];
        return file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? file[..^".json".Length]
            : file;
    }

    /// <summary>
    /// The same discovery contract as <see cref="ArcPageRegistry"/>: assemblies next to the gallery are
    /// loaded so their embedded documents are visible even when no page of theirs was touched yet.
    /// </summary>
    private static IEnumerable<Assembly> Assemblies()
    {
        var directory = AppContext.BaseDirectory;
        foreach (var path in Directory.GetFiles(directory, AssemblyPrefix + "*.dll"))
        {
            try
            {
                Assembly.LoadFrom(path);
            }
            catch
            {
                // Resource-only or native assembly: skip.
            }
        }
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
            if (name is not null && name.StartsWith(AssemblyPrefix, StringComparison.Ordinal))
            {
                yield return assembly;
            }
        }
    }
}

