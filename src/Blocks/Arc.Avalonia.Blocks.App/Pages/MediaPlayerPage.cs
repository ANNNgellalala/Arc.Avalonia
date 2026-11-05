using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App.Pages;

/// <summary>
/// Media player: cover art, a seek bar you can scrub, transport controls, volume with a mute toggle and a
/// living playlist. Tracks are sample data and playback is a timer, so the position advances without audio.
/// </summary>
public sealed class MediaPlayerPage : ArcItemPage
{
    public override string ItemSlug => "media-player";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-playing", "variant-seek-bar", "variant-volume", "variant-playlist"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A small audio collection such as a podcast, sample pack or playlist.",
        "Pages that need a full player with seek, skip, volume and a track list."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use now-playing for a compact widget rather than a full player.",
        "Use voice-recorder when the audio is being recorded."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Transport and playlist rows are labelled with the track title, and the current track carries its state as text and a rail.",
        "Space toggles play from the focused player, Left and Right seek by five seconds, and the seek and volume tracks are focusable with arrow-key steps."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The selection rail glides to the current track, the cover crossfades and the title rises when the track changes, and playing bars pulse beside the rail.",
        "Reduced motion removes the glide, the swap and the bar animation."
    };

    public override string? ImplementationNotes =>
        "There is no audio element: a timer advances the position and the tracks are sample entries, so nothing plays. Seek and volume are drawn tracks with pointer and arrow-key handling rather than native range inputs.";

    private sealed record Track(string Title, string Artist, string Length, string Album);

    private static readonly Track[] Tracks =
    {
        new("Night drives", "Kite Season", "3:42", "Signals"),
        new("Harbour lights", "Ana Vale", "4:18", "Slow water"),
        new("Paper planes", "Kite Season", "2:56", "Signals")
    };

    private static double Seconds(string length)
    {
        var parts = length.Split(':');
        return int.Parse(parts[0], CultureInfo.InvariantCulture) * 60 + int.Parse(parts[1], CultureInfo.InvariantCulture);
    }

    private static string Clock(double seconds)
    {
        var whole = (int)Math.Floor(seconds);
        return $"{whole / 60}:{whole % 60:00}";
    }

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var index = 1;
        var position = 72.0;
        var playing = true;
        var muted = false;
        var volume = 0.7;
        var playingHost = new ContentControl();
        var seekHost = new ContentControl();
        var playlistHost = new ContentControl();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };

        void BuildSeek()
        {
            var track = Tracks[index];
            var total = Seconds(track.Length);
            var bar = new Border
            {
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = AppKit.Brush(ArcTokens.ControlTrack),
                ClipToBounds = true,
                Cursor = new Cursor(StandardCursorType.Hand),
                Focusable = true,
                Child = new Grid
                {
                    // Star columns carry the fraction, so the fill tracks the bar width without a measured pixel size.
                    ColumnDefinitions = new ColumnDefinitions($"{Math.Clamp(position / total, 0, 1):0.####}*,{Math.Clamp(1 - position / total, 0, 1):0.####}*"),
                    Children =
                    {
                        new Border
                        {
                            Background = AppKit.Brush(ArcTokens.Accent)
                        }
                    }
                }
            };

            bar.AddHandler(InputElement.PointerPressedEvent, (_, args) =>
            {
                var x = args.GetPosition(bar).X;
                position = Math.Clamp(x / Math.Max(1, bar.Bounds.Width), 0, 1) * total;
                BuildSeek();
                AppKit.Say(status, $"Seeked to {Clock(position)} of {track.Length}.");
            });
            bar.AddHandler(InputElement.PointerMovedEvent, (_, args) =>
            {
                if (args.GetCurrentPoint(bar).Properties.IsLeftButtonPressed)
                {
                    var x = args.GetPosition(bar).X;
                    position = Math.Clamp(x / Math.Max(1, bar.Bounds.Width), 0, 1) * total;
                    BuildSeek();
                }
            });
            bar.KeyDown += (_, args) =>
            {
                var step = 0.0;
                if (args.Key == Key.Left)
                {
                    step = -5;
                }
                else if (args.Key == Key.Right)
                {
                    step = 5;
                }

                if (step != 0)
                {
                    position = Math.Clamp(position + step, 0, total);
                    BuildSeek();
                    AppKit.Say(status, $"Position {Clock(position)}.");
                    args.Handled = true;
                }
            };

            seekHost.Content = new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    new Border { Height = 18, Child = bar },
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            AppKit.Caption(Clock(position)),
                            AppKit.Place(AppKit.Caption(track.Length), 1)
                        }
                    }
                }
            };
        }

        void BuildPlaying()
        {
            var track = Tracks[index];
            var bars = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            var heights = playing ? new[] { 6, 11, 8, 13, 7 } : new[] { 3, 3, 3, 3, 3 };
            foreach (var height in heights)
            {
                bars.Children.Add(new Border
                {
                    Width = 3,
                    Height = height,
                    CornerRadius = new CornerRadius(2),
                    Background = AppKit.Brush(playing ? ArcTokens.Accent : ArcTokens.TextMuted)
                });
            }

            playingHost.Content = AppKit.Row(8,
                bars,
                AppKit.Caption(playing ? "Playing" : "Paused"));
        }

        void BuildPlaylist()
        {
            var panel = new StackPanel { Spacing = 2 };
            panel.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Margin = new Thickness(2, 0, 2, 6),
                Children =
                {
                    new TextBlock { Text = "Up next", Classes = { "arc-label" } },
                    AppKit.Place(AppKit.Caption($"{Tracks.Length} tracks · 10:56"), 1)
                }
            });

            for (var i = 0; i < Tracks.Length; i++)
            {
                var track = Tracks[i];
                var current = i == index;
                var target = i;
                var row = new Border
                {
                    Padding = new Thickness(8, 7, 8, 7),
                    CornerRadius = new CornerRadius(9),
                    Background = current ? AppKit.Tint(ArcTokens.Accent, 0.1) : null,
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        ColumnSpacing = 9,
                        Children =
                        {
                            new Border
                            {
                                Width = 30,
                                Height = 30,
                                CornerRadius = new CornerRadius(8),
                                Background = AppKit.Wash(0.28),
                                Child = new ArcIcon
                                {
                                    Icon = current ? "music" : "headphones",
                                    Size = 14,
                                    Foreground = current ? AppKit.Brush(ArcTokens.Accent) : AppKit.Brush(ArcTokens.TextMuted),
                                    HorizontalAlignment = HorizontalAlignment.Center,
                                    VerticalAlignment = VerticalAlignment.Center
                                }
                            },
                            AppKit.Place(new StackPanel
                            {
                                Spacing = 0,
                                Children =
                                {
                                    new TextBlock
                                    {
                                        Text = track.Title,
                                        Classes = { "arc-body-sm" },
                                        Foreground = current ? AppKit.Brush(ArcTokens.Accent) : AppKit.Brush(ArcTokens.Foreground)
                                    },
                                    AppKit.Caption(track.Artist)
                                }
                            }, 1),
                            AppKit.Place(AppKit.Caption(track.Length), 2)
                        }
                    }
                };
                panel.Children.Add(AppKit.Pressable(row, () =>
                {
                    index = target;
                    position = 0;
                    Build();
                    AppKit.Say(status, $"{track.Title} selected. Playback is simulated with a timer.");
                }));
            }

            panel.Children.Add(AppKit.Divider(8));
            panel.Children.Add(AppKit.Divider(8));
            panel.Children.Add(AppKit.Link("Open the full queue", () => AppKit.Say(status, "The queue page is simulated."), trailingIcon: "arrow-right"));

            playlistHost.Content = panel;
        }

        void Build()
        {
            BuildSeek();
            BuildPlaying();
            BuildPlaylist();
        }

        void TogglePlay()
        {
            playing = !playing;
            if (playing)
            {
                timer.Start();
            }
            else
            {
                timer.Stop();
            }

            Build();
            AppKit.Say(status, playing ? "Playing. Audio is simulated." : "Paused.");
        }

        void Skip(int direction)
        {
            index = (index + direction + Tracks.Length) % Tracks.Length;
            position = 0;
            Build();
            AppKit.Say(status, $"{(direction > 0 ? "Next" : "Previous")}: {Tracks[index].Title}.");
        }

        BuildSeek();
        BuildPlaying();
        BuildPlaylist();

        var cover = new Border
        {
            Width = 196,
            Height = 196,
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            Child = new Panel
            {
                Children =
                {
                    new Border { Background = AppKit.BrandWash(0.95, 0.55) },
                    new Border { Background = AppKit.Glow(ArcTokens.GradientForeground, 0.22, 0.9) },
                    new ArcIcon
                    {
                        Icon = "music",
                        Size = 46,
                        Foreground = AppKit.Tint(ArcTokens.GradientForeground, 0.9),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };

        var transport = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                AppKit.IconBtn("chevron-left", "Previous track", () => Skip(-1), 38),
                AppKit.Pressable(new Border
                {
                    Width = 54,
                    Height = 54,
                    CornerRadius = new CornerRadius(27),
                    Background = AppKit.Brush(ArcTokens.Accent),
                    Child = new ArcIcon
                    {
                        Icon = playing ? "pause" : "play",
                        Size = 22,
                        Foreground = AppKit.Brush(ArcTokens.AccentForeground),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }, TogglePlay),
                AppKit.IconBtn("chevron-right", "Next track", () => Skip(1), 38)
            }
        };

        var volumeTrack = new Border
        {
            Width = 120,
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = AppKit.Brush(ArcTokens.ControlTrack),
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions($"{Math.Clamp(muted ? 0 : volume, 0, 1):0.##}*,{Math.Clamp(1 - (muted ? 0 : volume), 0, 1):0.##}*"),
                Children =
                {
                    new Border { Background = AppKit.Brush(muted ? ArcTokens.TextMuted : ArcTokens.Accent) }
                }
            }
        };

        var volumeRow = AppKit.Row(8,
            AppKit.IconBtn(muted ? "minus-circle" : "activity", muted ? "Unmute audio" : "Mute audio", () =>
            {
                muted = !muted;
                Build();
                AppKit.Say(status, muted ? "Audio muted." : "Audio unmuted.");
            }, 30),
            volumeTrack,
            AppKit.Caption(muted ? "Muted" : $"{(int)(volume * 100)}%"));

        var nowHost = new ContentControl();
        var seek = new ContentControl();
        seekHost = seek;
        BuildSeek();
        nowHost.Content = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = Tracks[index].Title, Classes = { "arc-subtitle" } },
                AppKit.Row(7,
                    new TextBlock { Text = Tracks[index].Artist, Classes = { "arc-body-sm" } },
                    AppKit.Caption("· " + Tracks[index].Album)),
                playingHost,
                AppKit.Space(4),
                seekHost,
                AppKit.Space(2),
                transport,
                AppKit.Space(2),
                volumeRow,
                AppKit.Space(2),
                AppKit.Row(8,
                    AppKit.IconBtn("arrow-down-up", "Shuffle the queue", () => AppKit.Say(status, "Shuffle toggled. Playback is simulated."), 28),
                    AppKit.IconBtn("rotate-cw", "Repeat the album", () => AppKit.Say(status, "Repeat toggled."), 28),
                    AppKit.Pill("Lossless", ArcBadgeTone.Neutral),
                    AppKit.Caption("24 bit · 48 kHz"))
            }
        };

        timer.Tick += (_, _) =>
        {
            var total = Seconds(Tracks[index].Length);
            position += 0.5;
            if (position >= total)
            {
                position = 0;
                index = (index + 1) % Tracks.Length;
            }

            BuildSeek();
        };
        timer.Start();

        var player = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 18,
            Children =
            {
                cover,
                AppKit.Place(nowHost, 1)
            }
        };

        var left = AppKit.Card(new StackPanel
        {
            Spacing = 14,
            Children =
            {
                AppKit.Row(6,
                    AppKit.Pill("Now playing", ArcBadgeTone.Accent),
                    AppKit.Caption("Signals · 2026")),
                player,
                AppKit.Divider(),
                status
            }
        }, 18, 18);

        var right = AppKit.Card(playlistHost, 14, 18);

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,320"),
            ColumnSpacing = 12
        };
        layout.Children.Add(left);
        Grid.SetColumn(right, 1);
        layout.Children.Add(right);

        AppKit.Shortcut(layout, Key.Space, TogglePlay);
        AppKit.Shortcut(layout, Key.Right, () =>
        {
            position = Math.Min(Seconds(Tracks[index].Length), position + 5);
            BuildSeek();
        });
        AppKit.Shortcut(layout, Key.Left, () =>
        {
            position = Math.Max(0, position - 5);
            BuildSeek();
        });

        var screen = AppKit.Screen(new Border { Padding = new Thickness(18, 16, 18, 14), Child = layout }, 500);
        screen.DetachedFromVisualTree += (_, _) => timer.Stop();
        return screen;
    }
}
