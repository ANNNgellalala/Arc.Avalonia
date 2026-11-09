using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Services;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery;

/// <summary>
/// Hosts <see cref="GalleryShell"/> in an <see cref="ArcWindow"/> and keeps the public surface the smoke and
/// screenshot harnesses use. The desktop window keeps its historical 1460x940 three-column default; the phone
/// window is 412x915 DIP, fixed-size and centred, and forces the compact shell from its first layout pass.
/// </summary>
public sealed class GalleryWindow : ArcWindow
{
    private readonly GalleryShell _shell;

    public GalleryWindow(string? initialSlug = null, bool mobile = false)
    {
        Title = "Arc for Avalonia · 中文文档";
        if (mobile)
        {
            Width = 412;
            Height = 915;
            MinWidth = 360;
            MinHeight = 600;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        else
        {
            Width = 1460;
            Height = 940;
            MinWidth = 1120;
            MinHeight = 700;
        }

        Background = DemoUi.Brush(ArcTokens.Background);
        _shell = new GalleryShell(initialSlug, mobile);
        PageHost.Content = _shell;
    }

    /// <summary>The hosted shell (exposed for harnesses and diagnostics).</summary>
    public GalleryShell Shell => _shell;

    /// <summary>True when the shell is in the compact (phone) presentation.</summary>
    public bool IsCompact => _shell.IsCompact;

    public string? CurrentSlug => _shell.CurrentSlug;

    public int DocumentCount => _shell.DocumentCount;

    public IReadOnlyList<ArcItemPage> ItemPages => _shell.ItemPages;

    public void RebuildSidebar() => _shell.RebuildSidebar();

    public void ShowSlug(string slug) => _shell.ShowSlug(slug);

    public void ShowPage(ArcPage page) => _shell.ShowPage(page);

    public void ShowHome() => _shell.ShowHome();

    public void Search(string text) => _shell.Search(text);

    public void RebuildAll() => _shell.RebuildAll();
}