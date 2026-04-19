using System;
using System.IO;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;
using VoxFlow.Core.Services;
using Whisper.net;
using Xunit;

namespace VoxFlow.Core.Tests;

public sealed class TranscriptionFilterTests
{
    [Fact]
    public void FilterSegments_SkipsNoiseAndLowValueSegments()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: "/tmp/input.m4a",
            wavFilePath: "/tmp/output.wav",
            resultFilePath: "/tmp/result.txt",
            modelFilePath: "/tmp/model.bin",
            ffmpegExecutablePath: "ffmpeg");

        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var language = new SupportedLanguage("en", "English", 0);
        var filter = new TranscriptionFilter();

        var segments = new[]
        {
            CreateSegment("   ", 0.90f, 1),
            CreateSegment("[music]", 0.90f, 1),
            CreateSegment("[door opening]", 0.90f, 1),
            CreateSegment("hi", 0.10f, 1),
            CreateSegment("...", 0.90f, 1),
            CreateSegment("tiny", 0.90f, 31),
            CreateSegment("Repeated phrase.", 0.90f, 2),
            CreateSegment("Repeated phrase.", 0.90f, 2),
            CreateSegment("Repeated phrase.", 0.90f, 2),
            CreateSegment("  valid   speech  ", 0.90f, 3)
        };

        var result = filter.FilterSegments(language, segments, options);

        Assert.Equal(3, result.Accepted.Count);
        Assert.Equal("Repeated phrase.", result.Accepted[0].Text);
        Assert.Equal("Repeated phrase.", result.Accepted[1].Text);
        Assert.Equal("valid speech", result.Accepted[2].Text);
        Assert.Equal(7, result.Skipped.Count);
        Assert.Contains(result.Skipped, segment => segment.Reason == SegmentSkipReason.EmptyText);
        Assert.Contains(result.Skipped, segment => segment.Reason == SegmentSkipReason.NoiseMarker);
        Assert.Contains(result.Skipped, segment => segment.Reason == SegmentSkipReason.LowProbability);
        Assert.Contains(result.Skipped, segment => segment.Reason == SegmentSkipReason.SuspiciousNonSpeech);
        Assert.Contains(result.Skipped, segment => segment.Reason == SegmentSkipReason.LowInformationLong);
        Assert.Contains(result.Skipped, segment => segment.Reason == SegmentSkipReason.RepetitiveLoop);
    }

    [Fact]
    public void FilterSegments_AcceptsAllValidSegments_WhenNoFilterTriggered()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: "/tmp/input.m4a",
            wavFilePath: "/tmp/output.wav",
            resultFilePath: "/tmp/result.txt",
            modelFilePath: "/tmp/model.bin",
            ffmpegExecutablePath: "ffmpeg");

        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var language = new SupportedLanguage("en", "English", 0);
        var filter = new TranscriptionFilter();

        var segments = new[]
        {
            CreateSegment("Hello world", 0.95f, 2),
            CreateSegment("This is a test", 0.88f, 3),
            CreateSegment("Goodbye", 0.72f, 1)
        };

        var result = filter.FilterSegments(language, segments, options);

        Assert.Equal(3, result.Accepted.Count);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void FilterSegments_SkipsBracketedNonSpeechPlaceholders()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: "/tmp/input.m4a",
            wavFilePath: "/tmp/output.wav",
            resultFilePath: "/tmp/result.txt",
            modelFilePath: "/tmp/model.bin",
            ffmpegExecutablePath: "ffmpeg",
            suppressBracketedNonSpeechSegments: true);

        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var language = new SupportedLanguage("en", "English", 0);
        var filter = new TranscriptionFilter();

        var segments = new[]
        {
            CreateSegment("[door opening]", 0.90f, 1),
            CreateSegment("(clapping)", 0.90f, 1),
            CreateSegment("[This is a real sentence.]", 0.90f, 1),
            CreateSegment("Normal speech", 0.90f, 2)
        };

        var result = filter.FilterSegments(language, segments, options);

        Assert.Equal(2, result.Accepted.Count);
        Assert.Equal("[This is a real sentence.]", result.Accepted[0].Text);
        Assert.Equal("Normal speech", result.Accepted[1].Text);
    }

    [Fact]
    public void FilterSegments_NormalizesWhitespaceInAcceptedSegments()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: "/tmp/input.m4a",
            wavFilePath: "/tmp/output.wav",
            resultFilePath: "/tmp/result.txt",
            modelFilePath: "/tmp/model.bin",
            ffmpegExecutablePath: "ffmpeg");

        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var language = new SupportedLanguage("en", "English", 0);
        var filter = new TranscriptionFilter();

        var segments = new[]
        {
            CreateSegment("  multiple   spaces   here  ", 0.90f, 2),
            CreateSegment("\ttabs\tand\tnewlines\n", 0.90f, 2)
        };

        var result = filter.FilterSegments(language, segments, options);

        Assert.Equal(2, result.Accepted.Count);
        Assert.Equal("multiple spaces here", result.Accepted[0].Text);
        Assert.Equal("tabs and newlines", result.Accepted[1].Text);
    }

    [Fact]
    public void FilterSegments_PreservesWordTokens_FromAcceptedSegments()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: "/tmp/input.m4a",
            wavFilePath: "/tmp/output.wav",
            resultFilePath: "/tmp/result.txt",
            modelFilePath: "/tmp/model.bin",
            ffmpegExecutablePath: "ffmpeg");

        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var language = new SupportedLanguage("en", "English", 0);
        var filter = new TranscriptionFilter();

        var tokens = new[]
        {
            CreateToken("Hello", start: 0, end: 50),
            CreateToken(" world", start: 50, end: 110),
            CreateToken("!", start: 110, end: 130)
        };

        var segments = new[]
        {
            CreateSegmentWithTokens("Hello world!", 0.95f, durationSeconds: 2, tokens)
        };

        var result = filter.FilterSegments(language, segments, options);

        var accepted = Assert.Single(result.Accepted);
        Assert.Equal(3, accepted.Words.Count);
        Assert.Equal("Hello", accepted.Words[0].Text);
        Assert.Equal(0, accepted.Words[0].Start);
        Assert.Equal(50, accepted.Words[0].End);
        Assert.Equal(" world", accepted.Words[1].Text);
        Assert.Equal("!", accepted.Words[2].Text);
        Assert.Equal(110, accepted.Words[2].Start);
        Assert.Equal(130, accepted.Words[2].End);
    }

    [Fact]
    public void FilterSegments_SkippedSegmentDoesNotLeakTokens_ToAcceptedNeighbor()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: "/tmp/input.m4a",
            wavFilePath: "/tmp/output.wav",
            resultFilePath: "/tmp/result.txt",
            modelFilePath: "/tmp/model.bin",
            ffmpegExecutablePath: "ffmpeg");

        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var language = new SupportedLanguage("en", "English", 0);
        var filter = new TranscriptionFilter();

        var noiseTokens = new[]
        {
            CreateToken("[music]", start: 0, end: 100)
        };
        var speechTokens = new[]
        {
            CreateToken("Hello", start: 100, end: 150),
            CreateToken(" world", start: 150, end: 210)
        };

        var segments = new[]
        {
            CreateSegmentWithTokens("[music]", 0.90f, durationSeconds: 1, noiseTokens),
            CreateSegmentWithTokens("Hello world", 0.90f, durationSeconds: 2, speechTokens)
        };

        var result = filter.FilterSegments(language, segments, options);

        var accepted = Assert.Single(result.Accepted);
        Assert.Equal("Hello world", accepted.Text);
        Assert.Equal(2, accepted.Words.Count);
        Assert.Equal("Hello", accepted.Words[0].Text);
        Assert.Equal(" world", accepted.Words[1].Text);
        Assert.DoesNotContain(accepted.Words, token => token.Text == "[music]");
    }

    [Fact]
    public void FilterSegments_DuplicateLoopFilter_DropsTokensAlongsideSegment()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: "/tmp/input.m4a",
            wavFilePath: "/tmp/output.wav",
            resultFilePath: "/tmp/result.txt",
            modelFilePath: "/tmp/model.bin",
            ffmpegExecutablePath: "ffmpeg");

        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var language = new SupportedLanguage("en", "English", 0);
        var filter = new TranscriptionFilter();

        var firstLoopTokens = new[] { CreateToken("Repeat.", start: 0, end: 50) };
        var secondLoopTokens = new[] { CreateToken("Repeat.", start: 200, end: 250) };
        var thirdLoopTokens = new[] { CreateToken("Repeat.", start: 400, end: 450) };
        var droppedLoopTokens = new[] { CreateToken("Repeat.", start: 600, end: 650) };

        var segments = new[]
        {
            CreateSegmentWithTokens("Repeat.", 0.90f, durationSeconds: 2, firstLoopTokens),
            CreateSegmentWithTokens("Repeat.", 0.90f, durationSeconds: 2, secondLoopTokens),
            CreateSegmentWithTokens("Repeat.", 0.90f, durationSeconds: 2, thirdLoopTokens),
            CreateSegmentWithTokens("Repeat.", 0.90f, durationSeconds: 2, droppedLoopTokens)
        };

        var result = filter.FilterSegments(language, segments, options);

        // Default MaxConsecutiveDuplicateSegments is 2: first two copies stay,
        // the 3rd and 4th are routed to Skipped with RepetitiveLoop.
        Assert.Equal(2, result.Accepted.Count);
        Assert.Single(result.Accepted[0].Words);
        Assert.Equal(0, result.Accepted[0].Words[0].Start);
        Assert.Single(result.Accepted[1].Words);
        Assert.Equal(200, result.Accepted[1].Words[0].Start);

        Assert.Equal(2, result.Skipped.Count);
        Assert.All(result.Skipped, skipped =>
            Assert.Equal(SegmentSkipReason.RepetitiveLoop, skipped.Reason));

        // None of the accepted segments should have inherited the 3rd or 4th copy's tokens.
        Assert.DoesNotContain(
            result.Accepted.SelectMany(segment => segment.Words),
            token => token.Start == 400 || token.Start == 600);
    }

    [Fact]
    public void FilterSegments_ReturnsEmptyForEmptyInput()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: "/tmp/input.m4a",
            wavFilePath: "/tmp/output.wav",
            resultFilePath: "/tmp/result.txt",
            modelFilePath: "/tmp/model.bin",
            ffmpegExecutablePath: "ffmpeg");

        var options = TranscriptionOptions.LoadFromPath(settingsPath);
        var language = new SupportedLanguage("en", "English", 0);
        var filter = new TranscriptionFilter();

        var result = filter.FilterSegments(language, Array.Empty<SegmentData>(), options);

        Assert.Empty(result.Accepted);
        Assert.Empty(result.Skipped);
    }

    private static SegmentData CreateSegment(string text, float probability, int durationSeconds)
    {
        return CreateSegmentWithTokens(text, probability, durationSeconds, Array.Empty<WhisperToken>());
    }

    private static SegmentData CreateSegmentWithTokens(
        string text,
        float probability,
        int durationSeconds,
        WhisperToken[] tokens)
    {
        return new SegmentData(
            text,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(durationSeconds),
            probability,
            probability,
            probability,
            probability,
            "en",
            tokens);
    }

    private static WhisperToken CreateToken(string text, long start, long end)
    {
        return new WhisperToken
        {
            Text = text,
            Start = start,
            End = end,
            Probability = 1.0f
        };
    }
}
