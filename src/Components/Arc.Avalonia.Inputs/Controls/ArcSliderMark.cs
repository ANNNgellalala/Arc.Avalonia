namespace Arc.Avalonia.Inputs.Controls;

/// <summary>A tick or detent on a slider track, optionally labelled.</summary>
public sealed class ArcSliderMark
{
    public ArcSliderMark(double value, string? label = null)
    {
        Value = value;
        Label = label;
    }

    public double Value { get; }

    public string? Label { get; }
}
