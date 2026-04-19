using Whisper.net;

namespace VoxFlow.Core.Models;

/// <summary>
/// Represents a transcript segment that survived the filtering pipeline.
/// </summary>
public sealed record FilteredSegment(
    TimeSpan Start,
    TimeSpan End,
    string Text,
    double Probability,
    IReadOnlyList<WhisperToken> Words)
{
    public FilteredSegment(TimeSpan Start, TimeSpan End, string Text, double Probability)
        : this(Start, End, Text, Probability, Array.Empty<WhisperToken>())
    {
    }
}
