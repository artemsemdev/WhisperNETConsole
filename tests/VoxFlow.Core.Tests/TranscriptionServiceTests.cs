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

    private static TranscriptionService BuildService(
        string settingsPath,
        ISpeakerEnrichmentService enrichment)
    {
        return new TranscriptionService(
            new StubConfigurationService(settingsPath),
            new PassingValidationService(),
            new SuccessfulAudioConversionService(),
            new NoOpModelService(),
            new SuccessfulWavAudioLoader(),
            new SingleSegmentLanguageSelectionService(),
            new NoOpOutputWriter(),
            enrichment);
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
