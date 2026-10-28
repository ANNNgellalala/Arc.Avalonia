using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.AdvancedInputs.Controls;

public enum ArcMentionKind
{
    Person,
    Channel
}

public enum ArcMentionPlacement
{
    Auto,
    Top,
    Bottom
}

public sealed class ArcMentionPerson
{
    public ArcMentionPerson(string id, string name, string? role = null)
    {
        Id = id;
        Name = name;
        Role = role;
    }

    public string Id { get; }

    public string Name { get; }

    public string? Role { get; }
}

public sealed class ArcMentionChannel
{
    public ArcMentionChannel(string id, string name, string? description = null, int? members = null)
    {
        Id = id;
        Name = name;
        Description = description;
        Members = members;
    }

    public string Id { get; }

    public string Name { get; }

    public string? Description { get; }

    public int? Members { get; }
}

/// <summary>One token inside the text, with the offsets it occupies.</summary>
public sealed class ArcMention
{
    public ArcMention(ArcMentionKind kind, string id, string label, int start, int end)
    {
        Kind = kind;
        Id = id;
        Label = label;
        Start = start;
        End = end;
    }

    public ArcMentionKind Kind { get; }

    public string Id { get; }

    public string Label { get; }

    public int Start { get; internal set; }

    public int End { get; internal set; }

    /// <summary>The visible text of the mention, "@label" or "#label".</summary>
    public string Display => (Kind == ArcMentionKind.Person ? "@" : "#") + Label;
}

/// <summary>Plain text plus the mentions that sit inside it.</summary>
public sealed class ArcMentionValue
{
    public ArcMentionValue()
    {
        Text = string.Empty;
        Mentions = new List<ArcMention>();
    }

    public ArcMentionValue(string text, IList<ArcMention>? mentions = null)
    {
        Text = text;
        Mentions = mentions ?? new List<ArcMention>();
    }

    public string Text { get; set; }

    public IList<ArcMention> Mentions { get; set; }

    public override string ToString() => Text;
}

public sealed class ArcMentionValueEventArgs : EventArgs
{
    public ArcMentionValueEventArgs(ArcMentionValue value) => Value = value;

    public ArcMentionValue Value { get; }
}

/// <summary>
/// Arc's mention input: an autosizing field where @people and #channels become tokens, with a
/// suggestion list at the caret and keyboard control of the list.
/// </summary>
public class ArcMentionInput : TemplatedControl
{
    public static readonly StyledProperty<ArcMentionValue?> ValueProperty =
        AvaloniaProperty.Register<ArcMentionInput, ArcMentionValue?>(nameof(Value));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcMentionInput, string?>(nameof(Label));

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ArcMentionInput, string?>(nameof(Placeholder));

    public static readonly StyledProperty<IList<ArcMentionPerson>?> PeopleProperty =
        AvaloniaProperty.Register<ArcMentionInput, IList<ArcMentionPerson>?>(nameof(People));

    public static readonly StyledProperty<IList<ArcMentionChannel>?> ChannelsProperty =
        AvaloniaProperty.Register<ArcMentionInput, IList<ArcMentionChannel>?>(nameof(Channels));

    public static readonly StyledProperty<int> MinRowsProperty =
        AvaloniaProperty.Register<ArcMentionInput, int>(nameof(MinRows), 1);

    public static readonly StyledProperty<int> MaxRowsProperty =
        AvaloniaProperty.Register<ArcMentionInput, int>(nameof(MaxRows), 8);

    public static readonly StyledProperty<bool> SubmitOnEnterProperty =
        AvaloniaProperty.Register<ArcMentionInput, bool>(nameof(SubmitOnEnter));

    public static readonly StyledProperty<int> MaxSuggestionsProperty =
        AvaloniaProperty.Register<ArcMentionInput, int>(nameof(MaxSuggestions), 6);

    public static readonly StyledProperty<ArcMentionPlacement> PlacementProperty =
        AvaloniaProperty.Register<ArcMentionInput, ArcMentionPlacement>(nameof(Placement));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ArcMentionInput, string?>(nameof(Description));

    private Border? _shell;
    private TextBlock? _mirror;
    private TextBox? _box;
    private TextBlock? _message;
    private Border? _panel;
    private StackPanel? _rows;
    private ScrollViewer? _scroller;

    private readonly List<Suggestion> _suggestions = new();
    private int _suggestionIndex;
    private bool _panelOpen;
    private int _triggerIndex = -1;
    private ArcMentionKind _triggerKind;
    private bool _syncing;
    private string _text = string.Empty;
    private readonly List<ArcMention> _mentions = new();

    static ArcMentionInput()
    {
        ValueProperty.Changed.AddClassHandler<ArcMentionInput>((input, _) => input.SyncFromValue());
        PlaceholderProperty.Changed.AddClassHandler<ArcMentionInput>((input, _) => input.UpdatePlaceholder());
        MinRowsProperty.Changed.AddClassHandler<ArcMentionInput>((input, _) => input.ApplyRows());
        MaxRowsProperty.Changed.AddClassHandler<ArcMentionInput>((input, _) => input.ApplyRows());
        LabelProperty.Changed.AddClassHandler<ArcMentionInput>((input, _) => input.UpdateAutomation());
        PeopleProperty.Changed.AddClassHandler<ArcMentionInput>((input, _) => input.RefreshSuggestions());
        ChannelsProperty.Changed.AddClassHandler<ArcMentionInput>((input, _) => input.RefreshSuggestions());
    }

    public ArcMentionInput()
    {
        ClipToBounds = false;
    }

    /// <summary>Raised on every edit with the text and the mentions moved to match it.</summary>
    public event EventHandler<ArcMentionValueEventArgs>? ValueChanged;

    /// <summary>Raised when a suggestion becomes a mention.</summary>
    public event EventHandler<ArcMentionValueEventArgs>? MentionAdded;

    /// <summary>Raised on Enter when <see cref="SubmitOnEnter"/> is set and the text is not blank.</summary>
    public event EventHandler<ArcMentionValueEventArgs>? Submitted;

    public ArcMentionValue? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>People offered after @; leave it out to turn person mentions off.</summary>
    public IList<ArcMentionPerson>? People
    {
        get => GetValue(PeopleProperty);
        set => SetValue(PeopleProperty, value);
    }

    /// <summary>Channels offered after #; leave it out to turn channel mentions off.</summary>
    public IList<ArcMentionChannel>? Channels
    {
        get => GetValue(ChannelsProperty);
        set => SetValue(ChannelsProperty, value);
    }

    public int MinRows
    {
        get => GetValue(MinRowsProperty);
        set => SetValue(MinRowsProperty, value);
    }

    public int MaxRows
    {
        get => GetValue(MaxRowsProperty);
        set => SetValue(MaxRowsProperty, value);
    }

    public bool SubmitOnEnter
    {
        get => GetValue(SubmitOnEnterProperty);
        set => SetValue(SubmitOnEnterProperty, value);
    }

    public int MaxSuggestions
    {
        get => GetValue(MaxSuggestionsProperty);
        set => SetValue(MaxSuggestionsProperty, value);
    }

    public ArcMentionPlacement Placement
    {
        get => GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>The mentions in the current text, with their offsets.</summary>
    public IList<ArcMention> Mentions => _mentions;

    /// <summary>Focuses the text field.</summary>
    public void FocusInput() => _box?.Focus();

    /// <summary>Inserts text at the caret as if typed.</summary>
    public void Insert(string text)
    {
        if (_box is null)
        {
            ApplyTemplate();
        }

        if (_box is null || string.IsNullOrEmpty(text))
        {
            return;
        }

        _text = _box.Text ?? string.Empty;
        var caret = Math.Clamp(_box.CaretIndex, 0, _text.Length);
        var updated = _text.Substring(0, caret) + text + _text.Substring(caret);
        _box.Text = updated;
        _box.CaretIndex = caret + text.Length;
    }

    /// <summary>Types @ or # at the caret so the suggestion list opens.</summary>
    public void OpenSuggestions(ArcMentionKind kind)
    {
        if (_box is null)
        {
            ApplyTemplate();
        }

        if (_box is null)
        {
            return;
        }

        _text = _box.Text ?? string.Empty;
        var caret = Math.Clamp(_box.CaretIndex, 0, _text.Length);
        var needsSpace = caret > 0 && !char.IsWhiteSpace(_text[caret - 1]);
        Insert((needsSpace ? " " : string.Empty) + (kind == ArcMentionKind.Person ? "@" : "#"));
        _box.Focus();
    }

    /// <summary>Empties the text and the mentions.</summary>
    public void Clear()
    {
        _text = string.Empty;
        _mentions.Clear();
        if (_box is not null)
        {
            _box.Text = string.Empty;
        }

        UpdateMirror();
        EmitChange();
    }

    /// <summary>Turns a value into storage friendly text, &lt;@id&gt; for people and &lt;#id&gt; for channels.</summary>
    public static string SerializeMentions(ArcMentionValue value, Func<ArcMention, string>? format = null)
    {
        var builder = new System.Text.StringBuilder();
        var index = 0;
        foreach (var mention in value.Mentions.OrderBy(entry => entry.Start))
        {
            if (mention.Start > index && mention.Start <= value.Text.Length)
            {
                builder.Append(value.Text, index, mention.Start - index);
            }

            builder.Append(format?.Invoke(mention) ??
                           (mention.Kind == ArcMentionKind.Person ? $"<@{mention.Id}>" : $"<#{mention.Id}>"));
            index = Math.Min(mention.End, value.Text.Length);
        }

        if (index < value.Text.Length)
        {
            builder.Append(value.Text, index, value.Text.Length - index);
        }

        return builder.ToString();
    }

    /// <summary>The visible text of a mention, "@label" or "#label".</summary>
    public static string MentionText(ArcMentionKind kind, string label) =>
        (kind == ArcMentionKind.Person ? "@" : "#") + label;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_box is not null)
        {
            _box.TextChanged -= OnTextChanged;
            _box.RemoveHandler(KeyDownEvent, OnBoxKeyDown);
            _box.GotFocus -= OnBoxGotFocus;
            _box.LostFocus -= OnBoxLostFocus;
        }

        _shell = e.NameScope.Find<Border>("Shell");
        _mirror = e.NameScope.Find<TextBlock>("Mirror");
        _box = e.NameScope.Find<TextBox>("PART_Input");
        _message = e.NameScope.Find<TextBlock>("Message");
        _panel = e.NameScope.Find<Border>("Panel");
        _rows = e.NameScope.Find<StackPanel>("Rows");
        _scroller = e.NameScope.Find<ScrollViewer>("Scroller");

        if (_box is not null)
        {
            _box.TextChanged += OnTextChanged;
            _box.AddHandler(KeyDownEvent, OnBoxKeyDown, RoutingStrategies.Tunnel);
            _box.GotFocus += OnBoxGotFocus;
            _box.LostFocus += OnBoxLostFocus;
            AutomationProperties.SetName(_box, Label ?? "Message");
            AutomationProperties.SetHelpText(_box, "Type @ to mention a person or # to mention a channel.");
            _box.Text = _text;
        }

        ApplyRows();
        UpdatePlaceholder();
        UpdateMirror();
        UpdateMessage();
        UpdateAutomation();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            Classes.Set("disabled", !IsEnabled);
        }
        else if (change.Property == BoundsProperty)
        {
            PositionPanel();
        }
    }

    private void OnBoxGotFocus(object? sender, FocusChangedEventArgs e) => Classes.Set("focus", true);

    private void OnBoxLostFocus(object? sender, FocusChangedEventArgs e)
    {
        Classes.Set("focus", false);
    }

    private void ApplyRows()
    {
        if (_box is null)
        {
            return;
        }

        var line = 20d;
        _box.MinHeight = Math.Max(1, MinRows) * line + 4;
        _box.MaxHeight = Math.Max(MinRows, MaxRows) * line + 4;
    }

    private void UpdatePlaceholder()
    {
        if (_box is not null)
        {
            _box.PlaceholderText = Placeholder;
        }
    }

    private void SyncFromValue()
    {
        var value = Value;
        _text = value?.Text ?? string.Empty;
        _mentions.Clear();
        if (value?.Mentions is { } mentions)
        {
            foreach (var mention in mentions)
            {
                _mentions.Add(mention);
            }
        }

        if (_box is not null && !string.Equals(_box.Text, _text, StringComparison.Ordinal))
        {
            _syncing = true;
            _box.Text = _text;
            _syncing = false;
        }

        UpdateMirror();
        UpdateMessage();
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing || _box is null)
        {
            return;
        }

        _text = _box.Text ?? string.Empty;
        ReconcileMentions();
        UpdateMirror();
        DetectTrigger();
        UpdateMessage();
        EmitChange();
    }

    private void EmitChange()
    {
        var value = new ArcMentionValue(_text, _mentions.ToList());
        SetCurrentValue(ValueProperty, value);
        ValueChanged?.Invoke(this, new ArcMentionValueEventArgs(value));
        UpdateAutomation();
    }

    private void ReconcileMentions()
    {
        for (var i = _mentions.Count - 1; i >= 0; i--)
        {
            var mention = _mentions[i];
            var display = mention.Display;
            if (mention.Start < 0 || mention.End > _text.Length ||
                mention.End - mention.Start != display.Length ||
                string.CompareOrdinal(_text, mention.Start, display, 0, display.Length) != 0)
            {
                _mentions.RemoveAt(i);
            }
        }
    }

    private void UpdateMirror()
    {
        if (_mirror is null)
        {
            return;
        }

        var inlines = new InlineCollection();
        var index = 0;
        foreach (var mention in _mentions.OrderBy(entry => entry.Start))
        {
            if (mention.Start > index && mention.Start <= _text.Length)
            {
                inlines.Add(new Run(_text.Substring(index, mention.Start - index))
                {
                    Foreground = ArcResources.Brush(ArcTokens.Foreground)
                });
            }

            var end = Math.Min(mention.End, _text.Length);
            inlines.Add(new Run(_text.Substring(mention.Start, Math.Max(0, end - mention.Start)))

            {

                Foreground = ArcResources.Brush(ArcTokens.AccentStrong),

                FontWeight = FontWeight.Medium,

                TextDecorations = TextDecorations.Underline

            });
            index = end;
        }

        if (index < _text.Length)
        {
            inlines.Add(new Run(_text.Substring(index))
            {
                Foreground = ArcResources.Brush(ArcTokens.Foreground)
            });
        }

        _mirror.Inlines = inlines;
    }

    // ------------------------------------------------------------ suggestions

    private void DetectTrigger()
    {
        if (_box is null)
        {
            return;
        }

        var caret = Math.Clamp(_box.CaretIndex, 0, _text.Length);
        var trigger = -1;
        var kind = ArcMentionKind.Person;

        for (var i = caret - 1; i >= 0; i--)
        {
            var c = _text[i];
            if (c is '@' or '#')
            {
                var before = i == 0 ? ' ' : _text[i - 1];
                if (i == 0 || char.IsWhiteSpace(before) || before is '(' or '[' or '"' or '\'' or '>' or ':' or '-')
                {
                    trigger = i;
                    kind = c == '@' ? ArcMentionKind.Person : ArcMentionKind.Channel;
                }

                break;
            }

            if (char.IsWhiteSpace(c) || c is ',' or '.' or '!' or '?' or ')')
            {
                break;
            }
        }

        if (trigger < 0)
        {
            ClosePanel();
            return;
        }

        _triggerIndex = trigger;
        _triggerKind = kind;
        BuildSuggestions(_text.Substring(trigger + 1, caret - trigger - 1));
    }

    private void RefreshSuggestions()
    {
        if (_panelOpen && _triggerIndex >= 0 && _box is not null)
        {
            var caret = Math.Clamp(_box.CaretIndex, 0, _text.Length);
            BuildSuggestions(_text.Substring(_triggerIndex + 1, Math.Max(0, caret - _triggerIndex - 1)));
        }
    }

    private void BuildSuggestions(string query)
    {
        _suggestions.Clear();
        var kind = _triggerKind;

        if (kind == ArcMentionKind.Person && People is { } people)
        {
            foreach (var person in people)
            {
                var rank = Rank(person.Name, query, person.Role);
                if (rank >= 0)
                {
                    _suggestions.Add(new Suggestion(kind, person, null, rank));
                }
            }
        }
        else if (kind == ArcMentionKind.Channel && Channels is { } channels)
        {
            foreach (var channel in channels)
            {
                var rank = Rank(channel.Name, query, channel.Description);
                if (rank >= 0)
                {
                    _suggestions.Add(new Suggestion(kind, null, channel, rank));
                }
            }
        }

        _suggestions.Sort((left, right) => left.Rank.CompareTo(right.Rank));
        if (_suggestions.Count > Math.Max(1, MaxSuggestions))
        {
            _suggestions.RemoveRange(Math.Max(1, MaxSuggestions), _suggestions.Count - Math.Max(1, MaxSuggestions));
        }

        _suggestionIndex = Math.Clamp(_suggestionIndex, 0, Math.Max(0, _suggestions.Count - 1));
        RenderSuggestions();
    }

    private static int Rank(string name, string query, string? extra)
    {
        if (string.IsNullOrEmpty(query))
        {
            return 4;
        }

        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (name.Split(' ', '-', '_').Any(word => word.StartsWith(query, StringComparison.OrdinalIgnoreCase)))
        {
            return 1;
        }

        if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return extra is not null && extra.Contains(query, StringComparison.OrdinalIgnoreCase) ? 3 : -1;
    }

    private void RenderSuggestions()
    {
        if (_rows is null || _panel is null)
        {
            return;
        }

        _rows.Children.Clear();
        var hasAny = _suggestions.Count > 0;
        if (!hasAny)
        {
            _rows.Children.Add(new TextBlock
            {
                Text = "No matches",
                Classes = { "arc-caption" },
                Margin = new Thickness(12, 9)
            });
        }
        else
        {
            for (var i = 0; i < _suggestions.Count; i++)
            {
                _rows.Children.Add(CreateRow(_suggestions[i], i == _suggestionIndex));
            }
        }

        _panel.IsVisible = true;
        _panelOpen = true;
        PositionPanel();
        UpdateAutomation();
    }

    private Control CreateRow(Suggestion suggestion, bool active)
    {
        var title = suggestion.Person?.Name ?? suggestion.Channel?.Name ?? string.Empty;
        var detail = suggestion.Person?.Role ??
                     (suggestion.Channel is { } channel
                         ? channel.Members is { } members ? $"{members} members" : channel.Description
                         : null);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };

        var badge = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = ArcResources.Brush(ArcTokens.SurfaceMuted),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = title.Length > 0 ? title.Substring(0, 1).ToUpperInvariant() : "?",
                FontSize = 11,
                FontWeight = FontWeight.Medium,
                Foreground = ArcResources.Brush(ArcTokens.TextSecondary),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        grid.Children.Add(badge);

        var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = title, Classes = { "arc-body-sm" }, FontWeight = FontWeight.Medium });
        if (!string.IsNullOrWhiteSpace(detail))
        {
            text.Children.Add(new TextBlock { Text = detail, Classes = { "arc-caption" } });
        }

        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var kind = new TextBlock
        {
            Text = suggestion.Kind == ArcMentionKind.Person ? "@" : "#",
            Classes = { "arc-caption" },
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(kind, 2);
        grid.Children.Add(kind);

        var row = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 5),
            Background = active ? ArcResources.Brush(ArcTokens.SurfaceMuted) : Brushes.Transparent,
            Child = grid,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        row.PointerEntered += (_, _) =>
        {
            var index = _rows?.Children.IndexOf(row) ?? -1;
            if (index >= 0)
            {
                _suggestionIndex = index;
                RenderSuggestions();
            }
        };

        row.PointerPressed += (_, args) =>
        {
            args.Handled = true;
            Choose(suggestion);
        };

        return row;
    }

    private void PositionPanel()
    {
        if (_panel is null || _shell is null)
        {
            return;
        }

        var width = Math.Max(200, Math.Min(300, Bounds.Width));
        _panel.Width = width;
        var above = Placement == ArcMentionPlacement.Top;
        var offset = _shell.TranslatePoint(new Point(0, above ? -6 : _shell.Bounds.Height + 6), this) ?? new Point(0, 50);
        _panel.Margin = new Thickness(0, offset.Y, 0, 0);
        _panel.VerticalAlignment = above ? VerticalAlignment.Top : VerticalAlignment.Top;
        if (above)
        {
            _panel.Margin = new Thickness(0, offset.Y - 240, 0, 0);
        }
    }

    private void ClosePanel()
    {
        if (_panel is not null)
        {

            _panel.IsVisible = false;
        }

        _panelOpen = false;
        _suggestions.Clear();
        _triggerIndex = -1;
    }

    private void Choose(Suggestion suggestion)
    {
        if (_box is null || _triggerIndex < 0)
        {
            return;
        }

        var caret = Math.Clamp(_box.CaretIndex, 0, _text.Length);
        _text = _box.Text ?? string.Empty;

        var label = suggestion.Person?.Name ?? suggestion.Channel?.Name ?? string.Empty;
        var id = suggestion.Person?.Id ?? suggestion.Channel?.Id ?? string.Empty;
        var token = MentionText(suggestion.Kind, label) + " ";
        var start = _triggerIndex;

        var updated = _text.Substring(0, start) + token + _text.Substring(Math.Min(caret, _text.Length));
        for (var i = _mentions.Count - 1; i >= 0; i--)
        {
            if (_mentions[i].End > start)
            {
                _mentions.RemoveAt(i);
            }
        }

        _mentions.Add(new ArcMention(suggestion.Kind, id, label, start, start + token.Length - 1));

        _text = updated;
        _syncing = true;
        _box.Text = updated;
        _syncing = false;
        _box.CaretIndex = start + token.Length;
        _box.Focus();

        ClosePanel();
        UpdateMirror();
        var value = new ArcMentionValue(_text, _mentions.ToList());
        SetCurrentValue(ValueProperty, value);
        ValueChanged?.Invoke(this, new ArcMentionValueEventArgs(value));
        MentionAdded?.Invoke(this, new ArcMentionValueEventArgs(value));
    }

    private void MoveSuggestion(int delta)
    {
        if (_suggestions.Count == 0)
        {
            return;
        }

        _suggestionIndex = (_suggestionIndex + delta + _suggestions.Count) % _suggestions.Count;
        RenderSuggestions();
    }

    private void OnBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_panelOpen && _suggestions.Count > 0)
        {
            switch (e.Key)
            {
                case Key.Down:
                    e.Handled = true;
                    MoveSuggestion(1);
                    return;
                case Key.Up:
                    e.Handled = true;
                    MoveSuggestion(-1);
                    return;
                case Key.Enter:
                case Key.Tab:
                    e.Handled = true;
                    Choose(_suggestions[_suggestionIndex]);
                    return;
                case Key.Escape:
                    e.Handled = true;
                    ClosePanel();
                    return;
            }
        }
        else if (e.Key == Key.Escape && _panelOpen)
        {
            e.Handled = true;
            ClosePanel();
            return;
        }

        if (_box is null)
        {
            return;
        }

        var caret = _box.CaretIndex;
        var mention = _mentions.FirstOrDefault(entry => caret > entry.Start && caret <= entry.End);

        if (e.Key == Key.Back && mention is not null && caret == mention.End)
        {
            e.Handled = true;
            _box.Text = _text.Remove(mention.Start, Math.Min(mention.End - mention.Start, _text.Length - mention.Start));
            _box.CaretIndex = mention.Start;
            return;
        }

        if (e.Key == Key.Delete && mention is not null && caret == mention.Start)
        {
            e.Handled = true;
            _box.Text = _text.Remove(mention.Start, Math.Min(mention.End - mention.Start, _text.Length - mention.Start));
            _box.CaretIndex = mention.Start;
            return;
        }

        if (e.Key == Key.Left && mention is not null && caret == mention.End)
        {
            e.Handled = true;
            _box.CaretIndex = mention.Start;
            return;
        }

        if (e.Key == Key.Right && mention is not null && caret == mention.Start)
        {
            e.Handled = true;
            _box.CaretIndex = mention.End;
            return;
        }

        if (e.Key == Key.Enter && SubmitOnEnter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            if (!string.IsNullOrWhiteSpace(_text))
            {
                Submitted?.Invoke(this, new ArcMentionValueEventArgs(new ArcMentionValue(_text, _mentions.ToList())));
            }
        }
    }

    private void UpdateMessage()
    {
        if (_message is null)
        {
            return;
        }

        _message.Text = Description;
        _message.IsVisible = !string.IsNullOrWhiteSpace(Description);
        _message.Foreground = ArcResources.Brush(ArcTokens.TextMuted);
    }

    private void UpdateAutomation()
    {
        var label = Label ?? "Message";
        AutomationProperties.SetName(this, label);
        if (_panelOpen)
        {
            AutomationProperties.SetHelpText(this,
                _suggestions.Count == 0
                    ? "No matches"
                    : $"{_suggestions.Count} {(_triggerKind == ArcMentionKind.Person ? "people" : "channels")}");
        }
    }

    private sealed record Suggestion(ArcMentionKind Kind, ArcMentionPerson? Person, ArcMentionChannel? Channel, int Rank);
}
