using System.IO;
using System.Linq;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Services;
using Xunit;

namespace VoxFlow.Core.Tests;

public sealed class BatchOutputExtensionTests
{
    [Theory]
    [InlineData(".txt")]
    [InlineData(".srt")]
    [InlineData(".vtt")]
    [InlineData(".json")]
    [InlineData(".md")]
    public void DiscoverInputFiles_UsesConfiguredOutputExtension(string extension)
    {
        using var directory = new TemporaryDirectory();
        var inputDir = Path.Combine(directory.Path, "input");
        var outputDir = Path.Combine(directory.Path, "output");
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        File.WriteAllText(Path.Combine(inputDir, "recording.m4a"), "audio data");

        var options = new BatchOptions(
            InputDirectory: inputDir,
            OutputDirectory: outputDir,
            TempDirectory: directory.Path,
            FilePattern: "*.m4a",
            StopOnFirstError: false,
            KeepIntermediateFiles: false,
            SummaryFilePath: "summary.txt");

        var service = new FileDiscoveryService();
        var files = service.DiscoverInputFiles(options, outputExtension: extension);

        Assert.Single(files);
        Assert.EndsWith($"recording{extension}", files[0].OutputPath);
    }

    [Fact]
    public void DiscoverInputFiles_DefaultExtensionIsTxt()
    {
        using var directory = new TemporaryDirectory();
        var inputDir = Path.Combine(directory.Path, "input");
        var outputDir = Path.Combine(directory.Path, "output");
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        File.WriteAllText(Path.Combine(inputDir, "test.m4a"), "audio data");

        var options = new BatchOptions(
            InputDirectory: inputDir,
            OutputDirectory: outputDir,
            TempDirectory: directory.Path,
            FilePattern: "*.m4a",
            StopOnFirstError: false,
            KeepIntermediateFiles: false,
            SummaryFilePath: "summary.txt");

        var service = new FileDiscoveryService();
        var files = service.DiscoverInputFiles(options);

        Assert.Single(files);
        Assert.EndsWith(".txt", files[0].OutputPath);
    }
}
