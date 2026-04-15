using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;
using VoxFlow.Core.Services;
using Whisper.net;
using Xunit;

namespace VoxFlow.Core.Tests;

public sealed class TranscriptionServiceTests
{
    [Fact]
    public async Task TranscribeFileAsync_DisabledConfig_AndNullOverride_DoesNotCallEnrichment()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = WriteSettings(directory, speakerLabelingEnabled: false);
        var outputPath = Path.Combine(directory.Path, "result.txt");

        var enrichment = new RecordingEnrichmentService();
        var service = BuildService(settingsPath, enrichment);

        var result = await service.TranscribeFileAsync(new TranscribeFileRequest(
            InputPath: "/fake/input.m4a",
            ResultFilePath: outputPath));

        Assert.True(result.Success);
        Assert.Equal(0, enrichment.CallCount);
        Assert.Null(result.SpeakerTranscript);
        Assert.Empty(result.EnrichmentWarnings);
    }

    [Fact]
    public async Task TranscribeFileAsync_EnabledViaConfig_CallsEnrichment_AndPropagatesDocument()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = WriteSettings(directory, speakerLabelingEnabled: true);

        var document = BuildDocument();
        var enrichment = new RecordingEnrichmentService
        {
            NextResult = new SpeakerEnrichmentResult(document, Array.Empty<string>(), RuntimeBootstrapped: false)
        };
        var service = BuildService(settingsPath, enrichment);

        var result = await service.TranscribeFileAsync(new TranscribeFileRequest(
            InputPath: "/fake/input.m4a",
            ResultFilePath: Path.Combine(directory.Path, "result.txt")));

        Assert.Equal(1, enrichment.CallCount);
        Assert.Same(document, result.SpeakerTranscript);
    }

    [Fact]
    public async Task TranscribeFileAsync_EnabledViaConfig_RequestOverrideFalse_DoesNotCallEnrichment()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = WriteSettings(directory, speakerLabelingEnabled: true);

        var enrichment = new RecordingEnrichmentService();
        var service = BuildService(settingsPath, enrichment);

        var result = await service.TranscribeFileAsync(new TranscribeFileRequest(
            InputPath: "/fake/input.m4a",
            ResultFilePath: Path.Combine(directory.Path, "result.txt"),
            EnableSpeakers: false));

        Assert.Equal(0, enrichment.CallCount);
        Assert.Null(result.SpeakerTranscript);
    }

    [Fact]
    public async Task TranscribeFileAsync_DisabledConfig_RequestOverrideTrue_CallsEnrichment()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = WriteSettings(directory, speakerLabelingEnabled: false);

        var enrichment = new RecordingEnrichmentService();
        var service = BuildService(settingsPath, enrichment);

        var result = await service.TranscribeFileAsync(new TranscribeFileRequest(
            InputPath: "/fake/input.m4a",
            ResultFilePath: Path.Combine(directory.Path, "result.txt"),
            EnableSpeakers: true));

        Assert.Equal(1, enrichment.CallCount);
    }

    [Fact]
    public async Task TranscribeFileAsync_EnrichmentReturnsWarnings_AreAppendedToResultWarnings()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = WriteSettings(directory, speakerLabelingEnabled: true);

        var enrichment = new RecordingEnrichmentService
        {
            NextResult = new SpeakerEnrichmentResult(
                Document: null,
                Warnings: new[] { "speaker-labeling: a", "speaker-labeling: b" },
                RuntimeBootstrapped: false)
        };
        var service = BuildService(settingsPath, enrichment);

        var result = await service.TranscribeFileAsync(new TranscribeFileRequest(
            InputPath: "/fake/input.m4a",
            ResultFilePath: Path.Combine(directory.Path, "result.txt")));

        Assert.Equal(new[] { "speaker-labeling: a", "speaker-labeling: b" }, result.EnrichmentWarnings);
        Assert.Contains("speaker-labeling: a", result.Warnings);
        Assert.Contains("speaker-labeling: b", result.Warnings);
    }

    [Fact]
    public async Task TranscribeFileAsync_EnrichmentThrows_IsWrappedAsWarning_AndPipelineStillSucceeds()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = WriteSettings(directory, speakerLabelingEnabled: true);

        var enrichment = new ThrowingEnrichmentService("kaboom");
        var service = BuildService(settingsPath, enrichment);

        var result = await service.TranscribeFileAsync(new TranscribeFileRequest(
            InputPath: "/fake/input.m4a",
            ResultFilePath: Path.Combine(directory.Path, "result.txt")));

        Assert.True(result.Success);
        Assert.Null(result.SpeakerTranscript);
        Assert.Single(result.EnrichmentWarnings);
        Assert.StartsWith("speaker-labeling: internal error:", result.EnrichmentWarnings[0]);
        Assert.Contains("kaboom", result.EnrichmentWarnings[0]);
    }

    [Fact]
    public async Task TranscribeFileAsync_EnabledWithDocument_WritesVoxflowJsonArtifact()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = WriteSettings(directory, speakerLabelingEnabled: true);
        var resultPath = Path.Combine(directory.Path, "result.txt");

        var document = BuildDocument();
        var enrichment = new RecordingEnrichmentService
        {
            NextResult = new SpeakerEnrichmentResult(document, Array.Empty<string>(), RuntimeBootstrapped: false)
        };
        var artifactWriter = new RecordingArtifactWriter();
        var service = BuildService(settingsPath, enrichment, artifactWriter);

        await service.TranscribeFileAsync(new TranscribeFileRequest(
            InputPath: "/fake/input.m4a",
            ResultFilePath: resultPath));

        Assert.Equal(1, artifactWriter.CallCount);
        Assert.Equal(resultPath, artifactWriter.LastResultPath);
        Assert.Same(document, artifactWriter.LastDocument);
    }

    [Fact]
    public async Task TranscribeFileAsync_EnabledWithNullDocument_DoesNotWriteVoxflowJsonArtifact()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = WriteSettings(directory, speakerLabelingEnabled: true);

        var enrichment = new RecordingEnrichmentService
        {
            NextResult = new SpeakerEnrichmentResult(
                Document: null,
                Warnings: new[] { "speaker-labeling: sidecar-crashed: boom" },
                RuntimeBootstrapped: false)
        };
        var artifactWriter = new RecordingArtifactWriter();
        var service = BuildService(settingsPath, enrichment, artifactWriter);

        await service.TranscribeFileAsync(new TranscribeFileRequest(
            InputPath: "/fake/input.m4a",
            ResultFilePath: Path.Combine(directory.Path, "result.txt")));

        Assert.Equal(0, artifactWriter.CallCount);
    }

    [Fact]
    public async Task TranscribeFileAsync_DisabledPath_DoesNotWriteVoxflowJsonArtifact()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = WriteSettings(directory, speakerLabelingEnabled: false);

        var artifactWriter = new RecordingArtifactWriter();
        var service = BuildService(settingsPath, new RecordingEnrichmentService(), artifactWriter);

        await service.TranscribeFileAsync(new TranscribeFileRequest(
            InputPath: "/fake/input.m4a",
            ResultFilePath: Path.Combine(directory.Path, "result.txt")));

        Assert.Equal(0, artifactWriter.CallCount);
    }

    [Fact]
    public async Task TranscribeFileAsync_EnabledWithDocument_PassesSpeakerTranscriptToOutputWriter()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = WriteSettings(directory, speakerLabelingEnabled: true);

        var document = BuildDocument();
        var enrichment = new RecordingEnrichmentService
        {
            NextResult = new SpeakerEnrichmentResult(document, Array.Empty<string>(), RuntimeBootstrapped: false)
        };
        var writer = new RecordingOutputWriter();
        var service = new TranscriptionService(
            new StubConfigurationService(settingsPath),
            new PassingValidationService(),
            new SuccessfulAudioConversionService(),
            new NoOpModelService(),
            new SuccessfulWavAudioLoader(),
            new SingleSegmentLanguageSelectionService(),
            writer,
            enrichment,
            new RecordingArtifactWriter());

        await service.TranscribeFileAsync(new TranscribeFileRequest(
            InputPath: "/fake/input.m4a",
            ResultFilePath: Path.Combine(directory.Path, "result.txt")));

        Assert.NotNull(writer.LastContext);
        Assert.Same(document, writer.LastContext!.SpeakerTranscript);
    }

    [Fact]
    public async Task TranscribeFileAsync_DisabledPath_PassesNullSpeakerTranscriptToOutputWriter()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = WriteSettings(directory, speakerLabelingEnabled: false);

        var writer = new RecordingOutputWriter();
        var service = new TranscriptionService(
            new StubConfigurationService(settingsPath),
            new PassingValidationService(),
            new SuccessfulAudioConversionService(),
            new NoOpModelService(),
            new SuccessfulWavAudioLoader(),
            new SingleSegmentLanguageSelectionService(),
            writer,
            new RecordingEnrichmentService(),
            new RecordingArtifactWriter());

        await service.TranscribeFileAsync(new TranscribeFileRequest(
            InputPath: "/fake/input.m4a",
            ResultFilePath: Path.Combine(directory.Path, "result.txt")));

        Assert.NotNull(writer.LastContext);
        Assert.Null(writer.LastContext!.SpeakerTranscript);
    }

    [Fact]
    public async Task TranscribeFileAsync_ReportsProgressStageDiarizing_BetweenTranscribingAndWriting()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = WriteSettings(directory, speakerLabelingEnabled: true);

        var enrichment = new ProgressReportingEnrichmentService(percent: 90.0);
        var service = BuildService(settingsPath, enrichment);

        var updates = new List<ProgressUpdate>();
        var progress = new Progress<ProgressUpdate>(u => updates.Add(u));

        await service.TranscribeFileAsync(
            new TranscribeFileRequest(
                InputPath: "/fake/input.m4a",
                ResultFilePath: Path.Combine(directory.Path, "result.txt")),
            progress);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!updates.Any(u => u.Stage == ProgressStage.Diarizing) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        var stages = updates.Select(u => u.Stage).ToList();
        var transcribingIdx = stages.FindIndex(s => s == ProgressStage.Transcribing);
        var diarizingIdx = stages.FindIndex(s => s == ProgressStage.Diarizing);
        var writingIdx = stages.FindIndex(s => s == ProgressStage.Writing);

        Assert.True(transcribingIdx >= 0, "expected Transcribing update");
        Assert.True(diarizingIdx >= 0, "expected Diarizing update");
        Assert.True(writingIdx >= 0, "expected Writing update");
        Assert.True(transcribingIdx < diarizingIdx, "Transcribing must precede Diarizing");
        Assert.True(diarizingIdx < writingIdx, "Diarizing must precede Writing");
    }

    private static TranscriptDocument BuildDocument()
        => new(
            Speakers: new[] { new SpeakerInfo("A", "A", TimeSpan.FromSeconds(1)) },
            Words: Array.Empty<TranscriptWord>(),
            Turns: Array.Empty<SpeakerTurn>(),
            Metadata: new TranscriptMetadata(1, "pyannote/test", 1));

    private sealed class ThrowingEnrichmentService : ISpeakerEnrichmentService
    {
        private readonly string _message;
        public ThrowingEnrichmentService(string message) { _message = message; }

        public Task<SpeakerEnrichmentResult> EnrichAsync(
            string wavPath,
            IReadOnlyList<FilteredSegment> segments,
            TranscriptMetadata metadata,
            SpeakerLabelingOptions options,
            IProgress<ProgressUpdate>? progress,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException(_message);
    }

    private sealed class ProgressReportingEnrichmentService : ISpeakerEnrichmentService
    {
        private readonly double _percent;
        public ProgressReportingEnrichmentService(double percent) { _percent = percent; }

        public Task<SpeakerEnrichmentResult> EnrichAsync(
            string wavPath,
            IReadOnlyList<FilteredSegment> segments,
            TranscriptMetadata metadata,
            SpeakerLabelingOptions options,
            IProgress<ProgressUpdate>? progress,
            CancellationToken cancellationToken)
        {
            progress?.Report(new ProgressUpdate(
                ProgressStage.Diarizing,
                _percent,
                TimeSpan.FromMilliseconds(1),
                Message: "diarizing"));
            return Task.FromResult(SpeakerEnrichmentResult.Empty);
        }
    }

    private static TranscriptionService BuildService(
        string settingsPath,
        ISpeakerEnrichmentService enrichment)
        => BuildService(settingsPath, enrichment, new RecordingArtifactWriter());

    private static TranscriptionService BuildService(
        string settingsPath,
        ISpeakerEnrichmentService enrichment,
        IVoxflowTranscriptArtifactWriter artifactWriter)
    {
        return new TranscriptionService(
            new StubConfigurationService(settingsPath),
            new PassingValidationService(),
            new SuccessfulAudioConversionService(),
            new NoOpModelService(),
            new SuccessfulWavAudioLoader(),
            new SingleSegmentLanguageSelectionService(),
            new NoOpOutputWriter(),
            enrichment,
            artifactWriter);
    }

    private sealed class RecordingArtifactWriter : IVoxflowTranscriptArtifactWriter
    {
        public int CallCount { get; private set; }
        public string? LastResultPath { get; private set; }
        public TranscriptDocument? LastDocument { get; private set; }

        public Task WriteAsync(string resultPath, TranscriptDocument document, CancellationToken cancellationToken)
        {
            CallCount++;
            LastResultPath = resultPath;
            LastDocument = document;
            return Task.CompletedTask;
        }
    }

    private static string WriteSettings(TemporaryDirectory directory, bool speakerLabelingEnabled)
    {
        var modelPath = Path.Combine(directory.Path, "model.bin");
        File.WriteAllText(modelPath, string.Empty);
        return TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: "/fake/input.m4a",
            wavFilePath: Path.Combine(directory.Path, "audio.wav"),
            resultFilePath: Path.Combine(directory.Path, "result.txt"),
            modelFilePath: modelPath,
            ffmpegExecutablePath: "ffmpeg",
            speakerLabeling: new
            {
                enabled = speakerLabelingEnabled,
                timeoutSeconds = 600,
                pythonRuntimeMode = "ManagedVenv",
                modelId = "pyannote/test"
            });
    }

    private sealed class RecordingEnrichmentService : ISpeakerEnrichmentService
    {
        public int CallCount { get; private set; }
        public SpeakerEnrichmentResult NextResult { get; set; } = SpeakerEnrichmentResult.Empty;

        public Task<SpeakerEnrichmentResult> EnrichAsync(
            string wavPath,
            IReadOnlyList<FilteredSegment> segments,
            TranscriptMetadata metadata,
            SpeakerLabelingOptions options,
            IProgress<ProgressUpdate>? progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class StubConfigurationService(string settingsPath) : IConfigurationService
    {
        public Task<TranscriptionOptions> LoadAsync(string? configurationPath = null)
            => Task.FromResult(TranscriptionOptions.LoadFromPath(configurationPath ?? settingsPath));

        public IReadOnlyList<SupportedLanguage> GetSupportedLanguages(string? configurationPath = null)
            => LoadAsync(configurationPath).GetAwaiter().GetResult().SupportedLanguages;
    }

    private sealed class PassingValidationService : IValidationService
    {
        public Task<ValidationResult> ValidateAsync(
            TranscriptionOptions options,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ValidationResult(
                "PASSED",
                true,
                false,
                options.ConfigurationPath,
                [new ValidationCheck("Settings file", ValidationCheckStatus.Passed, options.ConfigurationPath)]));
    }

    private sealed class SuccessfulAudioConversionService : IAudioConversionService
    {
        public Task ConvertToWavAsync(
            string inputPath,
            string outputPath,
            TranscriptionOptions options,
            CancellationToken cancellationToken = default)
        {
            File.WriteAllText(outputPath, "wav");
            return Task.CompletedTask;
        }

        public Task<bool> ValidateFfmpegAsync(
            TranscriptionOptions options,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class NoOpModelService : IModelService
    {
        public Task<WhisperFactory> GetOrCreateFactoryAsync(
            TranscriptionOptions options,
            CancellationToken cancellationToken = default)
            => Task.FromResult<WhisperFactory>(null!);

        public ModelInfo InspectModel(TranscriptionOptions options)
            => new(options.ModelFilePath, options.ModelType, false, null, false, true);
    }

    private sealed class SuccessfulWavAudioLoader : IWavAudioLoader
    {
        public Task<float[]> LoadSamplesAsync(
            string wavPath,
            TranscriptionOptions options,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new float[16_000]);
    }

    private sealed class SingleSegmentLanguageSelectionService : ILanguageSelectionService
    {
        public Task<LanguageSelectionResult> SelectBestCandidateAsync(
            WhisperFactory factory,
            float[] audioSamples,
            TranscriptionOptions options,
            IProgress<ProgressUpdate>? progress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new LanguageSelectionResult(
                new SupportedLanguage("en", "English", 0),
                Score: 0.9,
                AudioDuration: TimeSpan.FromSeconds(1),
                AcceptedSegments: new[] { new FilteredSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "hello", 0.9) },
                SkippedSegments: Array.Empty<SkippedSegment>()));
    }

    private sealed class RecordingOutputWriter : IOutputWriter
    {
        public TranscriptOutputContext? LastContext { get; private set; }

        public Task WriteAsync(
            string outputPath,
            IReadOnlyList<FilteredSegment> segments,
            TranscriptOutputContext context,
            CancellationToken cancellationToken = default)
        {
            LastContext = context;
            File.WriteAllText(outputPath, "recorded");
            return Task.CompletedTask;
        }

        public string BuildOutputText(IReadOnlyList<FilteredSegment> segments, TranscriptOutputContext context)
            => string.Empty;
    }

    private sealed class NoOpOutputWriter : IOutputWriter
    {
        public Task WriteAsync(
            string outputPath,
            IReadOnlyList<FilteredSegment> segments,
            TranscriptOutputContext context,
            CancellationToken cancellationToken = default)
        {
            File.WriteAllText(outputPath, string.Join("\n", segments.Select(s => s.Text)));
            return Task.CompletedTask;
        }

        public string BuildOutputText(IReadOnlyList<FilteredSegment> segments, TranscriptOutputContext context)
            => string.Join("\n", segments.Select(s => s.Text));
    }
}
