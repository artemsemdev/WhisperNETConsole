using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Interfaces;

/// <summary>
/// Composes <see cref="IPythonRuntime"/>, <see cref="IDiarizationSidecar"/>,
/// and <see cref="ISpeakerMergeService"/> into a single orchestrated call.
/// Owns runtime readiness, managed-venv bootstrap, per-call timeout,
/// cancellation, sidecar failure taxonomy, and progress mapping. Never
/// throws for enrichment-side failures: returns warnings with a null
/// <c>Document</c> instead, so the caller's transcription pipeline stays up.
/// </summary>
public interface ISpeakerEnrichmentService
{
    Task<SpeakerEnrichmentResult> EnrichAsync(
        string wavPath,
        IReadOnlyList<FilteredSegment> segments,
        TranscriptMetadata metadata,
        SpeakerLabelingOptions options,
        IProgress<ProgressUpdate>? progress,
        CancellationToken cancellationToken);
}
