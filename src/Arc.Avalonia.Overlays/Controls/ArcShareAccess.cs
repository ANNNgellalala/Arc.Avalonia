using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Overlays.Controls;

/// <summary>One role a person can hold. Owner rows cannot be changed or removed.</summary>
public sealed class ArcShareRoleOption
{
    public ArcShareRoleOption()
    {
    }

    public ArcShareRoleOption(string value, string label, bool isOwner = false, bool allowForLink = true)
    {
        Value = value;
        Label = label;
        IsOwner = isOwner;
        AllowForLink = allowForLink;
    }

    public string Value { get; set; } = string.Empty;

    /// <summary>Menu text, for example "Can edit".</summary>
    public string Label { get; set; } = string.Empty;

    public bool IsOwner { get; set; }

    /// <summary>Owners are never offered as the role for anyone with the link.</summary>
    public bool AllowForLink { get; set; } = true;
}

/// <summary>Someone with access to the shared object.</summary>
public sealed class ArcSharePerson
{
    public string? DisplayName { get; set; }

    public string? Email { get; set; }

    /// <summary>Role value, matched against <see cref="ArcShareAccess.Roles"/>.</summary>
    public string? Role { get; set; }

    /// <summary>An invited person who has not accepted yet.</summary>
    public bool IsPending { get; set; }
}

/// <summary>A directory entry suggested while typing.</summary>
public sealed class ArcShareSuggestion
{
    public ArcShareSuggestion()
    {
    }

    public ArcShareSuggestion(string displayName, string email, string? initials = null)
    {
        DisplayName = displayName;
        Email = email;
        Initials = initials;
    }

    public string? DisplayName { get; set; }

    public string? Email { get; set; }

    /// <summary>Avatar text; derived from the name when omitted.</summary>
    public string? Initials { get; set; }
}

public enum ArcShareLinkAccess
{
    Restricted,
    Anyone
}

public sealed class ArcShareInviteEventArgs : EventArgs
{
    public ArcShareInviteEventArgs(IReadOnlyList<ArcShareSuggestion> people, string? role)
    {
        People = people;
        Role = role;
    }

    public IReadOnlyList<ArcShareSuggestion> People { get; }

    public string? Role { get; }
}

/// <summary>
/// A share panel for a document, board or project: type an address to make a chip, choose suggestions with
/// the keyboard, invite with a role, change roles from a compact menu whose label morphs to its new width,
/// remove someone with an undo bar that lingers for five seconds, switch general access between restricted
/// and anyone with the link, and copy the link with an in-place confirmation.
///
/// The panel is a page-hosted surface (it sits in a card rather than a popup), so suggestions and role
/// menus are hosted in its own overlay canvas and render in screenshots. Approximations of the public
/// description: the chip field reflows immediately instead of animating layout transforms, the people
/// count updates without a rolling digit, and the clipboard confirmation falls back to a failure state
/// when the platform clipboard is unavailable. No Pro source was consulted.
/// </summary>
public sealed class ArcShareAccess : TemplatedControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ArcShareAccess, string>(nameof(Title), "Q3 launch plan");

    public static readonly StyledProperty<IList<ArcSharePerson>?> PeopleProperty =
        AvaloniaProperty.Register<ArcShareAccess, IList<ArcSharePerson>?>(nameof(People));

    public static readonly StyledProperty<IList<ArcShareRoleOption>?> RolesProperty =
        AvaloniaProperty.Register<ArcShareAccess, IList<ArcShareRoleOption>?>(nameof(Roles));

    public static readonly StyledProperty<IList<ArcShareSuggestion>?> SuggestionsProperty =
        AvaloniaProperty.Register<ArcShareAccess, IList<ArcShareSuggestion>?>(nameof(Suggestions));

    public static readonly StyledProperty<ArcShareLinkAccess> LinkAccessProperty =
        AvaloniaProperty.Register<ArcShareAccess, ArcShareLinkAccess>(nameof(LinkAccess));

    public static readonly StyledProperty<string?> LinkRoleProperty =
        AvaloniaProperty.Register<ArcShareAccess, string?>(nameof(LinkRole), "viewer");

    public static readonly StyledProperty<string?> LinkUrlProperty =
        AvaloniaProperty.Register<ArcShareAccess, string?>(nameof(LinkUrl));

    public static readonly StyledProperty<string?> SelfEmailProperty =
        AvaloniaProperty.Register<ArcShareAccess, string?>(nameof(SelfEmail));

    public static readonly StyledProperty<string?> InviteRoleProperty =
        AvaloniaProperty.Register<ArcShareAccess, string?>(nameof(InviteRole), "editor");

    public static readonly StyledProperty<bool> ShowDoneProperty =
        AvaloniaProperty.Register<ArcShareAccess, bool>(nameof(ShowDone));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcShareAccess, string>(nameof(Label), "Share");

    private static readonly Regex EmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly List<Chip> _chips = new();
    private readonly List<ArcButton> _suggestionButtons = new();
    private readonly DispatcherTimer _undoTimer;
    private readonly DispatcherTimer _copyTimer;
    private readonly DispatcherTimer _commitTimer;
    private readonly DispatcherTimer _settleTimer;

    private TextBlock? _heading;
    private ArcButton? _done;
    private Border? _fieldShell;
    private Border? _fieldRing;
    private TextBox? _textBox;
    private TextBlock? _placeholder;
    private WrapPanel? _chipsHost;
    private StackPanel? _errorRow;
    private TextBlock? _errorText;
    private ArcButton? _inviteRoleButton;
    private ArcButton? _inviteButton;
    private StackPanel? _peopleHost;
    private TextBlock? _peopleCount;
    private Border? _undoBar;
    private TextBlock? _undoText;
    private ArcButton? _undoButton;
    private Border? _accessIconBox;
    private ArcIcon? _accessIcon;
    private TextBlock? _accessTitle;
    private TextBlock? _accessDescription;
    private ArcButton? _accessButton;
    private Grid? _linkRoleRow;
    private ArcButton? _linkRoleButton;
    private TextBlock? _linkText;
    private ArcButton? _copyButton;
    private ArcIcon? _copyIcon;
    private TextBlock? _copyLabel;
    private Canvas? _overlay;
    private Border? _suggestions;
    private StackPanel? _suggestionRows;
    private ArcMenuSurface? _menu;
    private Control? _menuTrigger;

    private int _suggestionIndex = -1;
    private Chip? _markedChip;
    private ArcSharePerson? _pendingPerson;
    private int _pendingIndex = -1;
    private Border? _pendingContainer;
    private double _pendingHeight;
    private bool _pointerFocus;
    private bool _copying;
    private Control? _lastClosedTrigger;
    private DateTime _menuClosedAt;
    private bool _headsUp;

    static ArcShareAccess()
    {
        TitleProperty.Changed.AddClassHandler<ArcShareAccess>((panel, _) => panel.UpdateHeading());
        PeopleProperty.Changed.AddClassHandler<ArcShareAccess>((panel, _) => panel.BuildPeople());
        RolesProperty.Changed.AddClassHandler<ArcShareAccess>((panel, _) => panel.UpdateRoleFaces());
        LinkAccessProperty.Changed.AddClassHandler<ArcShareAccess>((panel, _) => panel.SyncAccess(animate: true));
        LinkRoleProperty.Changed.AddClassHandler<ArcShareAccess>((panel, _) => panel.UpdateRoleFaces());
        LinkUrlProperty.Changed.AddClassHandler<ArcShareAccess>((panel, _) => panel.UpdateLink());
        SelfEmailProperty.Changed.AddClassHandler<ArcShareAccess>((panel, _) => panel.BuildPeople());
        InviteRoleProperty.Changed.AddClassHandler<ArcShareAccess>((panel, _) => panel.UpdateRoleFaces());
        ShowDoneProperty.Changed.AddClassHandler<ArcShareAccess>((panel, _) => panel.UpdateHeading());
        LabelProperty.Changed.AddClassHandler<ArcShareAccess>((panel, _) => panel.UpdateHeading());
    }

    public ArcShareAccess()
    {
        ClipToBounds = false;

        _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _undoTimer.Tick += (_, _) =>
        {
            _undoTimer.Stop();
            HideUndo();
            _pendingPerson = null;
            _pendingIndex = -1;
            _pendingContainer = null;
        };

        _copyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
        _copyTimer.Tick += (_, _) =>
        {
            _copyTimer.Stop();
            SetCopyState(CopyState.Idle);
        };

        _commitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _commitTimer.Tick += (_, _) =>
        {
            _commitTimer.Stop();
            if (_pendingContainer is not null && _peopleHost is not null)
            {
                _peopleHost.Children.Remove(_pendingContainer);
            }

            _pendingContainer = null;
        };

        _settleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(520) };
        _settleTimer.Tick += (_, _) =>
        {
            _settleTimer.Stop();
            foreach (var button in _rowButtons)
            {
                button.Width = double.NaN;
            }
        };

        LayoutUpdated += (_, _) =>
        {
            if (_menu is not null)
            {
                PositionMenu();
            }

            if (_suggestions is not null)
            {
                PositionSuggestions();
            }
        };
    }

    /// <summary>Raised with the valid chips and the chosen role when Invite is pressed.</summary>
    public event EventHandler<ArcShareInviteEventArgs>? Invite;

    /// <summary>Raised after someone is removed, once the row has folded away.</summary>
    public event EventHandler<ArcSharePerson>? Remove;

    /// <summary>Raised when a removal is undone.</summary>
    public event EventHandler<ArcSharePerson>? UndoRemove;

    /// <summary>Raised after an invite, a role change, a removal or an undo, with the whole next list.</summary>
    public event EventHandler? PeopleChanged;

    public event EventHandler<ArcShareLinkAccess>? LinkAccessChanged;

    /// <summary>Raised once the link is on the clipboard.</summary>
    public event EventHandler<string>? LinkCopied;

    public event EventHandler? Done;

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>People with access. The control mutates this list and reports changes with PeopleChanged.</summary>
    public IList<ArcSharePerson>? People
    {
        get => GetValue(PeopleProperty);
        set => SetValue(PeopleProperty, value);
    }

    /// <summary>Roles offered in the menus; defaults to owner, can edit, can comment, can view.</summary>
    public IList<ArcShareRoleOption>? Roles
    {
        get => GetValue(RolesProperty);
        set => SetValue(RolesProperty, value);
    }

    /// <summary>Directory people suggested while typing.</summary>
    public IList<ArcShareSuggestion>? Suggestions
    {
        get => GetValue(SuggestionsProperty);
        set => SetValue(SuggestionsProperty, value);
    }

    public ArcShareLinkAccess LinkAccess
    {
        get => GetValue(LinkAccessProperty);
        set => SetValue(LinkAccessProperty, value);
    }

    /// <summary>What anyone with the link can do once link access is on.</summary>
    public string? LinkRole
    {
        get => GetValue(LinkRoleProperty);
        set => SetValue(LinkRoleProperty, value);
    }

    public string? LinkUrl
    {
        get => GetValue(LinkUrlProperty);
        set => SetValue(LinkUrlProperty, value);
    }

    /// <summary>Email of the current user, marked "(you)" and never removable.</summary>
    public string? SelfEmail
    {
        get => GetValue(SelfEmailProperty);
        set => SetValue(SelfEmailProperty, value);
    }

    /// <summary>Role preselected for a new invite.</summary>
    public string? InviteRole
    {
        get => GetValue(InviteRoleProperty);
        set => SetValue(InviteRoleProperty, value);
    }

    /// <summary>Shows a Done button that raises <see cref="Done"/>.</summary>
    public bool ShowDone
    {
        get => GetValue(ShowDoneProperty);
        set => SetValue(ShowDoneProperty, value);
    }

    /// <summary>Accessible name of the panel; the heading uses the title.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _heading = e.NameScope.Find<TextBlock>("Heading");
        _done = e.NameScope.Find<ArcButton>("DoneButton");
        _fieldShell = e.NameScope.Find<Border>("FieldShell");
        _fieldRing = e.NameScope.Find<Border>("FieldRing");
        _chipsHost = e.NameScope.Find<WrapPanel>("Chips");
        _placeholder = e.NameScope.Find<TextBlock>("Placeholder");
        _errorRow = e.NameScope.Find<StackPanel>("ErrorRow");
        _errorText = e.NameScope.Find<TextBlock>("ErrorText");
        _inviteRoleButton = e.NameScope.Find<ArcButton>("InviteRole");
        _inviteButton = e.NameScope.Find<ArcButton>("InviteButton");
        _peopleHost = e.NameScope.Find<StackPanel>("PeopleHost");
        _peopleCount = e.NameScope.Find<TextBlock>("PeopleCount");
        _undoBar = e.NameScope.Find<Border>("UndoBar");
        _undoText = e.NameScope.Find<TextBlock>("UndoText");
        _undoButton = e.NameScope.Find<ArcButton>("UndoButton");
        _accessIconBox = e.NameScope.Find<Border>("AccessIconBox");
        _accessIcon = e.NameScope.Find<ArcIcon>("AccessIcon");
        _accessTitle = e.NameScope.Find<TextBlock>("AccessTitle");
        _accessDescription = e.NameScope.Find<TextBlock>("AccessDescription");
        _accessButton = e.NameScope.Find<ArcButton>("AccessButton");
        _linkRoleRow = e.NameScope.Find<Grid>("LinkRoleRow");
        _linkRoleButton = e.NameScope.Find<ArcButton>("LinkRoleButton");
        _linkText = e.NameScope.Find<TextBlock>("LinkText");
        _copyButton = e.NameScope.Find<ArcButton>("CopyButton");
        _copyIcon = e.NameScope.Find<ArcIcon>("CopyIcon");
        _copyLabel = e.NameScope.Find<TextBlock>("CopyLabel");
        _overlay = e.NameScope.Find<Canvas>("Overlay");

        BuildField();
        UpdatePlaceholder();
        UpdateInviteState();

        if (_done is not null)
        {
            _done.Click += (_, _) => Done?.Invoke(this, EventArgs.Empty);
        }

        if (_undoButton is not null)
        {
            _undoButton.Click += (_, _) => Undo();
        }

        if (_inviteButton is not null)
        {
            _inviteButton.Click += (_, _) => SendInvite();
        }

        if (_inviteRoleButton is not null)
        {
            _inviteRoleButton.Click += (_, _) => OpenRoleMenuForInvite();
        }

        if (_accessButton is not null)
        {
            _accessButton.Click += (_, _) => OpenAccessMenu();
        }

        if (_linkRoleButton is not null)
        {
            _linkRoleButton.Click += (_, _) => OpenRoleMenuForLink();
        }

        if (_copyButton is not null)
        {
            _copyButton.Click += (_, _) => _ = CopyLinkAsync();
        }

        UpdateHeading();
        UpdateRoleFaces();
        SyncAccess(animate: false);
        UpdateLink();
        BuildPeople();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _undoTimer.Stop();
        _copyTimer.Stop();
        _commitTimer.Stop();
        _settleTimer.Stop();
        CloseSuggestions();
        CloseMenu(returnFocus: false);
        base.OnDetachedFromVisualTree(e);
    }

    // ---------------------------------------------------------------- public helpers

    /// <summary>
    /// Turns text into a chip. Returns false when the text is not a valid address; the chip is then drawn
    /// with the danger tone and the error line explains how to fix it.
    /// </summary>
    public bool AddChip(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var chip = ResolveChip(text.Trim());
        if (chip is null)
        {
            return false;
        }

        _chips.Add(chip);
        RebuildChips();
        RefreshChipError();
        UpdateInviteState();
        if (!chip.Valid)
        {
            _chipsHost?.Children.OfType<TextBox>().FirstOrDefault()?.Focus();
        }

        return chip.Valid;
    }

    /// <summary>Removes someone with the five second undo bar.</summary>
    public void RemovePerson(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var list = People ??= new List<ArcSharePerson>();
        var person = list.FirstOrDefault(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase));
        if (person is null || IsOwner(person))
        {
            return;
        }

        CommitUndo();
        var index = list.IndexOf(person);
        list.Remove(person);
        _pendingPerson = person;
        _pendingIndex = index;

        if (_rowContainers.TryGetValue(person, out var container))
        {
            _pendingContainer = container;
            _pendingHeight = container.Bounds.Height;
            if (!ArcThemeManager.ReducedMotion && container.Bounds.Height > 0)
            {
                container.Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = HeightProperty,
                        Duration = ArcMotion.SpringSmoothDuration,
                        Easing = ArcMotion.SpringSmooth
                    },
                    new DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = ArcMotion.Fast,
                        Easing = ArcMotion.EaseStandard
                    }
                };
                container.Height = 0;
                container.Opacity = 0;
                _commitTimer.Stop();
                _commitTimer.Start();
            }
            else
            {
                _peopleHost?.Children.Remove(container);
                _pendingContainer = null;
            }
        }

        ShowUndo(person);
        UpdateCount();
        Remove?.Invoke(this, person);
        PeopleChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Restores the last removal, if the undo bar is still showing.</summary>
    public void Undo()
    {
        if (_pendingPerson is null)
        {
            return;
        }

        var person = _pendingPerson;
        var index = Math.Clamp(_pendingIndex, 0, Math.Max(0, (People?.Count ?? 0)));
        var list = People ??= new List<ArcSharePerson>();
        list.Insert(index, person);

        _pendingPerson = null;
        _pendingIndex = -1;
        _pendingContainer = null;
        _commitTimer.Stop();
        HideUndo();
        BuildPeople();
        UndoRemove?.Invoke(this, person);
        PeopleChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Opens a role menu: the link role when no email is given, otherwise that person's role menu.
    /// Exposed so a page can present the open state.
    /// </summary>
    public void OpenRoleMenu(string? email = null)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            OpenRoleMenuForLink();
            return;
        }

        var person = (People ?? new List<ArcSharePerson>())
            .FirstOrDefault(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase));
        if (person is null || !_rowTriggers.TryGetValue(person, out var trigger))
        {
            return;
        }

        OpenRoleMenuFor(person, trigger);
    }

    /// <summary>Opens the general access menu, for demos and keyboard shortcuts.</summary>
    public void OpenAccessMenu() => OpenAccessMenuCore();

    /// <summary>Puts <see cref="LinkUrl"/> on the clipboard and confirms in place.</summary>
    public async Task CopyLinkAsync()
    {
        if (_copying)
        {
            return;
        }

        _copying = true;
        SetCopyState(CopyState.Busy);
        var copied = false;
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null && !string.IsNullOrWhiteSpace(LinkUrl))
            {
                await clipboard.SetTextAsync(LinkUrl!).ConfigureAwait(true);
                copied = true;
            }
        }
        catch
        {
            copied = false;
        }

        _copying = false;
        SetCopyState(copied ? CopyState.Copied : CopyState.Failed);
        if (copied)
        {
            LinkCopied?.Invoke(this, LinkUrl ?? string.Empty);
        }

        _copyTimer.Stop();
        _copyTimer.Start();
    }

    // ---------------------------------------------------------------- heading and copy

    private void UpdateHeading()
    {
        if (_heading is not null)
        {
            _heading.Text = $"Share “{Title}”";
        }

        if (_done is not null)
        {
            _done.IsVisible = ShowDone;
        }

        AutomationProperties.SetName(this, $"{Label} {Title}");
        if (_inviteButton is not null)
        {
            AutomationProperties.SetHelpText(_inviteButton, "Sends an invite to everyone added");
        }
    }

    private void UpdateLink()
    {
        if (_linkText is not null)
        {
            _linkText.Text = LinkUrl ?? "No link yet";
            _linkText.TextTrimming = TextTrimming.CharacterEllipsis;
            ToolTip.SetTip(_linkText, LinkUrl);
        }

        if (_copyButton is not null)
        {
            AutomationProperties.SetName(_copyButton, "Copy link");
        }

        UpdateInviteState();
    }

    // ---------------------------------------------------------------- invite field

    private void BuildField()
    {
        if (_chipsHost is null)
        {
            return;
        }

        _textBox = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 2),
            MinWidth = 150,
            FontSize = 14,
            FocusAdorner = null,
            CaretBrush = ArcActionVisuals.Brush(ArcTokens.AccentStrong),
            PlaceholderText = string.Empty,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _textBox.Classes.Add("share-input");
        AutomationProperties.SetName(_textBox, "Invite people by name or email");
        AutomationProperties.SetHelpText(_textBox, "Type a name or email, then press Enter to add a chip");

        _textBox.TextChanged += (_, _) => OnFieldTextChanged();
        _textBox.KeyDown += OnFieldKeyDown;
        _textBox.PointerPressed += (_, _) => _pointerFocus = true;
        _textBox.GotFocus += (_, _) =>
        {
            _fieldShell?.Classes.Set("focus", true);
            if (!_pointerFocus && _fieldRing is not null)
            {
                _fieldRing.IsVisible = true;
            }

            _pointerFocus = false;
        };
        _textBox.LostFocus += (_, _) =>
        {
            _fieldShell?.Classes.Set("focus", false);
            if (_fieldRing is not null)
            {
                _fieldRing.IsVisible = false;
            }
        };

        _chipsHost.Children.Add(_textBox);

        if (_fieldShell is not null)
        {
            _fieldShell.PointerPressed += (_, _) => _textBox?.Focus();
            AutomationProperties.SetName(_fieldShell, "Invite field");
        }

        if (_overlay is not null)
        {
            _overlay.PointerPressed += OnOverlayPointerPressed;
        }
    }

    private void OnFieldTextChanged()
    {
        _markedChip = null;
        RefreshChipClasses();
        UpdatePlaceholder();
        UpdateInviteState();

        var text = _textBox?.Text?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            CloseSuggestions();
            RefreshChipError();
            return;
        }

        if (!_chips.Any(chip => !chip.Valid))
        {
            ShowError(false, null);
        }

        UpdateSuggestions(text);
    }

    private void OnFieldKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (_suggestions is not null && _suggestionIndex >= 0)
                {
                    AcceptSuggestion(_suggestionIndex);
                }
                else if (!string.IsNullOrWhiteSpace(_textBox?.Text))
                {
                    CommitText();
                }
                else if (HasValidChips)
                {
                    SendInvite();
                }

                e.Handled = true;
                break;
            case Key.Down when _suggestions is not null:
                MoveSuggestion(1);
                e.Handled = true;
                break;
            case Key.Up when _suggestions is not null:
                MoveSuggestion(-1);
                e.Handled = true;
                break;
            case Key.Escape:
                if (_suggestions is not null)
                {
                    CloseSuggestions();
                }
                else if (!string.IsNullOrEmpty(_textBox?.Text))
                {
                    SetFieldText(string.Empty);
                }

                e.Handled = true;
                break;
            case Key.Back when string.IsNullOrEmpty(_textBox?.Text) && _chips.Count > 0:
                if (_markedChip is null)
                {
                    _markedChip = _chips[^1];
                    RefreshChipClasses();
                }
                else
                {
                    RemoveChip(_markedChip);
                }

                e.Handled = true;
                break;
            case Key.OemComma:
            case Key.OemSemicolon:
            case Key.Tab:
                if (!string.IsNullOrWhiteSpace(_textBox?.Text))
                {
                    CommitText();
                    e.Handled = true;
                }

                break;
            case Key.Space when (_textBox?.Text ?? string.Empty).Contains('@'):
                CommitText();
                e.Handled = true;
                break;
        }
    }

    private void CommitText()
    {
        var text = _textBox?.Text?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return;
        }

        SetFieldText(string.Empty);
        AddChip(text);
    }

    private void SetFieldText(string text)
    {
        if (_textBox is not null)
        {
            _textBox.Text = text;
            _textBox.CaretIndex = text.Length;
        }
    }

    private Chip? ResolveChip(string text)
    {
        var directory = (Suggestions ?? new List<ArcShareSuggestion>())
            .FirstOrDefault(item =>
                string.Equals(item.Email, text, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(item.DisplayName) &&
                 string.Equals(item.DisplayName, text, StringComparison.OrdinalIgnoreCase)));

        if (directory is not null)
        {
            return new Chip(directory.DisplayName ?? directory.Email ?? text, directory.Email, directory.Initials, true);
        }

        return EmailPattern.IsMatch(text)
            ? new Chip(text, text, null, true)
            : new Chip(text, null, null, false);
    }

    private void RemoveChip(Chip chip)
    {
        _chips.Remove(chip);
        _markedChip = null;
        RebuildChips();
        RefreshChipError();
        UpdateInviteState();
        _textBox?.Focus();
    }

    private void RebuildChips()
    {
        if (_chipsHost is null)
        {
            return;
        }

        _chipsHost.Children.Clear();
        foreach (var chip in _chips)
        {
            _chipsHost.Children.Add(BuildChip(chip));
        }

        if (_textBox is null)
        {
            BuildField();
        }
        else
        {
            _chipsHost.Children.Add(_textBox);
        }

        RefreshChipClasses();
        UpdatePlaceholder();
    }

    private Control BuildChip(Chip chip)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(9999),
            Padding = new Thickness(4, 3, 6, 3),
            Margin = new Thickness(0, 0, 4, 4),
            Height = 30,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Classes.Add("sa-chip");
        border.Classes.Set("invalid", !chip.Valid);
        chip.Container = border;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };

        var avatar = Avatar(chip.Initials, 22);
        row.Children.Add(avatar);

        var label = new TextBlock
        {
            Text = chip.Name ?? chip.Text,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 180,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        label.Classes.Add("sa-chip-label");
        row.Children.Add(label);

        if (!chip.Valid)
        {
            var alert = new ArcIcon { Icon = "circle-alert", Size = 13, VerticalAlignment = VerticalAlignment.Center };
            alert.Classes.Add("sa-error-icon");
            row.Children.Add(alert);
        }

        var remove = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Compact = true,
            Size = ArcButtonSize.Sm,
            Width = 20,
            Height = 20,
            MinWidth = 20,
            MinHeight = 20,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(10),
            Content = new ArcIcon { Icon = "x", Size = 12 },
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = true
        };
        remove.Classes.Add("sa-chip-remove");
        ArcActionVisuals.SetName(remove, $"Remove {chip.Name ?? chip.Text}");
        remove.Click += (_, _) => RemoveChip(chip);
        row.Children.Add(remove);

        border.Child = row;
        return border;
    }

    private void RefreshChipClasses()
    {
        foreach (var chip in _chips)
        {
            if (chip.Container is not null)
            {
                chip.Container.Classes.Set("marked", ReferenceEquals(chip, _markedChip));
            }
        }
    }

    private void UpdatePlaceholder()
    {
        if (_placeholder is null)
        {
            return;
        }

        _placeholder.IsVisible = _chips.Count == 0 && string.IsNullOrEmpty(_textBox?.Text);
    }

    private void ShowError(bool visible, string? message)
    {
        _headsUp = visible;
        if (_errorRow is not null)
        {
            _errorRow.IsVisible = visible;
        }

        if (_errorText is not null)
        {
            _errorText.Text = message ?? string.Empty;
        }

        _fieldShell?.Classes.Set("invalid", visible);
        if (_textBox is not null)
        {
            AutomationProperties.SetHelpText(_textBox, visible ? message ?? string.Empty : string.Empty);
        }
    }

    /// <summary>Keeps the error line in step with the chips that are still invalid.</summary>
    private void RefreshChipError()
    {
        var invalid = _chips.LastOrDefault(chip => !chip.Valid);
        if (invalid is not null)
        {
            ShowError(true, $"“{invalid.Text}” is not a valid email address. Use the form name@company.com.");
        }
        else
        {
            ShowError(false, null);
        }
    }

    private bool HasValidChips => _chips.Any(chip => chip.Valid);

    private void UpdateInviteState()
    {
        if (_inviteButton is not null)
        {
            _inviteButton.IsEnabled = HasValidChips;
        }
    }

    // ---------------------------------------------------------------- suggestions

    private void UpdateSuggestions(string text)
    {
        if (_overlay is null)
        {
            return;
        }

        var directory = (Suggestions ?? new List<ArcShareSuggestion>()).Where(item =>
        {
            if (string.IsNullOrWhiteSpace(item.Email))
            {
                return false;
            }

            if ((People ?? new List<ArcSharePerson>())
                .Any(person => string.Equals(person.Email, item.Email, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return !_chips.Any(chip =>
                string.Equals(chip.Email, item.Email, StringComparison.OrdinalIgnoreCase));
        });

        var query = text.Trim();
        var matches = directory.Where(item =>
                (!string.IsNullOrWhiteSpace(item.DisplayName) &&
                 item.DisplayName!.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                     .Any(part => part.StartsWith(query, StringComparison.OrdinalIgnoreCase))) ||
                (item.Email?.StartsWith(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (!string.IsNullOrWhiteSpace(item.DisplayName) &&
                 item.DisplayName!.StartsWith(query, StringComparison.OrdinalIgnoreCase)))
            .Take(5)
            .ToList();

        if (matches.Count == 0)
        {
            CloseSuggestions();
            return;
        }

        if (_suggestions is null)
        {
            _suggestions = new Border { Classes = { "sa-suggest" }, IsHitTestVisible = true };
            _suggestionRows = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
            _suggestions.Child = _suggestionRows;
            _overlay.Children.Add(_suggestions);
        }

        _suggestionRows!.Children.Clear();
        _suggestionButtons.Clear();

        for (var i = 0; i < matches.Count; i++)
        {
            var index = i;
            var person = matches[i];
            var button = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(8, 6),
                MinHeight = 40,
                CornerRadius = new CornerRadius(12),
                IsTabStop = false
            };
            button.Classes.Add("sa-suggest-row");

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(Avatar(person.Initials ?? ArcShareVisuals.Initials(person.DisplayName ?? person.Email), 28));

            var textStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = person.DisplayName ?? person.Email,
                Classes = { "arc-body-sm", "arc-medium" }
            });
            textStack.Children.Add(new TextBlock
            {
                Text = person.Email,
                Classes = { "arc-caption" }
            });
            row.Children.Add(textStack);

            button.Content = row;
            ArcActionVisuals.SetName(button, person.DisplayName ?? person.Email ?? "Suggestion");
            button.Click += (_, _) => AcceptSuggestion(index);
            button.PointerEntered += (_, _) => SetSuggestionIndex(index);
            _suggestionButtons.Add(button);
            _suggestionRows.Children.Add(button);
        }

        SetSuggestionIndex(0);
        _suggestions.Measure(new Size(Math.Max(240, _fieldShell?.Bounds.Width ?? 320), double.PositiveInfinity));
        var size = _suggestions.DesiredSize;
        _suggestions.Width = Math.Max(240, _fieldShell?.Bounds.Width ?? 320);
        _suggestions.Height = size.Height;
        PositionSuggestions();
    }

    private void PositionSuggestions()
    {
        if (_suggestions is null || _overlay is null || _fieldShell is null)
        {
            return;
        }

        var origin = _fieldShell.TranslatePoint(new Point(0, 0), _overlay);
        if (origin is null)
        {
            return;
        }

        var x = origin.Value.X;
        var y = origin.Value.Y + _fieldShell.Bounds.Height + 4;
        var width = _suggestions.Width;
        var maxX = Math.Max(0, (_overlay.Bounds.Width > 0 ? _overlay.Bounds.Width : (Bounds.Width - 40)) - width);
        Canvas.SetLeft(_suggestions, Math.Clamp(x, 0, maxX));
        Canvas.SetTop(_suggestions, y);
    }

    private void MoveSuggestion(int delta)
    {
        if (_suggestionButtons.Count == 0)
        {
            return;
        }

        var next = (_suggestionIndex + delta + _suggestionButtons.Count) % _suggestionButtons.Count;
        SetSuggestionIndex(next);
    }

    private void SetSuggestionIndex(int index)
    {
        _suggestionIndex = index;
        for (var i = 0; i < _suggestionButtons.Count; i++)
        {
            _suggestionButtons[i].Classes.Set("active", i == index);
        }
    }

    private void AcceptSuggestion(int index)
    {
        if (index < 0 || index >= _suggestionButtons.Count)
        {
            return;
        }

        var person = SelectSuggestion(index);
        if (person is null)
        {
            return;
        }

        SetFieldText(string.Empty);
        _chips.Add(new Chip(person.DisplayName ?? person.Email ?? "Person", person.Email, person.Initials, true));
        RebuildChips();
        CloseSuggestions();
        UpdateInviteState();
        _textBox?.Focus();
    }

    private ArcShareSuggestion? SelectSuggestion(int index)
    {
        // Rebuild the same filtered list used to build the rows.
        if (_suggestionButtons.Count == 0 || index < 0)
        {
            return null;
        }

        var text = _textBox?.Text?.Trim() ?? string.Empty;
        var directory = (Suggestions ?? new List<ArcShareSuggestion>()).Where(item =>
            !string.IsNullOrWhiteSpace(item.Email) &&
            !(People ?? new List<ArcSharePerson>())
                .Any(person => string.Equals(person.Email, item.Email, StringComparison.OrdinalIgnoreCase)) &&
            !_chips.Any(chip => string.Equals(chip.Email, item.Email, StringComparison.OrdinalIgnoreCase)) &&
            ((!string.IsNullOrWhiteSpace(item.DisplayName) &&
              item.DisplayName!.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                  .Any(part => part.StartsWith(text, StringComparison.OrdinalIgnoreCase))) ||
             (item.Email?.StartsWith(text, StringComparison.OrdinalIgnoreCase) ?? false)));

        return directory.Take(5).ElementAtOrDefault(index);
    }

    private void CloseSuggestions()
    {
        if (_suggestions is not null && _overlay is not null)
        {
            _overlay.Children.Remove(_suggestions);
        }

        _suggestions = null;
        _suggestionRows = null;
        _suggestionButtons.Clear();
        _suggestionIndex = -1;
    }

    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_suggestions is null)
        {
            return;
        }

        if (e.Source is Visual source &&
            (IsWithin(source, _suggestions) || IsWithin(source, _fieldShell)))
        {
            return;
        }

        CloseSuggestions();
    }

    private static bool IsWithin(Visual? source, Visual? ancestor)
    {
        for (var visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (ReferenceEquals(visual, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------- invite

    private void SendInvite()
    {
        var valid = _chips.Where(chip => chip.Valid && !string.IsNullOrWhiteSpace(chip.Email)).ToList();
        if (valid.Count == 0)
        {
            var hasInvalid = _chips.Any(chip => !chip.Valid);
            ShowError(true, hasInvalid
                ? "Fix the highlighted address before inviting."
                : "Add at least one email address to invite.");
            return;
        }

        CommitUndo();
        var list = People ??= new List<ArcSharePerson>();
        var people = new List<ArcShareSuggestion>();
        var added = new List<ArcSharePerson>();
        foreach (var chip in valid)
        {
            var person = new ArcSharePerson
            {
                DisplayName = chip.Name ?? chip.Email,
                Email = chip.Email,
                Role = InviteRole ?? "editor",
                IsPending = true
            };
            list.Add(person);
            added.Add(person);
            people.Add(new ArcShareSuggestion(chip.Name ?? chip.Email ?? string.Empty, chip.Email ?? string.Empty, chip.Initials));
        }

        _chips.Clear();
        RebuildChips();
        ShowError(false, null);
        BuildPeople();
        HighlightNew(added);
        UpdateInviteState();

        Invite?.Invoke(this, new ArcShareInviteEventArgs(people, InviteRole));
        PeopleChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CommitUndo()
    {
        _undoTimer.Stop();
        _commitTimer.Stop();
        if (_pendingContainer is not null && _peopleHost is not null)
        {
            _peopleHost.Children.Remove(_pendingContainer);
        }

        _pendingContainer = null;
        _pendingPerson = null;
        _pendingIndex = -1;
        HideUndo();
        BuildPeople();
    }

    private void ShowUndo(ArcSharePerson person)
    {
        if (_undoBar is null || _undoText is null)
        {
            return;
        }

        _undoText.Text = $"Removed {person.DisplayName ?? person.Email}";
        _undoBar.IsVisible = true;
        _undoBar.Height = 0;
        _undoBar.Opacity = 0;
        if (!ArcThemeManager.ReducedMotion)
        {
            _undoBar.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = HeightProperty,
                    Duration = ArcMotion.SpringSmoothDuration,
                    Easing = ArcMotion.SpringSmooth
                },
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = ArcMotion.Fast,
                    Easing = ArcMotion.EaseStandard
                }
            };
        }
        else
        {
            _undoBar.Transitions = null;
        }

        _undoBar.Height = 44;
        _undoBar.Opacity = 1;
        _undoTimer.Stop();
        _undoTimer.Start();
        AutomationProperties.SetName(_undoBar, _undoText.Text);
    }

    private void HideUndo()
    {
        if (_undoBar is null)
        {
            return;
        }

        _undoTimer.Stop();
        if (ArcThemeManager.ReducedMotion)
        {
            _undoBar.IsVisible = false;
            _undoBar.Height = 0;
            _undoBar.Opacity = 0;
            return;
        }

        _undoBar.Height = 0;
        _undoBar.Opacity = 0;
    }

    // ---------------------------------------------------------------- people

    private readonly Dictionary<ArcSharePerson, Border> _rowContainers = new();
    private readonly Dictionary<ArcSharePerson, ArcButton> _rowTriggers = new();
    private readonly List<ArcButton> _rowButtons = new();

    private void BuildPeople()
    {
        if (_peopleHost is null)
        {
            return;
        }

        _peopleHost.Children.Clear();
        _rowContainers.Clear();
        _rowTriggers.Clear();
        _rowButtons.Clear();

        foreach (var person in People ?? new List<ArcSharePerson>())
        {
            var container = BuildPersonRow(person);
            _rowContainers[person] = container;
            _peopleHost.Children.Add(container);
        }

        UpdateCount();
    }

    private Border BuildPersonRow(ArcSharePerson person)
    {
        var owner = IsOwner(person);
        var isSelf = !string.IsNullOrWhiteSpace(SelfEmail) &&
                     string.Equals(person.Email, SelfEmail, StringComparison.OrdinalIgnoreCase);

        var container = new Border { ClipToBounds = true, Height = double.NaN };
        container.Classes.Add("sa-row");

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            Margin = new Thickness(0, 7),
            VerticalAlignment = VerticalAlignment.Center
        };

        var avatar = Avatar(ArcShareVisuals.Initials(person.DisplayName ?? person.Email), 32);
        avatar.Margin = new Thickness(0, 0, 10, 0);
        avatar.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(avatar, 0);
        grid.Children.Add(avatar);

        var identity = new StackPanel { Orientation = Orientation.Vertical, Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        nameRow.Children.Add(new TextBlock
        {
            Text = person.DisplayName ?? person.Email ?? "Person",
            Classes = { "arc-body-sm", "arc-medium" },
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 190
        });
        if (isSelf)
        {
            nameRow.Children.Add(new TextBlock { Text = "(you)", Classes = { "arc-caption" } });
        }

        if (person.IsPending)
        {
            nameRow.Children.Add(new ArcBadge
            {
                Content = "Pending",
                Tone = ArcBadgeTone.Neutral,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        identity.Children.Add(nameRow);
        identity.Children.Add(new TextBlock
        {
            Text = person.Email,
            Classes = { "arc-caption" },
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 200
        });
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);

        var roleLabel = RoleLabel(person.Role ?? "viewer");
        var role = RoleButton(roleLabel);
        role.IsEnabled = !owner;
        AutomationProperties.SetName(role, $"Role for {person.DisplayName ?? person.Email}: {roleLabel}");
        role.Click += (_, _) => OpenRoleMenuFor(person, role);
        role.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(role, 2);
        grid.Children.Add(role);
        _rowTriggers[person] = role;
        _rowButtons.Add(role);

        var remove = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Compact = true,
            Size = ArcButtonSize.Sm,
            Width = 30,
            Height = 30,
            MinWidth = 30,
            MinHeight = 30,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(4, 0, 0, 0),
            Content = new ArcIcon { Icon = "x", Size = 14 },
            IsVisible = !owner && !isSelf,
            VerticalAlignment = VerticalAlignment.Center
        };
        remove.Classes.Add("sa-remove");
        ArcActionVisuals.SetName(remove, $"Remove {person.DisplayName ?? person.Email}");
        remove.Click += (_, _) => RemovePerson(person.Email ?? string.Empty);
        Grid.SetColumn(remove, 3);
        grid.Children.Add(remove);

        container.Child = grid;
        container.PointerEntered += (_, _) => container.Classes.Set("hover", true);
        container.PointerExited += (_, _) => container.Classes.Set("hover", false);
        return container;
    }

    private void UpdateCount()
    {
        if (_peopleCount is null)
        {
            return;
        }

        var count = People?.Count ?? 0;
        _peopleCount.Text = count == 1 ? "1 person" : $"{count} people";
    }

    private void HighlightNew(IEnumerable<ArcSharePerson> people)
    {
        if (ArcThemeManager.ReducedMotion || _peopleHost is null)
        {
            return;
        }

        foreach (var person in people)
        {
            if (!_rowContainers.TryGetValue(person, out var container))
            {
                continue;
            }

            container.Background = ArcActionVisuals.Brush(ArcTokens.AccentSubtle);
            container.Transitions = new Transitions
            {
                new BrushTransition
                {
                    Property = BackgroundProperty,
                    Duration = ArcMotion.Considered
                }
            };
            Dispatcher.UIThread.Post(() => container.Background = Brushes.Transparent, DispatcherPriority.Background);
        }
    }

    private bool IsOwner(ArcSharePerson person) =>
        RolesOption(person.Role)?.IsOwner == true;

    // ---------------------------------------------------------------- roles and access

    private List<ArcShareRoleOption> RoleList() =>
        Roles?.ToList() ?? new List<ArcShareRoleOption>
        {
            new("owner", "Owner", isOwner: true, allowForLink: false),
            new("editor", "Can edit"),
            new("commenter", "Can comment"),
            new("viewer", "Can view")
        };

    private ArcShareRoleOption? RolesOption(string? value) =>
        RoleList().FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase));

    private string RoleLabel(string? value) => RolesOption(value)?.Label ?? value ?? "Can view";

    private static ArcButton RoleButton(string label)
    {
        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Height = 30,
            MinHeight = 30,
            Padding = new Thickness(8, 0),
            CornerRadius = new CornerRadius(10),
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("sa-role");
        button.Content = RoleButtonContent(label);
        return button;
    }

    private static Control RoleButtonContent(string label)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        });
        var chevron = new ArcIcon { Icon = "chevron-down", Size = 13, VerticalAlignment = VerticalAlignment.Center };
        chevron.Classes.Add("sa-chevron");
        row.Children.Add(chevron);
        return row;
    }

    private void UpdateRoleFaces()
    {
        if (_linkRoleButton is not null)
        {
            _linkRoleButton.Content = RoleButtonContent(RoleLabel(LinkRole ?? "viewer"));
            ArcActionVisuals.SetName(_linkRoleButton, $"Anyone with the link: {RoleLabel(LinkRole ?? "viewer")}");
            _linkRoleButton.Width = double.NaN;
        }

        if (_inviteRoleButton is not null)
        {
            _inviteRoleButton.Content = RoleButtonContent(RoleLabel(InviteRole ?? "editor"));
            ArcActionVisuals.SetName(_inviteRoleButton, $"Role for new invites: {RoleLabel(InviteRole ?? "editor")}");
            _inviteRoleButton.Width = double.NaN;
        }
    }

    private void SyncAccess(bool animate)
    {
        var anyone = LinkAccess == ArcShareLinkAccess.Anyone;
        if (_accessTitle is not null)
        {
            _accessTitle.Text = anyone ? "Anyone with the link" : "Restricted";
        }

        if (_accessDescription is not null)
        {
            _accessDescription.Text = anyone
                ? $"{RoleLabel(LinkRole ?? "viewer")} anyone who has the link, even without an invite."
                : "Only people with access can open the link.";
        }

        if (_linkRoleRow is not null)
        {
            _linkRoleRow.IsVisible = anyone;
        }

        if (_accessButton is not null)
        {
            _accessButton.Content = "Change";
            ArcActionVisuals.SetName(_accessButton, "Change general access");
        }

        if (_accessIcon is not null)
        {
            var icon = anyone ? "globe" : "lock";
            if (!string.Equals(_accessIcon.Icon, icon, StringComparison.Ordinal))
            {
                _accessIcon.Icon = icon;
                if (animate && !ArcThemeManager.ReducedMotion)
                {
                    _accessIcon.RenderTransformOrigin = RelativePoint.Center;
                    _accessIcon.RenderTransform = TransformOperations.Parse("rotate(-18deg)");
                    _accessIcon.Transitions = new Transitions
                    {
                        new TransformOperationsTransition
                        {
                            Property = RenderTransformProperty,
                            Duration = ArcMotion.SpringSmoothDuration,
                            Easing = ArcMotion.SpringSmooth
                        }
                    };
                    Dispatcher.UIThread.Post(
                        () => _accessIcon.RenderTransform = TransformOperations.Parse("rotate(0deg)"),
                        DispatcherPriority.Render);
                }
            }
        }

        if (_accessIconBox is not null)
        {
            _accessIconBox.Classes.Set("anyone", anyone);
        }
    }

    private void SetAccess(ArcShareLinkAccess access)
    {
        if (LinkAccess == access)
        {
            return;
        }

        SetCurrentValue(LinkAccessProperty, access);
        LinkAccessChanged?.Invoke(this, access);
    }

    // ---------------------------------------------------------------- menus

    private void OpenRoleMenuFor(ArcSharePerson person, ArcButton trigger)
    {
        if (IsOwner(person))
        {
            return;
        }

        var items = RoleList()
            .Where(option => !option.IsOwner)
            .Select(option => new ArcMenuItem
            {
                Label = option.Label,
                IsChecked = string.Equals(option.Value, person.Role, StringComparison.OrdinalIgnoreCase),
                Action = () => SetRole(person, option.Value)
            })
            .ToList();

        OpenMenu(
            items,
            $"Role for {person.DisplayName ?? person.Email}: {RoleLabel(person.Role)}",
            trigger,
            focusSelected: true);
    }

    private void OpenRoleMenuForInvite()
    {
        var items = RoleList()
            .Where(option => !option.IsOwner)
            .Select(option => new ArcMenuItem
            {
                Label = option.Label,
                IsChecked = string.Equals(option.Value, InviteRole, StringComparison.OrdinalIgnoreCase),
                Action = () => SetCurrentValue(InviteRoleProperty, option.Value)
            })
            .ToList();

        OpenMenu(items, $"Role for new invites: {RoleLabel(InviteRole)}", _inviteRoleButton, focusSelected: true);
    }

    private void OpenRoleMenuForLink()
    {
        var items = RoleList()
            .Where(option => option.AllowForLink && !option.IsOwner)
            .Select(option => new ArcMenuItem
            {
                Label = option.Label,
                IsChecked = string.Equals(option.Value, LinkRole, StringComparison.OrdinalIgnoreCase),
                Action = () => SetCurrentValue(LinkRoleProperty, option.Value)
            })
            .ToList();

        OpenMenu(items, $"Anyone with the link: {RoleLabel(LinkRole)}", _linkRoleButton, focusSelected: true);
    }

    private void OpenAccessMenuCore()
    {
        var items = new List<ArcMenuItem>
        {
            new()
            {
                Label = "Restricted",
                IsChecked = LinkAccess == ArcShareLinkAccess.Restricted,
                Action = () => SetAccess(ArcShareLinkAccess.Restricted)
            },
            new()
            {
                Label = "Anyone with the link",
                IsChecked = LinkAccess == ArcShareLinkAccess.Anyone,
                Action = () => SetAccess(ArcShareLinkAccess.Anyone)
            }
        };

        OpenMenu(items, "General access", _accessButton, focusSelected: true);
    }

    private void OpenMenu(IList<ArcMenuItem> items, string label, Control? trigger, bool focusSelected)
    {
        if (_overlay is null || trigger is null)
        {
            return;
        }

        // A press on the trigger dismisses the open menu before the click arrives; treat the pair as a toggle.
        if (_lastClosedTrigger == trigger &&
            (DateTime.UtcNow - _menuClosedAt).TotalMilliseconds < 240)
        {
            _lastClosedTrigger = null;
            return;
        }

        CloseMenu(returnFocus: false);
        var surface = new ArcMenuSurface
        {
            Items = items,
            Label = label,
            MinWidth = 188
        };
        surface.Closed += (_, _) => CloseMenu(returnFocus: true);
        _menu = surface;
        _menuTrigger = trigger;

        _overlay.Children.Add(surface);
        surface.Measure(Size.Infinity);
        var size = surface.DesiredSize;
        surface.Width = Math.Max(188, size.Width);
        if (size.Height > 0)
        {
            surface.Height = size.Height;
        }

        PositionMenu();
        surface.Open(focus: true);
        if (!focusSelected)
        {
            surface.Focus();
        }
    }

    /// <summary>
    /// Anchors an open menu to its trigger. Runs again after layout, because a page can open a menu before
    /// the trigger has bounds (the demo does, to present the open state in a still).
    /// </summary>
    private void PositionMenu()
    {
        if (_menu is null || _menuTrigger is null || _overlay is null)
        {
            return;
        }

        var trigger = _menuTrigger;
        if (trigger.Bounds.Width <= 0)
        {
            return;
        }

        var origin = trigger.TranslatePoint(new Point(0, 0), _overlay);
        if (origin is null)
        {
            return;
        }

        var width = _menu.Width > 0 ? _menu.Width : 188;
        var height = _menu.Height > 0 ? _menu.Height : _menu.DesiredSize.Height;
        var x = origin.Value.X + trigger.Bounds.Width - width;
        var y = origin.Value.Y + trigger.Bounds.Height + 4;
        var maxX = Math.Max(4, (_overlay.Bounds.Width > 0 ? _overlay.Bounds.Width : Bounds.Width) - width - 4);
        var maxY = Math.Max(0, (_overlay.Bounds.Height > 0 ? _overlay.Bounds.Height : Bounds.Height) - height - 4);
        Canvas.SetLeft(_menu, Math.Clamp(x, 4, maxX));
        Canvas.SetTop(_menu, Math.Clamp(y, 4, Math.Max(4, maxY)));
    }

    private void SetRole(ArcSharePerson person, string? role)
    {
        if (person.Role == role)
        {
            return;
        }

        person.Role = role;
        if (_rowTriggers.TryGetValue(person, out var trigger))
        {
            var label = RoleLabel(role);
            var previous = trigger.Bounds.Width;
            trigger.Content = RoleButtonContent(label);
            ArcActionVisuals.SetName(trigger, $"Role for {person.DisplayName ?? person.Email}: {label}");
            trigger.Measure(Size.Infinity);
            var target = trigger.DesiredSize.Width;
            if (!ArcThemeManager.ReducedMotion && previous > 0 && target > 0)
            {
                trigger.Width = previous;
                trigger.Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = WidthProperty,
                        Duration = ArcMotion.SpringMorphDuration,
                        Easing = ArcMotion.SpringMorph
                    }
                };
                Dispatcher.UIThread.Post(() => trigger.Width = target, DispatcherPriority.Render);
                _settleTimer.Stop();
                _settleTimer.Start();
            }
        }

        PeopleChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CloseMenu(bool returnFocus)
    {
        var surface = _menu;
        var trigger = _menuTrigger;
        _menu = null;
        _menuTrigger = null;
        if (surface is null)
        {
            return;
        }

        _overlay?.Children.Remove(surface);
        _lastClosedTrigger = trigger;
        _menuClosedAt = DateTime.UtcNow;
        if (returnFocus && trigger is not null && trigger.IsEffectivelyEnabled)
        {
            trigger.Focus();
        }
    }

    // ---------------------------------------------------------------- copy feedback

    private enum CopyState
    {
        Idle,
        Busy,
        Copied,
        Failed
    }

    private void SetCopyState(CopyState state)
    {
        if (_copyButton is null)
        {
            return;
        }

        var width = _copyButton.Bounds.Width > 0 ? _copyButton.Bounds.Width : 128;
        _copyButton.Width = width;
        _copyButton.Classes.Set("copied", state == CopyState.Copied);
        _copyButton.Classes.Set("failed", state == CopyState.Failed);

        if (_copyIcon is not null)
        {
            _copyIcon.Icon = state switch
            {
                CopyState.Copied => "check",
                CopyState.Failed => "circle-alert",
                _ => "copy"
            };
            _copyIcon.Classes.Set("copied", state == CopyState.Copied);
            _copyIcon.Classes.Set("failed", state == CopyState.Failed);
        }

        if (_copyLabel is not null)
        {
            _copyLabel.Text = state switch
            {
                CopyState.Busy => "Copying",
                CopyState.Copied => "Link copied",
                CopyState.Failed => "Couldn't copy",
                _ => "Copy link"
            };
        }

        AutomationProperties.SetName(_copyButton, _copyLabel?.Text ?? "Copy link");
        if (_copyLabel is not null)
        {
            AutomationProperties.SetLiveSetting(_copyLabel, AutomationLiveSetting.Polite);
            AutomationProperties.SetHelpText(_copyButton, state == CopyState.Failed
                ? "The clipboard is not available"
                : "Puts the link on the clipboard");
        }
    }

    // ---------------------------------------------------------------- helpers

    private static Border Avatar(string? initials, double size)
    {
        var border = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Classes.Add("sa-avatar");
        border.Child = new TextBlock
        {
            Text = initials,
            FontSize = size <= 24 ? 9 : 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ((TextBlock)border.Child).Classes.Add("sa-avatar-text");
        return border;
    }

    private sealed class Chip
    {
        public Chip(string text, string? email, string? initials, bool valid)
        {
            Text = text;
            Email = email;
            Initials = string.IsNullOrWhiteSpace(initials) ? ArcShareVisuals.Initials(text) : initials;
            Valid = valid;
            Name = email is null ? null : text;
        }

        public string Text { get; }

        public string? Email { get; }

        public string? Name { get; }

        public string? Initials { get; }

        public bool Valid { get; }

        public Border? Container { get; set; }
    }
}

/// <summary>Tiny shared helpers for the share panel; kept separate so pages can reuse the initials rule.</summary>
internal static class ArcShareVisuals
{
    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var trimmed = name.Trim();
        var at = trimmed.IndexOf('@');
        if (at > 0)
        {
            trimmed = trimmed[..at];
        }

        var parts = trimmed.Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "?";
        }

        if (parts.Length == 1)
        {
            return parts[0].Length >= 2
                ? parts[0][..2].ToUpperInvariant()
                : parts[0][..1].ToUpperInvariant();
        }

        return string.Concat(parts[0][..1], parts[^1][..1]).ToUpperInvariant();
    }
}



