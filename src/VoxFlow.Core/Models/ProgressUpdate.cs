namespace VoxFlow.Core.Models;

/// <summary>
/// Represents a host-facing progress notification emitted during transcription work.
/// </summary>
public sealed record ProgressUpdate(
    ProgressStage Stage,
    double PercentComplete,
    TimeSpan Elapsed,
    string? Message = null,
    string? CurrentLanguage = null,
    int? BatchFileIndex = null,
    int? BatchFileTotal = null);

/// <summary>
/// Identifies the major phase currently executing in the transcription pipeline.
/// </summary>
public enum ProgressStage
{
    Validating,
    Converting,
    LoadingModel,
    Transcribing,
    Filtering,
    Diarizing,
    Writing,
    Complete,
    Failed
}
