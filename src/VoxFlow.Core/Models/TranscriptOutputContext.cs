using VoxFlow.Core.Configuration;

namespace VoxFlow.Core.Models;

/// <summary>
/// Carries format selection and metadata used by format-specific transcript writers.
/// </summary>
public sealed record TranscriptOutputContext(
    ResultFormat Format,
    string? DetectedLanguage = null,
    int AcceptedSegmentCount = 0,
    int SkippedSegmentCount = 0,
    IReadOnlyList<string>? Warnings = null,
    TranscriptDocument? SpeakerTranscript = null);
