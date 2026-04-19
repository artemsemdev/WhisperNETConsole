namespace VoxFlow.Core.Models;

/// <summary>
/// Represents the outcome of a single-file transcription request.
/// </summary>
public sealed record TranscribeFileResult(
    bool Success,
    string? DetectedLanguage,
    string? ResultFilePath,
    int AcceptedSegmentCount,
    int SkippedSegmentCount,
    TimeSpan Duration,
    IReadOnlyList<string> Warnings,
    string? TranscriptPreview,
    TranscriptDocument? SpeakerTranscript = null,
    IReadOnlyList<string>? EnrichmentWarnings = null)
{
    public IReadOnlyList<string> EnrichmentWarnings { get; } = EnrichmentWarnings ?? Array.Empty<string>();
}
