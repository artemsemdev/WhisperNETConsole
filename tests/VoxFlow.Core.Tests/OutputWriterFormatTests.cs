using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;
using VoxFlow.Core.Services;
using Xunit;

namespace VoxFlow.Core.Tests;

/// <summary>
/// Tests the OutputWriter end-to-end with different formats to verify
/// that format dispatch, file writing, and UTF-8 encoding work correctly.
/// </summary>
public sealed class OutputWriterFormatTests
{
    private static readonly FilteredSegment[] Segments =
    [
        new(TimeSpan.FromMilliseconds(1200), TimeSpan.FromMilliseconds(3800), "Hello, world.", 0.95),
        new(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(8500), "Second segment.", 0.88)
    ];

    private static readonly TranscriptOutputContext MetadataContext = new(
        Format: ResultFormat.Txt,
        DetectedLanguage: "English (en)",
        AcceptedSegmentCount: 2,
        SkippedSegmentCount: 1,
        Warnings: ["test warning"]);

    [Fact]
    public async Task WriteAsync_SrtFormat_WritesValidSrtFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "result.srt");
        var context = MetadataContext with { Format = ResultFormat.Srt };

        var writer = new OutputWriter();
        await writer.WriteAsync(path, Segments, context);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("1", content);
        Assert.Contains("00:00:01,200 --> 00:00:03,800", content);
        Assert.Contains("Hello, world.", content);
    }

    [Fact]
    public async Task WriteAsync_VttFormat_WritesValidVttFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "result.vtt");
        var context = MetadataContext with { Format = ResultFormat.Vtt };

        var writer = new OutputWriter();
        await writer.WriteAsync(path, Segments, context);

        var content = await File.ReadAllTextAsync(path);
        Assert.StartsWith("WEBVTT", content);
        Assert.Contains("00:00:01.200 --> 00:00:03.800", content);
    }

    [Fact]
    public async Task WriteAsync_JsonFormat_WritesValidJsonFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "result.json");
        var context = MetadataContext with { Format = ResultFormat.Json };

        var writer = new OutputWriter();
        await writer.WriteAsync(path, Segments, context);

        var content = await File.ReadAllTextAsync(path);
        var doc = JsonDocument.Parse(content);
        Assert.Equal(2, doc.RootElement.GetProperty("segments").GetArrayLength());
    }

    [Fact]
    public async Task WriteAsync_MdFormat_WritesReadableMarkdown()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "result.md");
        var context = MetadataContext with { Format = ResultFormat.Md };

        var writer = new OutputWriter();
        await writer.WriteAsync(path, Segments, context);

        var content = await File.ReadAllTextAsync(path);
        Assert.StartsWith("# Transcript", content);
        Assert.Contains("Hello, world.", content);
    }

    [Fact]
    public async Task WriteAsync_AllFormats_ProduceUtf8WithoutBom()
    {
        foreach (var format in Enum.GetValues<ResultFormat>())
        {
            using var directory = new TemporaryDirectory();
            var path = Path.Combine(directory.Path, $"result{format.ToFileExtension()}");
            var context = MetadataContext with { Format = format };

            var writer = new OutputWriter();
            await writer.WriteAsync(path, Segments, context);

            var bytes = await File.ReadAllBytesAsync(path);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                $"Format {format}: output file should not contain a UTF-8 BOM.");
        }
    }

    [Fact]
    public void BuildOutputText_TxtFormat_MatchesLegacyBehavior()
    {
        var context = new TranscriptOutputContext(ResultFormat.Txt);
        var writer = new OutputWriter();
        var output = writer.BuildOutputText(Segments, context);

        // Verify it matches the exact legacy format
        Assert.Contains("00:00:01.2000000->00:00:03.8000000: Hello, world.", output);
        Assert.Contains("00:00:05->00:00:08.5000000: Second segment.", output);
    }
}
