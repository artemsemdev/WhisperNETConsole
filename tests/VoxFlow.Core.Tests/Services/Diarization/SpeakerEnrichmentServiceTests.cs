using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;
using VoxFlow.Core.Services.Diarization;
using VoxFlow.Core.Services.Python;
using Xunit;

namespace VoxFlow.Core.Tests.Services.Diarization;

public sealed class SpeakerEnrichmentServiceTests
{
    private static readonly TranscriptMetadata Metadata =
        new(SchemaVersion: 1, DiarizationModel: "pyannote/test", SidecarVersion: 1);

    private static readonly IReadOnlyList<FilteredSegment> EmptySegments = Array.Empty<FilteredSegment>();

    private static SpeakerLabelingOptions DisabledOptions()
        => SpeakerLabelingOptions.Disabled;

    private static SpeakerLabelingOptions EnabledOptions(int timeoutSeconds = 600)
        => new(
            Enabled: true,
            TimeoutSeconds: timeoutSeconds,
            RuntimeMode: PythonRuntimeMode.ManagedVenv,
            ModelId: "pyannote/test");

    [Fact]
    public async Task EnrichAsync_Disabled_ReturnsEmptyDocument_WithoutTouchingRuntime()
    {
        var runtime = new FakePythonRuntime
        {
            NextStatus = PythonRuntimeStatus.NotReady("should-not-be-queried")
        };
        var sidecar = FakeDiarizationSidecar.ThrowsIfCalled();
        var mergeService = new ThrowingSpeakerMergeService();
        var bootstrapper = new ThrowingBootstrapper();

        var service = new SpeakerEnrichmentService(runtime, sidecar, mergeService, bootstrapper);

        var result = await service.EnrichAsync(
            wavPath: "/tmp/audio.wav",
            segments: EmptySegments,
            metadata: Metadata,
            options: DisabledOptions(),
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result.Document);
        Assert.Empty(result.Warnings);
        Assert.False(result.RuntimeBootstrapped);
        Assert.Equal(0, sidecar.CallCount);
    }

    private sealed class ThrowingSpeakerMergeService : ISpeakerMergeService
    {
        public TranscriptDocument Merge(
            IReadOnlyList<FilteredSegment> segments,
            DiarizationResult diarization,
            TranscriptMetadata metadata)
            => throw new InvalidOperationException("merge service should not be called");
    }

    private sealed class ThrowingBootstrapper : IManagedVenvBootstrapper
    {
        public Task BootstrapAsync(IProgress<VenvBootstrapStage>? progress, CancellationToken cancellationToken)
            => throw new InvalidOperationException("bootstrapper should not be called");
    }
}
