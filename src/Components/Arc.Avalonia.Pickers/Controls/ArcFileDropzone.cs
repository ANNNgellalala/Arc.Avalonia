using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Services;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>Upload state of a dropzone row.</summary>
public enum ArcFileDropzoneStatus
{
    Ready,
    Uploading,
    Failed
}

/// <summary>One row in the file list; pass earlier uploads through <c>DefaultItems</c>.</summary>
public sealed class ArcFileDropzoneItem
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required long Size { get; init; }

    public ArcFileDropzoneStatus Status { get; init; }

    /// <summary>Upload percent, used when the status is uploading.</summary>
    public double Progress { get; init; }

    /// <summary>Failure reason shown on the row.</summary>
    public string? Error { get; init; }

    /// <summary>The picked file, when the row came from the visitor.</summary>
    public IStorageFile? File { get; init; }
}

/// <summary>
/// Arc's file dropzone: a generous dashed target that accepts drops or opens the system picker, plus a
/// list of files with their size and a remove action.
/// </summary>
public class ArcFileDropzone : TemplatedControl
{
    public static readonly StyledProperty<string?> AcceptProperty =
        AvaloniaProperty.Register<ArcFileDropzone, string?>(nameof(Accept));

    public static readonly StyledProperty<bool> MultipleProperty =
        AvaloniaProperty.Register<ArcFileDropzone, bool>(nameof(Multiple), true);

    public static readonly StyledProperty<int> MaxFilesProperty =
        AvaloniaProperty.Register<ArcFileDropzone, int>(nameof(MaxFiles), 5);

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcFileDropzone, string?>(nameof(Label), "Add files");

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ArcFileDropzone, string?>(nameof(Description), "Drop files here or choose from your device");

    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<ArcFileDropzone, string?>(nameof(Note));

    public static readonly StyledProperty<string?> DropLabelProperty =
        AvaloniaProperty.Register<ArcFileDropzone, string?>(nameof(DropLabel));

    public static readonly StyledProperty<long?> MaxSizeProperty =
        AvaloniaProperty.Register<ArcFileDropzone, long?>(nameof(MaxSize));

    public static readonly StyledProperty<IEnumerable<ArcFileDropzoneItem>?> DefaultItemsProperty =
        AvaloniaProperty.Register<ArcFileDropzone, IEnumerable<ArcFileDropzoneItem>?>(nameof(DefaultItems));

    public static readonly StyledProperty<bool> IsDragOverProperty =
        AvaloniaProperty.Register<ArcFileDropzone, bool>(nameof(IsDragOver));

    private readonly List<ArcFileDropzoneItem> _items = new();
    private Button? _drop;
    private TextBlock? _title;
    private TextBlock? _hint;
    private TextBlock? _note;
    private TextBlock? _error;
    private StackPanel? _list;

    static ArcFileDropzone()
    {
        IsDragOverProperty.Changed.AddClassHandler<ArcFileDropzone>((zone, _) => zone.UpdateDropState());
        LabelProperty.Changed.AddClassHandler<ArcFileDropzone>((zone, _) => zone.UpdateDropState());
        DropLabelProperty.Changed.AddClassHandler<ArcFileDropzone>((zone, _) => zone.UpdateDropState());
        DescriptionProperty.Changed.AddClassHandler<ArcFileDropzone>((zone, _) => zone.UpdateDropState());
        NoteProperty.Changed.AddClassHandler<ArcFileDropzone>((zone, _) => zone.UpdateDropState());
        MaxFilesProperty.Changed.AddClassHandler<ArcFileDropzone>((zone, _) => zone.UpdateDropState());
        AcceptProperty.Changed.AddClassHandler<ArcFileDropzone>((zone, _) => zone.UpdateDropState());
    }

    /// <summary>Accepted types as in the native accept attribute, for example ".pdf,image/*".</summary>
    public string? Accept
    {
        get => GetValue(AcceptProperty);
        set => SetValue(AcceptProperty, value);
    }

    /// <summary>Allow several files. On by default.</summary>
    public bool Multiple
    {
        get => GetValue(MultipleProperty);
        set => SetValue(MultipleProperty, value);
    }

    /// <summary>Most files the list holds. Defaults to five.</summary>
    public int MaxFiles
    {
        get => GetValue(MaxFilesProperty);
        set => SetValue(MaxFilesProperty, value);
    }

    /// <summary>Main prompt on the drop target.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Secondary prompt under the label.</summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Replaces the small line under the description, which otherwise lists the limits.</summary>
    public string? Note
    {
        get => GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    /// <summary>Label shown while files hover over the target.</summary>
    public string? DropLabel
    {
        get => GetValue(DropLabelProperty);
        set => SetValue(DropLabelProperty, value);
    }

    /// <summary>Largest accepted file in bytes; larger files fail with a size reason.</summary>
    public long? MaxSize
    {
        get => GetValue(MaxSizeProperty);
        set => SetValue(MaxSizeProperty, value);
    }

    /// <summary>Rows present at mount, such as earlier uploads.</summary>
    public IEnumerable<ArcFileDropzoneItem>? DefaultItems
    {
        get => GetValue(DefaultItemsProperty);
        set => SetValue(DefaultItemsProperty, value);
    }

    /// <summary>True while files hover the target. Also driven by real drag events.</summary>
    public bool IsDragOver
    {
        get => GetValue(IsDragOverProperty);
        set => SetValue(IsDragOverProperty, value);
    }

    /// <summary>Current rows, in display order.</summary>
    public IReadOnlyList<ArcFileDropzoneItem> Items => _items;

    /// <summary>Raised whenever the file list changes.</summary>
    public event EventHandler<IReadOnlyList<IStorageFile>>? FilesChanged;

    /// <summary>Raised with the full row list whenever it changes.</summary>
    public event EventHandler<IReadOnlyList<ArcFileDropzoneItem>>? ItemsChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_drop is not null)
        {
            _drop.Click -= OnBrowseClick;
            DragDrop.SetAllowDrop(_drop, false);
            _drop.RemoveHandler(DragDrop.DragEnterEvent, OnDragEnter);
            _drop.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
            _drop.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            _drop.RemoveHandler(DragDrop.DropEvent, OnDrop);
        }

        _drop = e.NameScope.Find<Button>("PART_Drop");
        _title = e.NameScope.Find<TextBlock>("PART_Title");
        _hint = e.NameScope.Find<TextBlock>("PART_Hint");
        _note = e.NameScope.Find<TextBlock>("PART_Note");
        _error = e.NameScope.Find<TextBlock>("PART_Error");
        _list = e.NameScope.Find<StackPanel>("PART_List");

        if (_drop is not null)
        {
            _drop.Click += OnBrowseClick;
            AutomationProperties.SetName(_drop, "Add files");
            DragDrop.SetAllowDrop(_drop, true);
            _drop.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
            _drop.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            _drop.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            _drop.AddHandler(DragDrop.DropEvent, OnDrop);
        }

        if (_items.Count == 0 && DefaultItems is { } seed)
        {
            _items.AddRange(seed);
            RenderList();
        }

        UpdateDropState();
    }

    /// <summary>Formats bytes the way the row shows them, for example "1.2 MB".</summary>
    public static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        string[] units = { "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = -1;
        do
        {
            value /= 1024;
            unit++;
        }
        while (value >= 1024 && unit < units.Length - 1);

        return $"{value:0.#} {units[unit]}";
    }

    private void UpdateDropState()
    {
        if (_title is not null)
        {
            _title.Text = IsDragOver ? DropLabel ?? Label ?? "Add files" : Label ?? "Add files";
        }

        if (_hint is not null)
        {
            _hint.Text = Description ?? string.Empty;
            _hint.IsVisible = !string.IsNullOrWhiteSpace(Description);
        }

        if (_note is not null)
        {
            _note.Text = Note ?? BuildNote();
            _note.IsVisible = !string.IsNullOrWhiteSpace(_note.Text);
        }

        if (_drop is not null)
        {
            _drop.Classes.Set("dragover", IsDragOver);
            AutomationProperties.SetName(_drop, Label ?? "Add files");
        }
    }

    private string BuildNote()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Accept))
        {
            parts.Add(Accept!.Replace(",", ", ").Replace("/*", " files"));
        }

        if (MaxSize is { } max)
        {
            parts.Add($"Up to {FormatFileSize(max)} each");
        }

        if (MaxFiles > 0)
        {
            parts.Add(MaxFiles == 1 ? "1 file" : $"Up to {MaxFiles} files");
        }

        return string.Join(" \u00b7 ", parts);
    }

    private void ShowError(string? message)
    {
        if (_error is null)
        {
            return;
        }

        _error.Text = message;
        _error.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var top = TopLevel.GetTopLevel(this);
        var options = new FilePickerOpenOptions
        {
            Title = Label ?? "Add files",
            AllowMultiple = Multiple,
            FileTypeFilter = BuildFileTypes()
        };

        // An application may register IFilePickerService (a headless picker or a custom file portal);
        // without one the control keeps using the platform storage provider directly.
        if (ArcServiceProvider.TryGet<IFilePickerService>(out var picker) && picker is not null)
        {
            var picked = await picker.OpenFileAsync(options, top);
            if (picked.Count > 0)
            {
                await AddFilesAsync(picked);
            }

            return;
        }

        if (top?.StorageProvider is null)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(options);

        if (files.Count > 0)
        {
            await AddFilesAsync(files);
        }
    }

    private List<FilePickerFileType>? BuildFileTypes()
    {
        if (string.IsNullOrWhiteSpace(Accept))
        {
            return null;
        }

        var patterns = new List<string>();
        foreach (var token in Accept!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            patterns.Add(token switch
            {
                "image/*" => "*.png",
                "application/pdf" => "*.pdf",
                _ when token.StartsWith('.') => "*" + token,
                _ => token
            });

            if (token == "image/*")
            {
                patterns.AddRange(new[] { "*.jpg", "*.jpeg", "*.webp", "*.gif" });
            }
        }

        return new List<FilePickerFileType> { new("Accepted files") { Patterns = patterns } };
    }

    private async Task AddFilesAsync(IReadOnlyList<IStorageItem> files)
    {
        var added = new List<IStorageFile>();
        var rejected = 0;
        var overLimit = 0;
        var tooLarge = 0;

        foreach (var item in files.OfType<IStorageFile>().Take(Multiple ? int.MaxValue : 1))
        {
            if (MaxFiles > 0 && _items.Count >= MaxFiles)
            {
                overLimit++;
                continue;
            }

            if (!MatchesAccept(item.Name))
            {
                rejected++;
                continue;
            }

            long size = 0;
            try
            {
                var properties = await item.GetBasicPropertiesAsync();
                size = (long)(properties.Size ?? 0);
            }
            catch
            {
                size = 0;
            }

            var oversized = MaxSize is { } limit && size > limit;
            if (oversized)
            {
                tooLarge++;
            }

            _items.Add(new ArcFileDropzoneItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = item.Name,
                Size = size,
                File = item,
                Status = oversized ? ArcFileDropzoneStatus.Failed : ArcFileDropzoneStatus.Ready,
                Error = oversized ? $"Larger than {FormatFileSize(MaxSize!.Value)}" : null
            });
            added.Add(item);
        }

        if (tooLarge > 0)
        {
            ShowError(tooLarge == 1 ? "One file is too large." : $"{tooLarge} files are too large.");
        }
        else if (rejected > 0)
        {
            ShowError(rejected == 1 ? "That file type is not accepted." : $"{rejected} files were not accepted.");
        }
        else if (overLimit > 0)
        {
            ShowError(MaxFiles == 1 ? "The list holds one file." : $"The list holds up to {MaxFiles} files.");
        }
        else
        {
            ShowError(null);
        }

        RenderList();
        UpdateDropState();

        if (added.Count > 0)
        {
            var all = _items.Where(item => item.File is not null).Select(item => item.File!).ToArray();
            FilesChanged?.Invoke(this, all);
        }

        ItemsChanged?.Invoke(this, _items.ToArray());
    }

    private bool MatchesAccept(string name)
    {
        if (string.IsNullOrWhiteSpace(Accept))
        {
            return true;
        }

        var extension = Path.GetExtension(name).ToLowerInvariant();
        foreach (var token in Accept!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (token == "*" || token == "*/*")
            {
                return true;
            }

            if (token.StartsWith('.') && extension == token.ToLowerInvariant())
            {
                return true;
            }

            if (token.EndsWith("/*", StringComparison.Ordinal))
            {
                var prefix = token[..^2].ToLowerInvariant();
                if (extension.Length > 0 && KnownExtensions(prefix).Contains(extension))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string[] KnownExtensions(string family) => family switch
    {
        "image" => new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".svg" },
        "audio" => new[] { ".mp3", ".wav", ".ogg", ".m4a" },
        "video" => new[] { ".mp4", ".webm", ".mov" },
        "text" => new[] { ".txt", ".md", ".csv", ".json" },
        _ => Array.Empty<string>()
    };

    private void RenderList()
    {
        if (_list is null)
        {
            return;
        }

        _list.Children.Clear();
        foreach (var item in _items)
        {
            _list.Children.Add(BuildRow(item));
        }
    }

    private Control BuildRow(ArcFileDropzoneItem item)
    {
        var isImage = item.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                      || item.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                      || item.Name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                      || item.Name.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);

        var icon = new ArcIcon
        {
            Icon = isImage ? "image" : "file",
            Size = 16,
            Foreground = ArcResources.Brush(ArcTokens.TextSecondary),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var iconBox = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(10),
            Background = ArcResources.Brush(ArcTokens.SurfaceMuted),
            Child = icon,
            VerticalAlignment = VerticalAlignment.Center
        };

        var title = new TextBlock
        {
            Text = item.Name,
            FontSize = ArcResources.Double(ArcTokens.TextSm),
            Foreground = ArcResources.Brush(ArcTokens.Foreground),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var meta = new TextBlock
        {
            Text = item.Status switch
            {
                ArcFileDropzoneStatus.Uploading => $"{FormatFileSize(item.Size)} \u00b7 Uploading {item.Progress:0}%",
                ArcFileDropzoneStatus.Failed => item.Error ?? "Upload failed",
                _ => FormatFileSize(item.Size)
            },
            FontSize = ArcResources.Double(ArcTokens.TextXs),
            Foreground = item.Status == ArcFileDropzoneStatus.Failed
                ? ArcResources.Brush(ArcTokens.Danger)
                : ArcResources.Brush(ArcTokens.TextMuted)
        };

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(title);
        text.Children.Add(meta);

        if (item.Status == ArcFileDropzoneStatus.Uploading)
        {
            var track = new Border
            {
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = ArcResources.Brush(ArcTokens.Border),
                Margin = new Thickness(0, 6, 0, 0),
                ClipToBounds = true
            };

            var progress = Math.Clamp(item.Progress, 0, 100);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(Math.Max(progress, 0.01), GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(Math.Max(100 - progress, 0.01), GridUnitType.Star));
            var fill = new Border
            {
                CornerRadius = new CornerRadius(2),
                Background = ArcResources.Brush(ArcTokens.Accent)
            };
            grid.Children.Add(fill);
            track.Child = grid;
            text.Children.Add(track);
        }

        var remove = new Button
        {
            Theme = this.TryFindResource("ArcPickerIconTheme", out var theme) ? theme as ControlTheme : null,
            Content = new ArcIcon { Icon = "x", Size = 14 },
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(remove, $"Remove {item.Name}");
        remove.Click += (_, e) =>
        {
            e.Handled = true;
            RemoveItem(item);
        };
        remove.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Delete or Key.Back)
            {
                RemoveItem(item);
                e.Handled = true;
            }
        };

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetColumn(iconBox, 0);
        Grid.SetColumn(text, 1);
        Grid.SetColumn(remove, 2);
        text.Margin = new Thickness(12, 0, 12, 0);

        row.Children.Add(iconBox);
        row.Children.Add(text);
        row.Children.Add(remove);

        var shell = new Border
        {
            Background = ArcResources.Brush(ArcTokens.Surface),
            BorderBrush = ArcResources.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(12),
            Child = row
        };
        AutomationProperties.SetName(shell, $"{item.Name}, {FormatFileSize(item.Size)}");
        return shell;
    }

    private void RemoveItem(ArcFileDropzoneItem item)
    {
        _items.Remove(item);
        RenderList();
        UpdateDropState();
        ItemsChanged?.Invoke(this, _items.ToArray());
        FilesChanged?.Invoke(this, _items.Where(row => row.File is not null).Select(row => row.File!).ToArray());
    }

    private void OnDragEnter(object? sender, DragEventArgs e) => HandleDrag(e);

    private void OnDragOver(object? sender, DragEventArgs e) => HandleDrag(e);

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        SetCurrentValue(IsDragOverProperty, false);
        e.Handled = true;
    }

    private void HandleDrag(DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is { Length: > 0 })
        {
            e.DragEffects = DragDropEffects.Copy;
            SetCurrentValue(IsDragOverProperty, true);
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        SetCurrentValue(IsDragOverProperty, false);
        var files = e.DataTransfer.TryGetFiles();
        e.Handled = true;
        if (files is { Length: > 0 })
        {
            await AddFilesAsync(files);
        }
    }
}
