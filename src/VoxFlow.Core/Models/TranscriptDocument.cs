using System.Text.Json;

namespace VoxFlow.Core.Models;

/// <summary>
/// Speaker-aware transcript produced by the speaker-labeling pipeline.
/// Top-level container with a speaker roster, per-word speaker references,
/// and a derived list of speaker turns.
/// </summary>
public sealed record TranscriptDocument(
    IReadOnlyList<SpeakerInfo> Speakers,
    IReadOnlyList<TranscriptWord> Words,
    IReadOnlyList<SpeakerTurn> Turns,
    TranscriptMetadata Metadata)
{
    public static JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
