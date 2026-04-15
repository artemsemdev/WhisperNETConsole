using System.Diagnostics;
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

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var stopwatch = Stopwatch.StartNew();
        // Synchronous IProgress<T> adapter: Progress<T> would queue to
        // SynchronizationContext/ThreadPool and lose FIFO ordering between
        // reports, which matters for downstream progress bar rendering.
        var sidecarProgress = progress is null
            ? null
            : new DelegateProgress<SpeakerLabelingProgress>(update =>
            {
                var fraction = update.Fraction ?? 0.0;
                if (fraction < 0.0) fraction = 0.0;
                else if (fraction > 1.0) fraction = 1.0;
                var percent = 85.0 + (fraction * 10.0); // map [0,1] into [85,95]
                progress.Report(new ProgressUpdate(
                    Stage: ProgressStage.Diarizing,
                    PercentComplete: percent,
                    Elapsed: stopwatch.Elapsed,
                    Message: update.Stage));
            });

        DiarizationResult diarization;
        try
        {
            diarization = await _sidecar
                .DiarizeAsync(new DiarizationRequest(wavPath), sidecarProgress, linkedCts.Token)
                .ConfigureAwait(false);
        }
        catch (DiarizationSidecarException ex)
        {
            return new SpeakerEnrichmentResult(
                Document: null,
                Warnings: new[] { FormatSidecarWarning(ex.Reason, ex.Message) },
                RuntimeBootstrapped: runtimeBootstrapped);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            return new SpeakerEnrichmentResult(
                Document: null,
                Warnings: new[] { $"speaker-labeling: timed out after {options.TimeoutSeconds}s" },
                RuntimeBootstrapped: runtimeBootstrapped);
        }

        var document = _mergeService.Merge(segments, diarization, metadata);
        var warnings = document.Speakers.Count == 0
            ? new[] { "speaker-labeling: diarization returned zero speakers" }
            : Array.Empty<string>();
        return new SpeakerEnrichmentResult(document, warnings, runtimeBootstrapped);
    }

    private static string FormatSidecarWarning(SidecarFailureReason reason, string message)
        => $"speaker-labeling: {ReasonToKebab(reason)}: {message}";

    private static string ReasonToKebab(SidecarFailureReason reason) => reason switch
    {
        SidecarFailureReason.RuntimeNotReady => "runtime-not-ready",
        SidecarFailureReason.ProcessCrashed => "process-crashed",
        SidecarFailureReason.Timeout => "timeout",
        SidecarFailureReason.MalformedJson => "malformed-json",
        SidecarFailureReason.SchemaViolation => "schema-violation",
        SidecarFailureReason.ErrorResponseReturned => "error-response-returned",
        _ => "unknown"
    };

    private sealed class DelegateProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public DelegateProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }
}
