using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;
using VoxFlow.Core.Services.Python;

namespace VoxFlow.Core.Services.Diarization;

/// <inheritdoc />
public sealed class SpeakerEnrichmentService : ISpeakerEnrichmentService
{
    private readonly IPythonRuntime _runtime;
    private readonly IDiarizationSidecar _sidecar;
    private readonly ISpeakerMergeService _mergeService;
    private readonly IManagedVenvBootstrapper _bootstrapper;

    public SpeakerEnrichmentService(
        IPythonRuntime runtime,
        IDiarizationSidecar sidecar,
        ISpeakerMergeService mergeService,
        IManagedVenvBootstrapper bootstrapper)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(sidecar);
        ArgumentNullException.ThrowIfNull(mergeService);
        ArgumentNullException.ThrowIfNull(bootstrapper);
        _runtime = runtime;
        _sidecar = sidecar;
        _mergeService = mergeService;
        _bootstrapper = bootstrapper;
    }

    public async Task<SpeakerEnrichmentResult> EnrichAsync(
        string wavPath,
        IReadOnlyList<FilteredSegment> segments,
        TranscriptMetadata metadata,
        SpeakerLabelingOptions options,
        IProgress<ProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wavPath);
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return SpeakerEnrichmentResult.Empty;
        }

        var runtimeBootstrapped = false;
        var status = await _runtime.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (!status.IsReady && status.CanBootstrap)
        {
            await _bootstrapper.BootstrapAsync(progress: null, cancellationToken).ConfigureAwait(false);
            runtimeBootstrapped = true;
            status = await _runtime.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!status.IsReady)
        {
            var warning = $"speaker-labeling: runtime not ready: {status.Error}";
            return new SpeakerEnrichmentResult(
                Document: null,
                Warnings: new[] { warning },
                RuntimeBootstrapped: runtimeBootstrapped);
        }

        var diarization = await _sidecar
            .DiarizeAsync(new DiarizationRequest(wavPath), progress: null, cancellationToken)
            .ConfigureAwait(false);

        var document = _mergeService.Merge(segments, diarization, metadata);
        return new SpeakerEnrichmentResult(document, Array.Empty<string>(), runtimeBootstrapped);
    }
}
