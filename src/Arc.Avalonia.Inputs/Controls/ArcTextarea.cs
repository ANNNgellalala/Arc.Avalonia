using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>
/// A multiline field for notes and descriptions. It keeps Arc's animated helper row, can grow with its
/// content, and shows a live character counter when a maximum length is set.
/// </summary>
public class ArcTextarea : ArcTextFieldBase
{
    public static readonly StyledProperty<int> RowsProperty =
        AvaloniaProperty.Register<ArcTextarea, int>(nameof(Rows), 4);

    public static readonly StyledProperty<bool> AutoGrowProperty =
        AvaloniaProperty.Register<ArcTextarea, bool>(nameof(AutoGrow));

    public static readonly StyledProperty<bool> ShowCounterProperty =
        AvaloniaProperty.Register<ArcTextarea, bool>(nameof(ShowCounter));

    public static readonly StyledProperty<int?> MaxLengthProperty =
        AvaloniaProperty.Register<ArcTextarea, int?>(nameof(MaxLength));

    static ArcTextarea()
    {
        RowsProperty.Changed.AddClassHandler<ArcTextarea>((field, _) => field.UpdateAutoGrow());
        AutoGrowProperty.Changed.AddClassHandler<ArcTextarea>((field, _) => field.UpdateAutoGrow());
        ShowCounterProperty.Changed.AddClassHandler<ArcTextarea>((field, _) => field.UpdateCounterRow());
        MaxLengthProperty.Changed.AddClassHandler<ArcTextarea>((field, _) => field.UpdateCounterRow());
    }

    /// <summary>Minimum visible rows; the field starts at this height.</summary>
    public int Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>Grows the text area to fit its content instead of scrolling after <see cref="Rows"/>.</summary>
    public bool AutoGrow
    {
        get => GetValue(AutoGrowProperty);
        set => SetValue(AutoGrowProperty, value);
    }

    /// <summary>Shows "12 / 280" or "12 characters" in the helper row.</summary>
    public bool ShowCounter
    {
        get => GetValue(ShowCounterProperty);
        set => SetValue(ShowCounterProperty, value);
    }

    /// <summary>Native maximum length, also the denominator of the counter.</summary>
    public int? MaxLength
    {
        get => GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    private TextBlock? _counter;
    private Control? _messageRow;

    protected override void OnFieldTemplateApplied(TemplateAppliedEventArgs e)
    {
        _counter = e.NameScope.Find<TextBlock>("Counter");
        _messageRow = e.NameScope.Find<Control>("MessageRow");

        if (InputBox is not null)
        {
            InputBox.AcceptsReturn = true;
            InputBox.TextWrapping = TextWrapping.Wrap;
            InputBox.MaxLength = MaxLength ?? 0;
            InputBox.SizeChanged += (_, _) => UpdateAutoGrow();
        }

        UpdateCounterRow();
        UpdateAutoGrow();
    }

    protected override void OnInputChanged()
    {
        UpdateCounterRow();
        UpdateAutoGrow();
    }

    protected override void OnMessageChanged()
    {
        UpdateCounterRow();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MaxLengthProperty && InputBox is not null)
        {
            InputBox.MaxLength = MaxLength ?? 0;
            UpdateCounterRow();
        }
    }

    private void UpdateCounterRow()
    {
        if (_counter is not null)
        {
            _counter.Text = ShowCounter ? BuildCounter() : null;
            _counter.IsVisible = ShowCounter;
        }

        if (_messageRow is not null)
        {
            var hasMessage = (MessageText?.IsVisible ?? false) && !string.IsNullOrEmpty(MessageText?.Text);
            _messageRow.IsVisible = hasMessage || ShowCounter;
        }
    }

    private string BuildCounter()
    {
        var length = Value?.Length ?? 0;
        return MaxLength is > 0 ? $"{length} / {MaxLength}" : $"{length} characters";
    }

    private void UpdateAutoGrow()
    {
        if (InputBox is null)
        {
            return;
        }

        if (!AutoGrow)
        {
            InputBox.Height = double.NaN;
            return;
        }

        var width = InputBox.Bounds.Width;
        if (width <= 1)
        {
            width = Math.Max(80, Bounds.Width - 28);
        }

        var lineHeight = Math.Max(16, InputBox.FontSize * 1.45);
        var typeface = new Typeface(InputBox.FontFamily, InputBox.FontStyle, InputBox.FontWeight, InputBox.FontStretch);
        var text = InputBox.Text ?? string.Empty;

        var formatted = new FormattedText(
            text.Length == 0 ? " " : text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            InputBox.FontSize,
            Brushes.Black)
        {
            MaxTextWidth = Math.Max(40, width)
        };

        var lines = Math.Max(Rows, (int)Math.Ceiling(formatted.Height / lineHeight));
        InputBox.Height = lines * lineHeight + 4;
    }
}
