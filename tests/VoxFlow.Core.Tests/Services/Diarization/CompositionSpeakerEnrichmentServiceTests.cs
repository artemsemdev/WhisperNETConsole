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
    public async Task EnrichAsync_EnabledStandaloneMode_BuildsInnerAndDelegates()
    {
        var options = SpeakerLabelingOptions.Disabled with
        {
            Enabled = true,
            RuntimeMode = PythonRuntimeMode.Standalone
        };
        var sentinel = new SpeakerEnrichmentResult(
            Document: null,
            Warnings: new[] { "inner-was-called" },
            RuntimeBootstrapped: false);
        var inner = new StubEnrichmentService(sentinel);
        var buildCount = 0;
        var service = new CompositionSpeakerEnrichmentService(
            innerFactory: _ =>
            {
                buildCount++;
                return inner;
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

        Assert.Same(sentinel, result);
        Assert.Equal(1, buildCount);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public void BuildRuntime_Standalone_ReturnsStandaloneRuntime()
    {
        var launcher = new VoxFlow.Core.Tests.Services.Python.FakeProcessLauncher();
        var venvPaths = new VoxFlow.Core.Tests.Services.Python.FakeVenvPaths();
        var standalonePaths = new VoxFlow.Core.Tests.Services.Python.FakeStandaloneRuntimePaths();

        var runtime = CompositionSpeakerEnrichmentService.BuildRuntime(
            PythonRuntimeMode.Standalone, launcher, venvPaths, standalonePaths);

        Assert.IsType<StandaloneRuntime>(runtime);
    }

    [Fact]
    public void BuildRuntime_ManagedVenv_ReturnsManagedVenvRuntime()
    {
        var launcher = new VoxFlow.Core.Tests.Services.Python.FakeProcessLauncher();
        var venvPaths = new VoxFlow.Core.Tests.Services.Python.FakeVenvPaths();
        var standalonePaths = new VoxFlow.Core.Tests.Services.Python.FakeStandaloneRuntimePaths();

        var runtime = CompositionSpeakerEnrichmentService.BuildRuntime(
            PythonRuntimeMode.ManagedVenv, launcher, venvPaths, standalonePaths);

        Assert.IsType<ManagedVenvRuntime>(runtime);
    }

    [Fact]
    public void BuildRuntime_SystemPython_ReturnsSystemPythonRuntime()
    {
        var launcher = new VoxFlow.Core.Tests.Services.Python.FakeProcessLauncher();
        var venvPaths = new VoxFlow.Core.Tests.Services.Python.FakeVenvPaths();
        var standalonePaths = new VoxFlow.Core.Tests.Services.Python.FakeStandaloneRuntimePaths();

        var runtime = CompositionSpeakerEnrichmentService.BuildRuntime(
            PythonRuntimeMode.SystemPython, launcher, venvPaths, standalonePaths);

        Assert.IsType<SystemPythonRuntime>(runtime);
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
