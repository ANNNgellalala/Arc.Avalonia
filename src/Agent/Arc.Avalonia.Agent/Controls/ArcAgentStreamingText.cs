using System;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Agent;

/// <summary>
/// A single streaming text block: append a delta on each flush, no re-layout of the surrounding tree and no
/// timer. The control only buffers text; the coalescer decides when a flush happens.
/// </summary>
public class ArcAgentStreamingText : Panel
{
    private readonly StringBuilder _buffer = new();
    private readonly TextBlock _text;
    private readonly TextBlock _cursor;
    private readonly TextBlock _label;
    private bool _isStreaming;

    /// <summary>Creates the control.</summary>
    public ArcAgentStreamingText()
    {
        _label = ArcUi.Caption("Streaming");
        _text = ArcUi.Text(string.Empty, "arc-body-sm", 680);
        _text.TextWrapping = TextWrapping.Wrap;
        _cursor = new TextBlock
        {
            Text = "▍",
            FontSize = 13,
            Foreground = ArcUi.Brush(ArcTokens.Accent),
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Bottom
        };

        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(_label);
        body.Children.Add(_text);
        body.Children.Add(_cursor);

        Children.Add(new Border
        {
            Background = ArcUi.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusControl),
            Padding = new Thickness(12, 8, 12, 8),
            Child = body
        });

        SetStreaming(true);
    }

    /// <summary>The accumulated text.</summary>
    public string Text => _buffer.ToString();

    /// <summary>True while the cursor is shown.</summary>
    public bool IsStreaming
    {
        get => _isStreaming;
        set => SetStreaming(value);
    }

    /// <summary>Appends one delta and refreshes the visible text.</summary>
    public void Append(string delta)
    {
        if (string.IsNullOrEmpty(delta))
        {
            return;
        }

        _buffer.Append(delta);
        _text.Text = _buffer.ToString();
    }

    /// <summary>Replaces the buffer (for snapshots/restores).</summary>
    public void SetText(string text)
    {
        _buffer.Clear();
        _buffer.Append(text);
        _text.Text = text;
    }

    /// <summary>Clears the buffer.</summary>
    public void Reset()
    {
        _buffer.Clear();
        _text.Text = string.Empty;
    }

    private void SetStreaming(bool streaming)
    {
        _isStreaming = streaming;
        _cursor.IsVisible = streaming;
        _label.Text = streaming ? "Streaming" : "Complete";
    }
}


