using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Chat;

/// <summary>
/// One virtualized row of <see cref="ArcChatView"/>: an optional day separator plus the
/// <see cref="ArcChatBubble"/>. Rows are created by the list item template and refreshed in place when a
/// message is added/removed so the day separators and group corners stay correct without rebuilding the
/// whole conversation.
/// </summary>
internal sealed class ArcChatRow : StackPanel
{
    private readonly ArcChatView _view;
    private readonly ArcChatBubble _bubble;
    private ArcChatMessage _message;
    private Grid? _day;
    private TextBlock? _dayText;

    public ArcChatRow(ArcChatView view, ArcChatMessage message)
    {
        _view = view;
        _message = message;
        Spacing = 0;

        _bubble = new ArcChatBubble();
        _bubble.InteractionOccurred += (_, args) => _view.OnBubbleInteraction(args);
        _bubble.Warning += (_, args) => _view.OnBubbleWarning(args);
        _bubble.Announced += (_, text) => _view.OnBubbleAnnounced(text);
        Children.Add(_bubble);
        Refresh();
    }

    public ArcChatMessage Message => _message;

    public void Refresh()
    {
        var index = _view.IndexOf(_message.Id);
        if (index < 0)
        {
            IsVisible = false;
            return;
        }

        IsVisible = true;
        _view.PrepareMessage(_message);

        var previous = index > 0 ? _view.Messages[index - 1] : null;
        var showDay = previous is null
                      || previous.CreatedAt.ToLocalTime().Date != _message.CreatedAt.ToLocalTime().Date;
        EnsureDay();
        if (showDay)
        {
            _dayText!.Text = ArcChatUi.FormatDay(_message.CreatedAt);
            if (!Children.Contains(_day!))
            {
                Children.Insert(0, _day!);
            }
        }
        else
        {
            Children.Remove(_day!);
        }

        var grouping = _view.GetGroupState(_message);
        _bubble.MarkdownOptions = _view.MarkdownOptions;
        _bubble.Message = _message;
        _bubble.ApplyGrouping(
            grouping.IsOwn,
            grouping.FirstInGroup,
            grouping.LastInGroup,
            grouping.ShowHeader,
            grouping.ShowStatus,
            grouping.ShowAvatar);
        _bubble.AnimateEntry = _view.ConsumeAnimation(_message.Id);
    }

    private void EnsureDay()
    {
        if (_day is not null)
        {
            return;
        }

        var hairline = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Center };
        hairline.Paint(Border.BackgroundProperty, ArcTokens.BorderSubtle);
        _dayText = ArcChatUi.Text(string.Empty, ArcTokens.TextXs, ArcTokens.TextMuted);
        var pill = ArcChatUi.Pill(_dayText, ArcTokens.SurfaceMuted, ArcTokens.BorderSubtle);
        pill.HorizontalAlignment = HorizontalAlignment.Center;

        _day = new Grid { Margin = new Thickness(0, 8, 0, 6) };
        _day.Children.Add(hairline);
        _day.Children.Add(pill);
        AutomationProperties.SetName(pill, "日期分隔");
    }
}
