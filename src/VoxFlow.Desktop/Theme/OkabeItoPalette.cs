using System.Text.RegularExpressions;
using VoxFlow.Core.Models;

namespace VoxFlow.Desktop.Theme;

/// <summary>
/// Okabe-Ito 8-color colorblind-safe palette. See https://jfly.uni-koeln.de/color/
/// Speakers beyond the palette size wrap around modulo 8; reusing a color for
/// the 9th speaker is a documented trade-off (palette exhaustion vs. silent ambiguity).
/// </summary>
public static class OkabeItoPalette
{
    public static readonly IReadOnlyList<string> Colors = new[]
    {
        "#E69F00", // Orange
        "#56B4E9", // Sky Blue
        "#009E73", // Bluish Green
        "#F0E442", // Yellow
        "#0072B2", // Blue
        "#D55E00", // Vermillion
        "#CC79A7", // Reddish Purple
        "#000000", // Black
    };

    private static readonly Regex LabelPattern = new("^[A-Z]+$", RegexOptions.Compiled);

    public static string ColorForSpeaker(SpeakerInfo speaker)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        var label = speaker.Id ?? string.Empty;
        if (!LabelPattern.IsMatch(label))
        {
            throw new ArgumentException(
                $"Speaker label must be an uppercase alphabetic ordinal (A, B, ..., Z, AA, ...); got '{label}'.",
                nameof(speaker));
        }

        var ordinal = OrdinalFor(label);
        return Colors[ordinal % Colors.Count];
    }

    private static int OrdinalFor(string label)
    {
        var ordinal = 0;
        for (var i = 0; i < label.Length; i++)
        {
            ordinal = ordinal * 26 + (label[i] - 'A' + 1);
        }
        return ordinal - 1;
    }
}
