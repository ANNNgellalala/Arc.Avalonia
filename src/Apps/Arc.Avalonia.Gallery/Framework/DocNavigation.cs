using System;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Gallery;

/// <summary>
/// Navigation hook for content built inside a page (home and coverage page) that needs to open another
/// entry. The shell installs the handler once; direct page construction (screenshots, tests) leaves it null
/// so a page can render without a shell.
/// </summary>
public static class DocNavigation
{
    public static Action<string>? Navigate { get; set; }

    /// <summary>Opens the coverage page; installed by the shell.</summary>
    public static Action? OpenStatus { get; set; }

    public static void Go(string slug) => Navigate?.Invoke(slug);

    public static void Status() => OpenStatus?.Invoke();

    public static bool IsAvailable => Navigate is not null;
}
