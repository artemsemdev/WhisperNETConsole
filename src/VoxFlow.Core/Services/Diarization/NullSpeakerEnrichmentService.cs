using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Services.Diarization;

/// <summary>
/// Default <see cref="ISpeakerEnrichmentService"/> used when no concrete
/// diarization stack has been composed into the container. Always returns
/// an empty <see cref="SpeakerEnrichmentResult"/>. Kept so <c>AddVoxFlowCore</c>
/// resolves cleanly during Phase 1 while the real
/// <see cref="SpeakerEnrichmentService"/> composition (sidecar + runtime +
/// bootstrapper) is assembled by the hosting layer.
/// </summary>
public sealed class NullSpeakerEnrichmentService : ISpeakerEnrichmentService
{
    public Task<SpeakerEnrichmentResult> EnrichAsync(
        string wavPath,
        IReadOnlyList<FilteredSegment> segments,
        TranscriptMetadata metadata,
        SpeakerLabelingOptions options,
        IProgress<ProgressUpdate>? progress,
        CancellationToken cancellationToken)
        => Task.FromResult(SpeakerEnrichmentResult.Empty);
}
