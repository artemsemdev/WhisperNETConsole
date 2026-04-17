using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;
using VoxFlow.Core.Services.Diarization;
using VoxFlow.Core.Services.Python;
using Xunit;

namespace VoxFlow.Core.Tests.Services.Diarization;

/// <summary>
/// Covers <see cref="CompositionSpeakerEnrichmentService"/>: the wrapper that
/// picks the right <see cref="IPythonRuntime"/> / sidecar / bootstrapper tree
/// per call based on <see cref="SpeakerLabelingOptions.RuntimeMode"/>, then
/// delegates to a <see cref="SpeakerEnrichmentService"/>.
/// </summary>
public sealed class CompositionSpeakerEnrichmentServiceTests
{
    [Fact]
    public async Task EnrichAsync_Disabled_ShortCircuits_WithoutBuildingRuntime()
    {
        var buildCount = 0;
        var service = new CompositionSpeakerEnrichmentService(
            innerFactory: _ =>
            {
                buildCount++;
                throw new InvalidOperationException("should not be called");
            });

        var result = await service.EnrichAsync(
            wavPath: "ignored.wav",
            segments: Array.Empty<FilteredSegment>(),
            metadata: new TranscriptMetadata(
                SchemaVersion: 1,
                DiarizationModel: "pyannote/test",
                SidecarVersion: 1),
            options: SpeakerLabelingOptions.Disabled,
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result.Document);
        Assert.Equal(0, buildCount);
    }

    [Fact]
    public async Task EnrichAsync_Enabled_DelegatesToInnerBuiltFromFactory()
    {
        var options = SpeakerLabelingOptions.Disabled with { Enabled = true };
        var segments = new List<FilteredSegment>();
        var metadata = new TranscriptMetadata(
            SchemaVersion: 1,
            DiarizationModel: "pyannote/test",
            SidecarVersion: 1);
        var sentinel = new SpeakerEnrichmentResult(
            Document: null,
            Warnings: new[] { "sentinel-warning" },
            RuntimeBootstrapped: false);
        var inner = new StubEnrichmentService(sentinel);
        var service = new CompositionSpeakerEnrichmentService(innerFactory: _ => inner);

        var result = await service.EnrichAsync(
            wavPath: "ignored.wav",
            segments: segments,
            metadata: metadata,
            options: options,
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.Same(sentinel, result);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task EnrichAsync_EnabledStandaloneMode_ReturnsWarning_NeverBuildsInner()
    {
        var options = SpeakerLabelingOptions.Disabled with
        {
            Enabled = true,
            RuntimeMode = PythonRuntimeMode.Standalone
        };
        var buildCount = 0;
        var service = new CompositionSpeakerEnrichmentService(
            innerFactory: _ =>
            {
                buildCount++;
                throw new InvalidOperationException("should not be called");
            });

        var result = await service.EnrichAsync(
            wavPath: "ignored.wav",
            segments: Array.Empty<FilteredSegment>(),
            metadata: new TranscriptMetadata(
                SchemaVersion: 1,
                DiarizationModel: "pyannote/test",
                SidecarVersion: 1),
            options: options,
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.Null(result.Document);
        Assert.Equal(0, buildCount);
        Assert.Single(result.Warnings);
        Assert.Contains("Standalone", result.Warnings[0]);
    }

    private sealed class StubEnrichmentService : ISpeakerEnrichmentService
    {
        private readonly SpeakerEnrichmentResult _result;
        public int CallCount { get; private set; }

        public StubEnrichmentService(SpeakerEnrichmentResult result) => _result = result;

        public Task<SpeakerEnrichmentResult> EnrichAsync(
            string wavPath,
            IReadOnlyList<FilteredSegment> segments,
            TranscriptMetadata metadata,
            SpeakerLabelingOptions options,
            IProgress<ProgressUpdate>? progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }
}
