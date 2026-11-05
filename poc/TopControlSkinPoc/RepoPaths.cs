using System;
using System.IO;

namespace TopControlSkinPoc;

/// <summary>Locates the repository root so screenshots land in docs/images/poc regardless of CWD.</summary>
public static class RepoPaths
{
    private static readonly Lazy<string> RootLazy = new(FindRoot);

    public static string Root => RootLazy.Value;

    public static string Poc => Path.Combine(Root, "poc");

    public static string DocsImagesPoc => Path.Combine(Root, "docs", "images", "poc");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ArcUI.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"ArcUI.slnx not found above {AppContext.BaseDirectory}");
    }
}
