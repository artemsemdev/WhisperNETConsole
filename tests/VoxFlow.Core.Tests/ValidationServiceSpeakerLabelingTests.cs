using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;
using VoxFlow.Core.Services;
using VoxFlow.Core.Services.Python;
using Xunit;

namespace VoxFlow.Core.Tests;

public sealed class ValidationServiceSpeakerLabelingTests
{
    [Fact]
    public async Task ValidateAsync_Enabled_RuntimeReady_AddsPassedSpeakerCheck()
    {
        using var directory = BuildEnvironment(out var settingsPath, speakerLabelingEnabled: true);
        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var preflight = new FakeSpeakerLabelingPreflight
        {
            RuntimeStatus = PythonRuntimeStatus.Ready("/usr/bin/python3", "3.11.6"),
            ModelCached = true
        };
        var service = new ValidationService(new StubAudioConversionService(), preflight);

        var result = await service.ValidateAsync(options);

        var runtimeCheck = result.Checks.SingleOrDefault(c => c.Name == "Speaker labeling runtime");
        Assert.NotNull(runtimeCheck);
        Assert.Equal(ValidationCheckStatus.Passed, runtimeCheck!.Status);
        Assert.Contains("3.11.6", runtimeCheck.Details);
    }

    [Fact]
    public async Task ValidateAsync_Disabled_DoesNotRunSpeakerChecks()
    {
        using var directory = BuildEnvironment(out var settingsPath, speakerLabelingEnabled: false);
        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var preflight = new ThrowingSpeakerLabelingPreflight();
        var service = new ValidationService(new StubAudioConversionService(), preflight);

        var result = await service.ValidateAsync(options);

        Assert.DoesNotContain(result.Checks, c => c.Name.StartsWith("Speaker labeling"));
    }

    [Fact]
    public async Task ValidateAsync_Enabled_RuntimeNotReady_AddsWarningCheck_AndCanStartStaysTrue()
    {
        using var directory = BuildEnvironment(out var settingsPath, speakerLabelingEnabled: true);
        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var preflight = new FakeSpeakerLabelingPreflight
        {
            RuntimeStatus = PythonRuntimeStatus.NotReady("python3 not found on PATH"),
            ModelCached = true
        };
        var service = new ValidationService(new StubAudioConversionService(), preflight);

        var result = await service.ValidateAsync(options);

        var runtimeCheck = result.Checks.Single(c => c.Name == "Speaker labeling runtime");
        Assert.Equal(ValidationCheckStatus.Warning, runtimeCheck.Status);
        Assert.Contains("python3 not found", runtimeCheck.Details);
        Assert.True(result.CanStart);
    }

    [Fact]
    public async Task ValidateAsync_Enabled_ModelNotCached_AddsInformationalWarning()
    {
        using var directory = BuildEnvironment(out var settingsPath, speakerLabelingEnabled: true);
        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var preflight = new FakeSpeakerLabelingPreflight
        {
            RuntimeStatus = PythonRuntimeStatus.Ready("/usr/bin/python3", "3.11.6"),
            ModelCached = false
        };
        var service = new ValidationService(new StubAudioConversionService(), preflight);

        var result = await service.ValidateAsync(options);

        var cacheCheck = result.Checks.Single(c => c.Name == "Speaker labeling model cache");
        Assert.Equal(ValidationCheckStatus.Warning, cacheCheck.Status);
        Assert.Contains("pyannote/test", cacheCheck.Details);
        Assert.True(result.CanStart);
    }

    [Fact]
    public async Task ValidateAsync_CheckSpeakerLabelingRuntimeFalse_SkipsSpeakerChecks()
    {
        using var directory = new TemporaryDirectory();
        var inputPath = Path.Combine(directory.Path, "input.m4a");
        await File.WriteAllTextAsync(inputPath, "audio data");
        var settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: inputPath,
            wavFilePath: Path.Combine(directory.Path, "output.wav"),
            resultFilePath: Path.Combine(directory.Path, "result.txt"),
            modelFilePath: Path.Combine(directory.Path, "model.bin"),
            ffmpegExecutablePath: "ffmpeg",
            startupValidation: new
            {
                enabled = true,
                printDetailedReport = true,
                checkInputFile = true,
                checkOutputDirectories = true,
                checkOutputWriteAccess = true,
                checkFfmpegAvailability = true,
                checkModelType = true,
                checkModelDirectory = true,
                checkModelLoadability = true,
                checkLanguageSupport = false,
                checkWhisperRuntime = false,
                checkSpeakerLabelingRuntime = false
            },
            speakerLabeling: new
            {
                enabled = true,
                timeoutSeconds = 600,
                pythonRuntimeMode = "ManagedVenv",
                modelId = "pyannote/test"
            });

        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var preflight = new ThrowingSpeakerLabelingPreflight();
        var service = new ValidationService(new StubAudioConversionService(), preflight);

        var result = await service.ValidateAsync(options);

        Assert.DoesNotContain(result.Checks, c => c.Name.StartsWith("Speaker labeling"));
    }

    private static TemporaryDirectory BuildEnvironment(out string settingsPath, bool speakerLabelingEnabled)
    {
        var directory = new TemporaryDirectory();
        var inputPath = Path.Combine(directory.Path, "input.m4a");
        File.WriteAllText(inputPath, "audio data");
        settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: inputPath,
            wavFilePath: Path.Combine(directory.Path, "output.wav"),
            resultFilePath: Path.Combine(directory.Path, "result.txt"),
            modelFilePath: Path.Combine(directory.Path, "model.bin"),
            ffmpegExecutablePath: "ffmpeg",
            speakerLabeling: new
            {
                enabled = speakerLabelingEnabled,
                timeoutSeconds = 600,
                pythonRuntimeMode = "ManagedVenv",
                modelId = "pyannote/test"
            });
        return directory;
    }

    private sealed class StubAudioConversionService : IAudioConversionService
    {
        public Task ConvertToWavAsync(string inputPath, string outputPath, TranscriptionOptions options, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> ValidateFfmpegAsync(TranscriptionOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class FakeSpeakerLabelingPreflight : ISpeakerLabelingPreflight
    {
        public PythonRuntimeStatus RuntimeStatus { get; set; }
            = PythonRuntimeStatus.NotReady("not configured");

        public bool ModelCached { get; set; } = true;

        public Task<PythonRuntimeStatus> GetRuntimeStatusAsync(CancellationToken cancellationToken)
            => Task.FromResult(RuntimeStatus);

        public bool IsModelCached(string modelId) => ModelCached;
    }

    private sealed class ThrowingSpeakerLabelingPreflight : ISpeakerLabelingPreflight
    {
        public Task<PythonRuntimeStatus> GetRuntimeStatusAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("preflight should not be called");

        public bool IsModelCached(string modelId)
            => throw new InvalidOperationException("preflight should not be called");
    }
}
