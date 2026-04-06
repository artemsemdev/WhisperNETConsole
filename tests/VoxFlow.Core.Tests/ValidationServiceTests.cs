using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;
using VoxFlow.Core.Services;
using Xunit;

namespace VoxFlow.Core.Tests;

public sealed class ValidationServiceTests
{
    [Fact]
    public async Task ValidateAsync_ThrowsWhenCancellationIsRequestedBeforeValidationStarts()
    {
        using var directory = new TemporaryDirectory();
        var inputPath = Path.Combine(directory.Path, "input.m4a");
        await File.WriteAllTextAsync(inputPath, "test");

        var settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: inputPath,
            wavFilePath: Path.Combine(directory.Path, "output.wav"),
            resultFilePath: Path.Combine(directory.Path, "result.txt"),
            modelFilePath: Path.Combine(directory.Path, "model.bin"),
            ffmpegExecutablePath: "ffmpeg");

        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var service = new ValidationService(new StubAudioConversionService());

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ValidateAsync(options, cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ValidateAsync_UnsupportedInputFormat_ReportsFailure()
    {
        using var directory = new TemporaryDirectory();
        var inputPath = Path.Combine(directory.Path, "input.txt");
        await File.WriteAllTextAsync(inputPath, "not audio");

        var settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: inputPath,
            wavFilePath: Path.Combine(directory.Path, "output.wav"),
            resultFilePath: Path.Combine(directory.Path, "result.txt"),
            modelFilePath: Path.Combine(directory.Path, "model.bin"),
            ffmpegExecutablePath: "ffmpeg");

        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var service = new ValidationService(new StubAudioConversionService());
        var result = await service.ValidateAsync(options);

        Assert.False(result.CanStart);
        var inputCheck = result.Checks.First(c => c.Name == "Input file");
        Assert.Equal(ValidationCheckStatus.Failed, inputCheck.Status);
        Assert.Contains("Unsupported input format", inputCheck.Details);
    }

    [Theory]
    [InlineData("input.m4a")]
    [InlineData("input.wav")]
    [InlineData("input.mp3")]
    [InlineData("input.aac")]
    [InlineData("input.flac")]
    [InlineData("input.ogg")]
    [InlineData("input.aiff")]
    [InlineData("input.mp4")]
    public async Task ValidateAsync_SupportedInputFormat_PassesInputFileCheck(string fileName)
    {
        using var directory = new TemporaryDirectory();
        var inputPath = Path.Combine(directory.Path, fileName);
        await File.WriteAllTextAsync(inputPath, "audio data");

        var settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: inputPath,
            wavFilePath: Path.Combine(directory.Path, "output.wav"),
            resultFilePath: Path.Combine(directory.Path, "result.txt"),
            modelFilePath: Path.Combine(directory.Path, "model.bin"),
            ffmpegExecutablePath: "ffmpeg");

        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var service = new ValidationService(new StubAudioConversionService());
        var result = await service.ValidateAsync(options);

        var inputCheck = result.Checks.First(c => c.Name == "Input file");
        Assert.Equal(ValidationCheckStatus.Passed, inputCheck.Status);
    }

    /// <summary>
    /// Minimal stub that satisfies the ValidationService constructor dependency.
    /// </summary>
    private sealed class StubAudioConversionService : IAudioConversionService
    {
        public Task ConvertToWavAsync(string inputPath, string outputPath, TranscriptionOptions options, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> ValidateFfmpegAsync(TranscriptionOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
