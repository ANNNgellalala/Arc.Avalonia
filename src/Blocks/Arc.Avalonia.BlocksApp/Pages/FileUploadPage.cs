using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;
using Path = System.IO.Path;

namespace Arc.Avalonia.BlocksApp.Pages;

/// <summary>
/// File upload: a drop target with type and size constraints, per-file progress, retry and remove, and a
/// live status line. Drops and picks are accepted locally and the upload is simulated with a timer; the
/// reference runs your onUpload with an abort signal.
/// </summary>
public sealed class FileUploadPage : ArcItemPage
{
    public override string ItemSlug => "file-upload";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Uploading files with per-file progress, retry and remove.",
        "Attachments that must be validated by type and size before upload."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use file-dropzone when you only need to pick files without upload state."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The drop target is a labelled button with its constraints described, and every row's retry and remove buttons name the file.",
        "Enter or Space on the drop target opens the picker, Tab moves through the rows, and each added, rejected or removed file is announced politely."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The drop target label swaps to Drop to add files while dragging, rows open their height, and one spring drives the bar and its percentage.",
        "Reduced motion jumps progress and replaces height and blur swaps with fades."
    };

    public override string? ImplementationNotes =>
        "Files are never uploaded: adding a file records its name and size and a timer walks the progress. Real OS drops and the system file picker both feed the same list. Removing a file stops its simulated progress but no network request is aborted.";

    private enum UploadState
    {
        Uploading,
        Done,
        Failed
    }

    private sealed class Upload
    {
        public required string Name { get; init; }
        public required long Size { get; init; }
        public UploadState State { get; set; }
        public double Progress { get; set; }
        public string? Error { get; set; }
        public string? Type { get; init; }
    }

    private static string Size(long bytes) => bytes >= 1024 * 1024
        ? (bytes / (1024.0 * 1024.0)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB"
        : Math.Max(1, bytes / 1024) + " KB";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        const long maxSize = 10 * 1024 * 1024;
        var status = AppKit.Status();
        var uploads = new List<Upload>
        {
            new() { Name = "brand-guide.pdf", Size = 3_400_000, State = UploadState.Done, Progress = 100, Type = "pdf" },
            new() { Name = "hero-shot.png", Size = 5_100_000, State = UploadState.Uploading, Progress = 64, Type = "png" },
            new() { Name = "contract-final-final.docx", Size = 14_600_000, State = UploadState.Failed, Progress = 0, Error = "Larger than the 10 MB limit", Type = "docx" }
        };
        var dragOver = false;
        var counter = 0;

        var listHost = new ContentControl();
        var dropHost = new ContentControl();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };

        void BuildList()
        {
            var panel = new StackPanel { Spacing = 6 };
            foreach (var upload in uploads.ToList())
            {
                var entry = upload;
                var icon = entry.Type switch
                {
                    "png" or "jpg" => "image",
                    "pdf" => "file",
                    _ => "file"
                };
                var trailing = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    VerticalAlignment = VerticalAlignment.Center
                };

                if (entry.State == UploadState.Uploading)
                {
                    trailing.Children.Add(new TextBlock
                    {
                        Text = $"{entry.Progress:0}%",
                        Classes = { "arc-caption" },
                        VerticalAlignment = VerticalAlignment.Center,
                        Width = 34,
                        TextAlignment = TextAlignment.Right
                    });
                }
                else if (entry.State == UploadState.Done)
                {
                    trailing.Children.Add(AppKit.Row(5,
                        new ArcIcon { Icon = "check", Size = 13, Foreground = AppKit.Brush(ArcTokens.Success) },
                        AppKit.Caption("Uploaded")));
                }
                else
                {
                    trailing.Children.Add(AppKit.Btn("Retry", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
                    {
                        uploads.Remove(entry);
                        uploads.Insert(0, new Upload { Name = entry.Name, Size = 1_200_000, State = UploadState.Uploading, Progress = 8, Type = entry.Type });
                        Build();
                        StartProgress();
                        AppKit.Say(status, $"Retrying {entry.Name} at a smaller size.");
                    }, height: 26));
                }

                trailing.Children.Add(AppKit.IconBtn("x", $"Remove {entry.Name}", () =>
                {
                    uploads.Remove(entry);
                    Build();
                    AppKit.Say(status, $"{entry.Name} removed.");
                }, 26));

                var row = new Border
                {
                    Padding = new Thickness(10),
                    Background = AppKit.Brush(ArcTokens.SurfaceRaised),
                    BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Child = new StackPanel
                    {
                        Spacing = 7,
                        Children =
                        {
                            new Grid
                            {
                                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                                ColumnSpacing = 9,
                                Children =
                                {
                                    AppKit.Disc(icon, 30, 14),
                                    AppKit.Place(new StackPanel
                                    {
                                        Spacing = 1,
                                        Children =
                                        {
                                            new TextBlock
                                            {
                                                Text = entry.Name,
                                                Classes = { "arc-body-sm" },
                                                TextTrimming = TextTrimming.CharacterEllipsis,
                                                MaxWidth = 260,
                                                HorizontalAlignment = HorizontalAlignment.Left
                                            },
                                            entry.State == UploadState.Failed
                                                ? new TextBlock
                                                {
                                                    Text = $"{Size(entry.Size)} · {entry.Error}",
                                                    Classes = { "arc-caption" },
                                                    Foreground = AppKit.Brush(ArcTokens.Danger)
                                                }
                                                : AppKit.Caption($"{Size(entry.Size)} · {(entry.State == UploadState.Done ? "Complete" : "Uploading")}")
                                        }
                                    }, 1),
                                    AppKit.Place(trailing, 2)
                                }
                            },
                            entry.State == UploadState.Uploading
                                ? new Border
                                {
                                    Height = 3,
                                    CornerRadius = new CornerRadius(2),
                                    Background = AppKit.Brush(ArcTokens.ControlTrack),
                                    ClipToBounds = true,
                                    Child = new Grid
                                    {
                                        ColumnDefinitions = new ColumnDefinitions($"{Math.Clamp(entry.Progress / 100, 0, 1):0.##}*,{Math.Clamp(1 - entry.Progress / 100, 0, 1):0.##}*"),
                                        Children =
                                        {
                                            new Border
                                            {
                                                CornerRadius = new CornerRadius(2),
                                                Background = AppKit.Brush(ArcTokens.Accent)
                                            }
                                        }
                                    }
                                }
                                : new Border { Height = 0 }
                        }
                    }
                };

                panel.Children.Add(row);
            }

            if (uploads.Count == 0)
            {
                panel.Children.Add(new Border
                {
                    Padding = new Thickness(12, 18, 12, 18),
                    Child = AppKit.Empty("upload", "No files yet", "Drop a file above or browse from your device.")
                });
            }

            listHost.Content = panel;
        }

        void StartProgress()
        {
            if (timer.IsEnabled)
            {
                return;
            }

            timer.Tick += (_, _) =>
            {
                var uploading = uploads.FirstOrDefault(upload => upload.State == UploadState.Uploading);
                if (uploading is null)
                {
                    timer.Stop();
                    return;
                }

                uploading.Progress = Math.Min(100, uploading.Progress + 8);
                if (uploading.Progress >= 100)
                {
                    uploading.State = UploadState.Done;
                    AppKit.Say(status, $"{uploading.Name} uploaded. The request is simulated.");
                }

                BuildList();
            };
            timer.Start();
        }

        void Add(string name, long size, string type)
        {
            counter++;
            if (size > maxSize)
            {
                uploads.Insert(0, new Upload
                {
                    Name = name,
                    Size = size,
                    State = UploadState.Failed,
                    Error = "Larger than the 10 MB limit",
                    Type = type
                });
                Build();
                AppKit.Say(status, $"{name} rejected: larger than 10 MB.");
                return;
            }

            uploads.Insert(0, new Upload { Name = name, Size = size, State = UploadState.Uploading, Progress = 4, Type = type });
            Build();
            StartProgress();
            AppKit.Say(status, $"{name} added and uploading.");
        }

        void BuildDrop()
        {
            var dashed = new Rectangle
            {
                RadiusX = 14,
                RadiusY = 14,
                Stroke = AppKit.Brush(dragOver ? ArcTokens.Accent : ArcTokens.BorderStrong),
                StrokeThickness = 1,
                StrokeDashArray = new AvaloniaList<double> { 5, 4 },
                Opacity = 0.7
            };

            var inner = new StackPanel
            {
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    AppKit.Disc(dragOver ? "download" : "upload", 38, 18, dragOver),
                    new TextBlock
                    {
                        Text = dragOver ? "Drop to add files" : "Drop files here or browse",
                        Classes = { "arc-body-sm", "arc-medium" },
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    AppKit.Caption("PNG, JPG, PDF or DOCX up to 10 MB, five files at a time", 320),
                    AppKit.Btn("Browse files", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => Add($"screenshot-{counter + 1}.png", 2_400_000, "png"), icon: "image", height: 30)
                }
            };

            var drop = new Border
            {
                Height = 134,
                Background = AppKit.Tint(dragOver ? ArcTokens.Accent : ArcTokens.SurfaceMuted, dragOver ? 0.1 : 1),
                CornerRadius = new CornerRadius(14),
                ClipToBounds = true,
                Child = new Panel { Children = { dashed, inner } }
            };

            ToolTip.SetTip(drop, "Drop files to upload");
            drop.Cursor = new Cursor(StandardCursorType.Hand);
            drop.Focusable = true;
            drop.KeyDown += (_, args) =>
            {
                if (args.Key is Key.Enter or Key.Space)
                {
                    Add($"screenshot-{counter + 1}.png", 2_400_000, "png");
                    args.Handled = true;
                }
            };

            DragDrop.SetAllowDrop(drop, true);
            drop.AddHandler(DragDrop.DragEnterEvent, (_, args) =>
            {
                dragOver = true;
                args.DragEffects = DragDropEffects.Copy;
                BuildDrop();
            });
            drop.AddHandler(DragDrop.DragOverEvent, (_, args) =>
            {
                args.DragEffects = DragDropEffects.Copy;
            });
            drop.AddHandler(DragDrop.DragLeaveEvent, (_, _) =>
            {
                dragOver = false;
                BuildDrop();
            });
            drop.AddHandler(DragDrop.DropEvent, async (_, args) =>
            {
                dragOver = false;
                BuildDrop();
                var picked = args.DataTransfer.TryGetFiles();
                var files = picked?.OfType<IStorageFile>().ToList() ?? new List<IStorageFile>();
                if (files.Count == 0)
                {
                    return;
                }

                foreach (var file in files.Take(5))
                {
                    var size = 1_000_000L;
                    try
                    {
                        await using var stream = await file.OpenReadAsync();
                        size = stream.CanSeek ? stream.Length : size;
                    }
                    catch
                    {
                        // A file that cannot be read keeps the sample size; the row still shows the name.
                    }

                    Add(Path.GetFileName(file.Name), size, Path.GetExtension(file.Name).Trim('.').ToLowerInvariant());
                }
            });

            dropHost.Content = drop;
        }

        void Build()
        {
            BuildDrop();
            BuildList();
        }

        Build();

        var content = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 2,
                            Children =
                            {
                                new TextBlock { Text = "Attachments", Classes = { "arc-body", "arc-medium" } },
                                AppKit.Caption("Three of five files used · 8.2 MB of 50 MB")
                            }
                        },
                        AppKit.Place(AppKit.Pill("1 of 3 complete", ArcBadgeTone.Success), 1)
                    }
                },
                dropHost,
                listHost,
                status
            }
        };

        return AppKit.Screen(new Border { Padding = new Thickness(18, 14, 18, 12), Child = content }, 474);
    }
}
