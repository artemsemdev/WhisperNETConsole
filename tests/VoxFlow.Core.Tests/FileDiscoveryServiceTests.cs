using System;
using System.IO;
using System.Linq;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;
using VoxFlow.Core.Services;
using Xunit;

namespace VoxFlow.Core.Tests;

public sealed class FileDiscoveryServiceTests
{
    [Fact]
    public void DiscoverInputFiles_DirectoryWithMatchingFiles_ReturnsSortedList()
    {
        using var directory = new TemporaryDirectory();
        var inputDir = Path.Combine(directory.Path, "input");
        var outputDir = Path.Combine(directory.Path, "output");
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        File.WriteAllText(Path.Combine(inputDir, "charlie.m4a"), "audio data");
        File.WriteAllText(Path.Combine(inputDir, "alpha.m4a"), "audio data");
        File.WriteAllText(Path.Combine(inputDir, "bravo.m4a"), "audio data");

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

        Assert.Equal(3, files.Count);
        Assert.Contains("alpha.m4a", files[0].InputPath);
        Assert.Contains("bravo.m4a", files[1].InputPath);
        Assert.Contains("charlie.m4a", files[2].InputPath);
        Assert.All(files, f => Assert.Equal(DiscoveryStatus.Ready, f.Status));
    }

    [Fact]
    public void DiscoverInputFiles_NoMatchingFiles_Throws()
    {
        using var directory = new TemporaryDirectory();
        var inputDir = Path.Combine(directory.Path, "input");
        Directory.CreateDirectory(inputDir);

        File.WriteAllText(Path.Combine(inputDir, "readme.txt"), "not audio");

        var options = new BatchOptions(
            InputDirectory: inputDir,
            OutputDirectory: directory.Path,
            TempDirectory: directory.Path,
            FilePattern: "*.m4a",
            StopOnFirstError: false,
            KeepIntermediateFiles: false,
            SummaryFilePath: "summary.txt");

        var service = new FileDiscoveryService();
        var exception = Assert.Throws<InvalidOperationException>(() => service.DiscoverInputFiles(options));
        Assert.Contains("No files matching", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscoverInputFiles_EmptyFilesAreSkipped()
    {
        using var directory = new TemporaryDirectory();
        var inputDir = Path.Combine(directory.Path, "input");
        var outputDir = Path.Combine(directory.Path, "output");
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        File.WriteAllText(Path.Combine(inputDir, "good.m4a"), "audio data");
        File.WriteAllText(Path.Combine(inputDir, "empty.m4a"), "");

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

        Assert.Equal(2, files.Count);
        var emptyFile = files.First(f => Path.GetFileName(f.InputPath) == "empty.m4a");
        var goodFile = files.First(f => Path.GetFileName(f.InputPath) == "good.m4a");
        Assert.Equal(DiscoveryStatus.Skipped, emptyFile.Status);
        Assert.Equal(DiscoveryStatus.Ready, goodFile.Status);
    }

    [Fact]
    public void DiscoverInputFiles_OutputAndTempPathsAreGeneratedCorrectly()
    {
        using var directory = new TemporaryDirectory();
        var inputDir = Path.Combine(directory.Path, "input");
        var outputDir = Path.Combine(directory.Path, "output");
        var tempDir = Path.Combine(directory.Path, "temp");
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(tempDir);

        File.WriteAllText(Path.Combine(inputDir, "recording.m4a"), "audio data");

        var options = new BatchOptions(
            InputDirectory: inputDir,
            OutputDirectory: outputDir,
            TempDirectory: tempDir,
            FilePattern: "*.m4a",
            StopOnFirstError: false,
            KeepIntermediateFiles: false,
            SummaryFilePath: "summary.txt");

        var service = new FileDiscoveryService();
        var files = service.DiscoverInputFiles(options);

        Assert.Single(files);
        Assert.Equal(Path.Combine(outputDir, "recording.txt"), files[0].OutputPath);
        Assert.StartsWith(tempDir, files[0].TempWavPath);
        Assert.EndsWith(".wav", files[0].TempWavPath);
    }

    [Fact]
    public void DiscoverInputFiles_CustomFilePattern_FiltersCorrectly()
    {
        using var directory = new TemporaryDirectory();
        var inputDir = Path.Combine(directory.Path, "input");
        Directory.CreateDirectory(inputDir);

        File.WriteAllText(Path.Combine(inputDir, "audio.m4a"), "m4a data");
        File.WriteAllText(Path.Combine(inputDir, "audio.wav"), "wav data");
        File.WriteAllText(Path.Combine(inputDir, "audio.mp3"), "mp3 data");

        var options = new BatchOptions(
            InputDirectory: inputDir,
            OutputDirectory: directory.Path,
            TempDirectory: directory.Path,
            FilePattern: "*.wav",
            StopOnFirstError: false,
            KeepIntermediateFiles: false,
            SummaryFilePath: "summary.txt");

        var service = new FileDiscoveryService();
        var files = service.DiscoverInputFiles(options);

        Assert.Single(files);
        Assert.Contains("audio.wav", files[0].InputPath);
    }

    [Fact]
    public void DiscoverInputFiles_MaxFilesSpecified_ReturnsOnlyRequestedCount()
    {
        using var directory = new TemporaryDirectory();
        var inputDir = Path.Combine(directory.Path, "input");
        Directory.CreateDirectory(inputDir);

        File.WriteAllText(Path.Combine(inputDir, "charlie.m4a"), "audio data");
        File.WriteAllText(Path.Combine(inputDir, "alpha.m4a"), "audio data");
        File.WriteAllText(Path.Combine(inputDir, "bravo.m4a"), "audio data");

        var options = new BatchOptions(
            InputDirectory: inputDir,
            OutputDirectory: directory.Path,
            TempDirectory: directory.Path,
            FilePattern: "*.m4a",
            StopOnFirstError: false,
            KeepIntermediateFiles: false,
            SummaryFilePath: "summary.txt");

        var service = new FileDiscoveryService();
        var files = service.DiscoverInputFiles(options, maxFiles: 2);

        Assert.Equal(2, files.Count);
        Assert.Contains("alpha.m4a", files[0].InputPath);
        Assert.Contains("bravo.m4a", files[1].InputPath);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DiscoverInputFiles_InvalidMaxFiles_Throws(int maxFiles)
    {
        using var directory = new TemporaryDirectory();
        var inputDir = Path.Combine(directory.Path, "input");
        Directory.CreateDirectory(inputDir);
        File.WriteAllText(Path.Combine(inputDir, "audio.m4a"), "audio data");

        var options = new BatchOptions(
            InputDirectory: inputDir,
            OutputDirectory: directory.Path,
            TempDirectory: directory.Path,
            FilePattern: "*.m4a",
            StopOnFirstError: false,
            KeepIntermediateFiles: false,
            SummaryFilePath: "summary.txt");

        var service = new FileDiscoveryService();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.DiscoverInputFiles(options, maxFiles));
    }

    [Fact]
    public void DiscoverInputFiles_MissingDirectory_Throws()
    {
        var options = new BatchOptions(
            InputDirectory: Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}"),
            OutputDirectory: Path.GetTempPath(),
            TempDirectory: Path.GetTempPath(),
            FilePattern: "*.m4a",
            StopOnFirstError: false,
            KeepIntermediateFiles: false,
            SummaryFilePath: "summary.txt");

        var service = new FileDiscoveryService();
        var exception = Assert.Throws<InvalidOperationException>(() => service.DiscoverInputFiles(options));
        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiscoverInputFiles_WildcardPattern_DiscoversAllSupportedFormats()
    {
        using var directory = new TemporaryDirectory();
        var inputDir = Path.Combine(directory.Path, "input");
        var outputDir = Path.Combine(directory.Path, "output");
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        File.WriteAllText(Path.Combine(inputDir, "audio.m4a"), "data");
        File.WriteAllText(Path.Combine(inputDir, "audio.wav"), "data");
        File.WriteAllText(Path.Combine(inputDir, "audio.mp3"), "data");
        File.WriteAllText(Path.Combine(inputDir, "audio.aac"), "data");
        File.WriteAllText(Path.Combine(inputDir, "audio.flac"), "data");
        File.WriteAllText(Path.Combine(inputDir, "audio.ogg"), "data");
        File.WriteAllText(Path.Combine(inputDir, "audio.aiff"), "data");
        File.WriteAllText(Path.Combine(inputDir, "audio.mp4"), "data");
        File.WriteAllText(Path.Combine(inputDir, "readme.txt"), "not audio");
        File.WriteAllText(Path.Combine(inputDir, "notes.pdf"), "not audio");

        var options = new BatchOptions(
            InputDirectory: inputDir,
            OutputDirectory: outputDir,
            TempDirectory: directory.Path,
            FilePattern: "*",
            StopOnFirstError: false,
            KeepIntermediateFiles: false,
            SummaryFilePath: "summary.txt");

        var service = new FileDiscoveryService();
        var files = service.DiscoverInputFiles(options);

        Assert.Equal(8, files.Count);
        Assert.All(files, f => Assert.Equal(DiscoveryStatus.Ready, f.Status));
        Assert.DoesNotContain(files, f => f.InputPath.EndsWith(".txt"));
        Assert.DoesNotContain(files, f => f.InputPath.EndsWith(".pdf"));
    }

    [Fact]
    public void DiscoverInputFiles_WildcardPattern_DoesNotDuplicateFiles()
    {
        using var directory = new TemporaryDirectory();
        var inputDir = Path.Combine(directory.Path, "input");
        Directory.CreateDirectory(inputDir);

        File.WriteAllText(Path.Combine(inputDir, "recording.mp3"), "data");

        var options = new BatchOptions(
            InputDirectory: inputDir,
            OutputDirectory: directory.Path,
            TempDirectory: directory.Path,
            FilePattern: "*",
            StopOnFirstError: false,
            KeepIntermediateFiles: false,
            SummaryFilePath: "summary.txt");

        var service = new FileDiscoveryService();
        var files = service.DiscoverInputFiles(options);

        Assert.Single(files);
    }

    [Fact]
    public void DiscoverInputFiles_WildcardPattern_NoSupportedFiles_Throws()
    {
        using var directory = new TemporaryDirectory();
        var inputDir = Path.Combine(directory.Path, "input");
        Directory.CreateDirectory(inputDir);

        File.WriteAllText(Path.Combine(inputDir, "readme.txt"), "not audio");

        var options = new BatchOptions(
            InputDirectory: inputDir,
            OutputDirectory: directory.Path,
            TempDirectory: directory.Path,
            FilePattern: "*",
            StopOnFirstError: false,
            KeepIntermediateFiles: false,
            SummaryFilePath: "summary.txt");

        var service = new FileDiscoveryService();
        var exception = Assert.Throws<InvalidOperationException>(() => service.DiscoverInputFiles(options));
        Assert.Contains("No files matching", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscoverInputFiles_WildcardPattern_RespectsMaxFiles()
    {
        using var directory = new TemporaryDirectory();
        var inputDir = Path.Combine(directory.Path, "input");
        Directory.CreateDirectory(inputDir);

        File.WriteAllText(Path.Combine(inputDir, "a.mp3"), "data");
        File.WriteAllText(Path.Combine(inputDir, "b.wav"), "data");
        File.WriteAllText(Path.Combine(inputDir, "c.flac"), "data");

        var options = new BatchOptions(
            InputDirectory: inputDir,
            OutputDirectory: directory.Path,
            TempDirectory: directory.Path,
            FilePattern: "*",
            StopOnFirstError: false,
            KeepIntermediateFiles: false,
            SummaryFilePath: "summary.txt");

        var service = new FileDiscoveryService();
        var files = service.DiscoverInputFiles(options, maxFiles: 2);

        Assert.Equal(2, files.Count);
    }
}
