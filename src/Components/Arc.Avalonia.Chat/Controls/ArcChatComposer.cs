using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Markdown;
using Arc.Avalonia.Services;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Chat;

/// <summary>Raised when the composer hands a draft to the host.</summary>
public class ArcChatSendRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the args.</summary>
    public ArcChatSendRoutedEventArgs()
    {
    }

    /// <summary>Creates the args for a draft.</summary>
    public ArcChatSendRoutedEventArgs(ArcChatDraft draft) => Draft = draft;

    /// <summary>The draft to send.</summary>
    public ArcChatDraft Draft { get; init; } = new(string.Empty, Array.Empty<ArcChatAttachment>());
}

/// <summary>
/// Chat composer: a padded multi-line input, an attach action with a pending-attachment tray, Enter to send,
/// Shift+Enter for a newline and a stop action while a run is active. The agent layer reuses it instead of
/// keeping a second composer.
/// </summary>
[TemplatePart("PART_Host", typeof(Panel))]
public class ArcChatComposer : TemplatedControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ArcChatComposer, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<ArcChatComposer, string>(nameof(Placeholder), "输入消息…");

    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<ArcChatComposer, bool>(nameof(IsRunning));

    public static readonly StyledProperty<bool> AllowAttachmentsProperty =
        AvaloniaProperty.Register<ArcChatComposer, bool>(nameof(AllowAttachments), true);

    public static readonly StyledProperty<int> MaxAttachmentsProperty =
        AvaloniaProperty.Register<ArcChatComposer, int>(nameof(MaxAttachments), 5);

    public static readonly StyledProperty<Control?> LeadingContentProperty =
        AvaloniaProperty.Register<ArcChatComposer, Control?>(nameof(LeadingContent));

    private Panel? _host;
    private TextBox? _input;
    private Border? _frame;
    private ArcButton? _sendButton;
    private ArcButton? _stopButton;
    private ArcButton? _attachButton;
    private WrapPanel? _tray;
    private readonly List<ArcChatAttachment> _pending = [];

    private bool _themeSubscribed;

    /// <summary>Creates the composer and routes Enter before the TextBox consumes it.</summary>
    public ArcChatComposer()
    {
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    static ArcChatComposer()
    {
        TextProperty.Changed.AddClassHandler<ArcChatComposer>((composer, _) => composer.SyncText());
        PlaceholderProperty.Changed.AddClassHandler<ArcChatComposer>((composer, _) => composer.OnPlaceholderChanged());
        IsRunningProperty.Changed.AddClassHandler<ArcChatComposer>((composer, _) => composer.UpdateActions());
        AllowAttachmentsProperty.Changed.AddClassHandler<ArcChatComposer>((composer, _) => composer.Rebuild());
        MaxAttachmentsProperty.Changed.AddClassHandler<ArcChatComposer>((composer, _) => composer.UpdateActions());
        LeadingContentProperty.Changed.AddClassHandler<ArcChatComposer>((composer, _) => composer.Rebuild());
    }

    /// <summary>Raised when the user submits a draft.</summary>
    public event EventHandler<ArcChatDraft>? Send;

    /// <summary>Raised when the user asks to stop the active run.</summary>
    public event EventHandler? StopRequested;

    /// <summary>Routed companion to <see cref="Send"/>.</summary>
    public static readonly RoutedEvent<ArcChatSendRoutedEventArgs> SendEvent =
        RoutedEvent.Register<ArcChatComposer, ArcChatSendRoutedEventArgs>("Send", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="Send"/>.</summary>
    public event EventHandler<ArcChatSendRoutedEventArgs>? SendRouted
    {
        add => AddHandler(SendEvent, value);
        remove => RemoveHandler(SendEvent, value);
    }

    /// <summary>Current text.</summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Placeholder copy.</summary>
    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>True while a run is active; the send action turns into stop.</summary>
    public bool IsRunning
    {
        get => GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    /// <summary>Shows the attach action.</summary>
    public bool AllowAttachments
    {
        get => GetValue(AllowAttachmentsProperty);
        set => SetValue(AllowAttachmentsProperty, value);
    }

    /// <summary>Maximum pending attachments.</summary>
    public int MaxAttachments
    {
        get => GetValue(MaxAttachmentsProperty);
        set => SetValue(MaxAttachmentsProperty, value);
    }

    /// <summary>Optional leading slot (model chip, tool toggles).</summary>
    public Control? LeadingContent
    {
        get => GetValue(LeadingContentProperty);
        set => SetValue(LeadingContentProperty, value);
    }

    /// <summary>Pending attachments staged in the tray.</summary>
    public IReadOnlyList<ArcChatAttachment> PendingAttachments => _pending;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_themeSubscribed || Application.Current is null)
        {
            return;
        }

        Application.Current.ActualThemeVariantChanged += OnThemeVariantChanged;
        _themeSubscribed = true;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_themeSubscribed && Application.Current is not null)
        {
            Application.Current.ActualThemeVariantChanged -= OnThemeVariantChanged;
            _themeSubscribed = false;
        }

        _input = null;
        _frame = null;
        _sendButton = null;
        _stopButton = null;
        _attachButton = null;
        _tray = null;
    }

    /// <summary>Clears the input and pending attachments.</summary>
    public void Clear()
    {
        Text = string.Empty;
        _pending.Clear();
        if (_input is not null)
        {
            _input.Text = string.Empty;
        }

        RebuildTray();
        UpdateActions();
    }

    /// <summary>Focuses the input.</summary>
    public void FocusInput() => _input?.Focus();

    /// <summary>Stages an attachment; ignored once <see cref="MaxAttachments"/> is reached.</summary>
    public void AddAttachment(ArcChatAttachment attachment)
    {
        TryAddAttachment(attachment);
    }

    /// <summary>Stages an attachment; returns whether it fit under <see cref="MaxAttachments"/>.</summary>
    public bool TryAddAttachment(ArcChatAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        if (_pending.Count >= MaxAttachments)
        {
            return false;
        }

        _pending.Add(attachment);
        RebuildTray();
        UpdateActions();
        return true;
    }

    /// <summary>Removes a staged attachment.</summary>
    public void RemoveAttachment(ArcChatAttachment attachment)
    {
        if (_pending.Remove(attachment))
        {
            RebuildTray();
            UpdateActions();
        }
    }

    /// <summary>Submits the current draft programmatically (no-op while a run is active).</summary>
    public void Submit()
    {
        if (IsRunning)
        {
            return;
        }

        var text = _input?.Text ?? Text;
        if (string.IsNullOrWhiteSpace(text) && _pending.Count == 0)
        {
            return;
        }

        var draft = new ArcChatDraft(text.Trim(), _pending.ToArray());
        Clear();
        Send?.Invoke(this, draft);
        RaiseEvent(new ArcChatSendRoutedEventArgs { RoutedEvent = SendEvent, Draft = draft });
        _input?.Focus();
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        // A few icon/text brushes are snapshots; rebuild once so the composer follows light/dark.
        Dispatcher.UIThread.Post(Rebuild, DispatcherPriority.Background);
    }

    private void SyncText()
    {
        if (_input is not null && !string.Equals(_input.Text, Text, StringComparison.Ordinal))
        {
            _input.Text = Text;
        }
    }

    private void OnPlaceholderChanged()
    {
        if (_input is not null)
        {
            _input.PlaceholderText = Placeholder;
            AutomationProperties.SetName(_input, Placeholder);
        }
    }

    private void Rebuild()
    {
        if (_host is null)
        {
            return;
        }

        var currentText = _input?.Text ?? Text;
        _host.Children.Clear();

        var shell = new StackPanel { Spacing = 6 };

        _tray = new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = double.NaN };
        shell.Children.Add(_tray);

        if (LeadingContent is not null)
        {
            shell.Children.Add(LeadingContent);
        }

        _frame = new Border
        {
            CornerRadius = ArcChatUi.Radius(ArcTokens.RadiusControl),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 4, 4),
        };
        _frame.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
        _frame.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto") };

        _attachButton = CreateIconButton("plus", "添加附件", ArcButtonVariant.Ghost, IsRunning ? false : AllowAttachments);
        _attachButton.Click += async (_, _) => await AttachAsync();
        grid.Children.Add(_attachButton);

        _input = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 7),
            MinHeight = 38,
            MaxHeight = 140,
            FontSize = ArcChatUi.TextSize(ArcTokens.TextSm, 13),
            PlaceholderText = Placeholder,
            VerticalContentAlignment = VerticalAlignment.Center,
            Text = currentText,
        };
        _input.Paint(TextBox.ForegroundProperty, ArcTokens.Foreground);
        _input.Paint(TextBox.PlaceholderForegroundProperty, ArcTokens.TextMuted);
        _input.Paint(TextBox.SelectionBrushProperty, ArcTokens.AccentSubtle);
        _input.Paint(TextBox.CaretBrushProperty, ArcTokens.AccentStrong);
        AutomationProperties.SetName(_input, Placeholder);
        AutomationProperties.SetHelpText(_input, "输入消息。Enter 发送，Shift+Enter 换行。");
        _input.TextChanged += (_, _) =>
        {
            Text = _input.Text ?? string.Empty;
            UpdateActions();
        };

        _input.GotFocus += (_, _) => _frame?.Paint(Border.BorderBrushProperty, ArcTokens.FocusOutline);
        _input.LostFocus += (_, _) => _frame?.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
        Grid.SetColumn(_input, 1);
        grid.Children.Add(_input);

        _stopButton = CreateIconButton("square", "停止生成", ArcButtonVariant.Secondary, !IsRunning);
        _stopButton.Width = 38;
        _stopButton.Height = 38;
        _stopButton.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);
        Grid.SetColumn(_stopButton, 2);
        grid.Children.Add(_stopButton);

        _sendButton = CreateIconButton("arrow-up", "发送消息", ArcButtonVariant.Primary, !IsRunning);
        _sendButton.Width = 38;
        _sendButton.Height = 38;
        _sendButton.Click += (_, _) => Submit();
        Grid.SetColumn(_sendButton, 3);
        grid.Children.Add(_sendButton);

        _frame.Child = grid;
        shell.Children.Add(_frame);
        shell.Children.Add(new TextBlock
        {
            Text = "Enter 发送 · Shift+Enter 换行",
            FontSize = ArcChatUi.TextSize(ArcTokens.TextXs, 12),
            Foreground = ArcChatUi.Brush(ArcTokens.TextMuted),
            HorizontalAlignment = HorizontalAlignment.Right,
        });

        _host.Children.Add(shell);
        RebuildTray();
        UpdateActions();
    }

    private ArcButton CreateIconButton(string icon, string name, ArcButtonVariant variant, bool visibleOrEnabled)
    {
        var button = new ArcButton
        {
            Content = new ArcIcon
            {
                Icon = icon,
                Size = icon == "square" ? 14 : 16,
                Foreground = ArcChatUi.Brush(variant == ArcButtonVariant.Primary
                    ? ArcTokens.AccentForeground
                    : ArcTokens.TextSecondary),
            },
            Variant = variant,
            Size = ArcButtonSize.Sm,
            Compact = true,
            Width = 34,
            Height = 34,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsVisible = visibleOrEnabled,
        };
        AutomationProperties.SetName(button, name);
        return button;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift) || _input?.IsFocused != true)
        {
            return;
        }

        e.Handled = true;
        Submit();
    }

    private void UpdateActions()
    {
        if (_sendButton is not null)
        {
            _sendButton.IsVisible = !IsRunning;
            _sendButton.IsEnabled = IsRunning || !string.IsNullOrWhiteSpace(_input?.Text) || _pending.Count > 0;
        }

        if (_stopButton is not null)
        {
            _stopButton.IsVisible = IsRunning;
        }

        if (_attachButton is not null)
        {
            _attachButton.IsVisible = AllowAttachments;
            _attachButton.IsEnabled = !IsRunning;
        }
    }

    private void RebuildTray()
    {
        if (_tray is null)
        {
            return;
        }

        _tray.Children.Clear();
        if (_pending.Count == 0)
        {
            _tray.IsVisible = false;
            return;
        }

        _tray.IsVisible = true;
        foreach (var attachment in _pending.ToArray())
        {
            var remove = new ArcButton
            {
                Content = new ArcIcon { Icon = "x", Size = 12, Foreground = ArcChatUi.Brush(ArcTokens.TextMuted) },
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Compact = true,
                Width = 22,
                Height = 22,
            };
            AutomationProperties.SetName(remove, $"移除附件 {attachment.Name}");
            remove.Click += (_, _) => RemoveAttachment(attachment);

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new ArcIcon
                    {
                        Icon = attachment.IsImage ? "image" : "file",
                        Size = 14,
                        Foreground = ArcChatUi.Brush(ArcTokens.TextSecondary),
                    },
                    new TextBlock
                    {
                        Text = attachment.Name,
                        FontSize = ArcChatUi.TextSize(ArcTokens.TextXs, 12),
                        Foreground = ArcChatUi.Brush(ArcTokens.Foreground),
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    remove,
                },
            };

            var chip = new Border
            {
                CornerRadius = ArcChatUi.Radius(ArcTokens.RadiusPill),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 3, 6, 3),
                Margin = new Thickness(0, 0, 6, 4),
                Child = row,
            };
            chip.Paint(Border.BackgroundProperty, ArcTokens.Surface);
            chip.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
            AutomationProperties.SetName(chip, attachment.Name);
            _tray.Children.Add(chip);
        }
    }

    private async Task AttachAsync()
    {
        if (IsRunning)
        {
            return;
        }

        try
        {
            var top = TopLevel.GetTopLevel(this);
            var options = new FilePickerOpenOptions { Title = "添加附件", AllowMultiple = true };
            IReadOnlyList<IStorageFile> files;
            if (ArcServiceProvider.TryGet<IFilePickerService>(out var picker) && picker is not null)
            {
                files = await picker.OpenFileAsync(options, top);
            }
            else if (top?.StorageProvider is not null)
            {
                files = await top.StorageProvider.OpenFilePickerAsync(options);
            }
            else
            {
                return;
            }

            foreach (var file in files.Take(Math.Max(0, MaxAttachments - _pending.Count)))
            {
                var path = file.TryGetLocalPath();
                long size = 0;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    try
                    {
                        size = new FileInfo(path).Length;
                    }
                    catch
                    {
                        // Size is optional metadata.
                    }
                }

                TryAddAttachment(new ArcChatAttachment
                {
                    Name = file.Name,
                    Size = size,
                    Url = path,
                    IsImage = IsImageName(file.Name),
                });
            }
        }
        catch
        {
            // Attachment picking is best effort; a denied picker leaves the composer untouched.
        }
    }

    private static bool IsImageName(string name)
        => name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
           || name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
           || name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
           || name.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
           || name.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
}
