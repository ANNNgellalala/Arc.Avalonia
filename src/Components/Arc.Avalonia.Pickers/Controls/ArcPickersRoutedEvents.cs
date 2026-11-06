using System;
using System.Collections.Generic;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>Payload for the bubbled <see cref="ArcCalendar.SelectedDateChangedRouted"/> event.</summary>
public sealed class ArcCalendarDateChangedEventArgs : RoutedEventArgs
{
    public ArcCalendarDateChangedEventArgs()
    {
    }

    public ArcCalendarDateChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The date the visitor picked.</summary>
    public DateTime Date { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcDatePicker.SelectedDateChangedRouted"/> event.</summary>
public sealed class ArcDatePickerSelectedEventArgs : RoutedEventArgs
{
    public ArcDatePickerSelectedEventArgs()
    {
    }

    public ArcDatePickerSelectedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The picked date, or null after Clear.</summary>
    public DateTime? Date { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcDateRangePicker.RangeChangedRouted"/> event.</summary>
public sealed class ArcDateRangeChangedEventArgs : RoutedEventArgs
{
    public ArcDateRangeChangedEventArgs()
    {
    }

    public ArcDateRangeChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The applied range.</summary>
    public ArcDateRange Range { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcTimePicker.TimeChangedRouted"/> event.</summary>
public sealed class ArcTimePickerChangedEventArgs : RoutedEventArgs
{
    public ArcTimePickerChangedEventArgs()
    {
    }

    public ArcTimePickerChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The selected time.</summary>
    public TimeSpan Time { get; init; }
}

/// <summary>Payload for the bubbled color picker <c>ValueChangedRouted</c> events.</summary>
public sealed class ArcColorValueChangedEventArgs : RoutedEventArgs
{
    public ArcColorValueChangedEventArgs()
    {
    }

    public ArcColorValueChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The new color.</summary>
    public Color Color { get; init; }
}

/// <summary>Payload for the bubbled <see cref="ArcEmojiPicker.SelectedRouted"/> event.</summary>
public sealed class ArcEmojiSelectedEventArgs : RoutedEventArgs
{
    public ArcEmojiSelectedEventArgs()
    {
    }

    public ArcEmojiSelectedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The chosen emoji and its metadata.</summary>
    public ArcEmojiSelection Selection { get; init; } = null!;
}

/// <summary>Payload for the bubbled <see cref="ArcFileDropzone.FilesChangedRouted"/> event.</summary>
public sealed class ArcFilesChangedEventArgs : RoutedEventArgs
{
    public ArcFilesChangedEventArgs()
    {
    }

    public ArcFilesChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The current files in drop order.</summary>
    public IReadOnlyList<IStorageFile> Files { get; init; } = null!;
}

/// <summary>Payload for the bubbled <see cref="ArcSignaturePad.StrokesChangedRouted"/> event.</summary>
public sealed class ArcStrokesChangedEventArgs : RoutedEventArgs
{
    public ArcStrokesChangedEventArgs()
    {
    }

    public ArcStrokesChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    /// <summary>The current ink strokes.</summary>
    public IReadOnlyList<ArcInkStroke> Strokes { get; init; } = null!;
}
