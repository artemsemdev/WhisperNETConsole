using System;
using System.Collections.Generic;
using System.Text.Json;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;
using VoxFlow.Core.Services.Formatters;
using Xunit;

namespace VoxFlow.Core.Tests;

public sealed class TranscriptFormatterTests
{
    private static readonly IReadOnlyList<FilteredSegment> SampleSegments = new[]
    {
        new FilteredSegment(TimeSpan.FromMilliseconds(1200), TimeSpan.FromMilliseconds(3800), "Hello, this is a test.", 0.95),
        new FilteredSegment(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(8500), "Second line here.", 0.88)
    };

    private static readonly TranscriptOutputContext DefaultContext = new(
        Format: ResultFormat.Txt,
        DetectedLanguage: "English (en)",
        AcceptedSegmentCount: 2,
        SkippedSegmentCount: 1,
        Warnings: new[] { "low quality" });

    // -----------------------------------------------------------------------
    // TXT formatter
    // -----------------------------------------------------------------------

    [Fact]
    public void TxtFormatter_ProducesLegacyTimestampedFormat()
    {
        var formatter = new TxtTranscriptFormatter();
        var output = formatter.Format(SampleSegments, DefaultContext);

        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("->", lines[0]);
        Assert.Contains(": Hello, this is a test.", lines[0]);
        Assert.Contains(": Second line here.", lines[1]);
    }

    [Fact]
    public void TxtFormatter_EmptySegments_ReturnsEmptyString()
    {
        var formatter = new TxtTranscriptFormatter();
        var output = formatter.Format(Array.Empty<FilteredSegment>(), DefaultContext);
        Assert.Equal(string.Empty, output);
    }

    [Fact]
    public void TxtFormatter_WithTwoSpeakerDocument_RendersSpeakerPrefixes()
    {
        var formatter = new TxtTranscriptFormatter();
        var document = BuildTwoSpeakerDocument();
        var context = DefaultContext with { SpeakerTranscript = document };

        var output = formatter.Format(SampleSegments, context);

        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal("Speaker A: Hello there", lines[0]);
        Assert.Equal("Speaker B: General Kenobi", lines[1]);
    }

    [Fact]
    public void TxtFormatter_WithNullSpeakerTranscript_ProducesLegacyOutputUnchanged()
    {
        var formatter = new TxtTranscriptFormatter();
        var withoutSpeakers = formatter.Format(SampleSegments, DefaultContext);
        var explicitNull = formatter.Format(
            SampleSegments,
            DefaultContext with { SpeakerTranscript = null });

        Assert.Equal(withoutSpeakers, explicitNull);
        Assert.Contains("->", withoutSpeakers);
        Assert.Contains(": Hello, this is a test.", withoutSpeakers);
        Assert.DoesNotContain("Speaker", withoutSpeakers);
    }

    // -----------------------------------------------------------------------
    // SRT formatter
    // -----------------------------------------------------------------------

    [Fact]
    public void SrtFormatter_ProducesNumberedCuesWithCorrectTimestamps()
    {
        var formatter = new SrtTranscriptFormatter();
        var output = formatter.Format(SampleSegments, DefaultContext);

        Assert.Contains("1", output);
        Assert.Contains("2", output);
        Assert.Contains("00:00:01,200 --> 00:00:03,800", output);
        Assert.Contains("Hello, this is a test.", output);
        Assert.Contains("00:00:05,000 --> 00:00:08,500", output);
        Assert.Contains("Second line here.", output);
    }

    [Fact]
    public void SrtFormatter_TimestampFormat_UsesCommaMilliseconds()
    {
        var ts = TimeSpan.FromMilliseconds(3723456); // 1h 2m 3s 456ms
        var formatted = SrtTranscriptFormatter.FormatSrtTimestamp(ts);
        Assert.Equal("01:02:03,456", formatted);
    }

    [Fact]
    public void SrtFormatter_TimestampFormat_ZeroTimestamp()
    {
        var formatted = SrtTranscriptFormatter.FormatSrtTimestamp(TimeSpan.Zero);
        Assert.Equal("00:00:00,000", formatted);
    }

    [Fact]
    public void SrtFormatter_EmptySegments_ReturnsEmptyString()
    {
        var formatter = new SrtTranscriptFormatter();
        var output = formatter.Format(Array.Empty<FilteredSegment>(), DefaultContext);
        Assert.Equal(string.Empty, output);
    }

    [Fact]
    public void SrtFormatter_WithTwoSpeakerDocument_RendersSpeakerPrefixes()
    {
        var formatter = new SrtTranscriptFormatter();
        var context = DefaultContext with { SpeakerTranscript = BuildSampleAlignedDocument() };

        var output = formatter.Format(SampleSegments, context);

        Assert.Contains("00:00:01,200 --> 00:00:03,800", output);
        Assert.Contains("Speaker A: Hello, this is a test.", output);
        Assert.Contains("00:00:05,000 --> 00:00:08,500", output);
        Assert.Contains("Speaker B: Second line here.", output);
    }

    [Fact]
    public void SrtFormatter_WithNullSpeakerTranscript_ProducesLegacyOutputUnchanged()
    {
        var formatter = new SrtTranscriptFormatter();
        var withoutSpeakers = formatter.Format(SampleSegments, DefaultContext);
        var explicitNull = formatter.Format(
            SampleSegments,
            DefaultContext with { SpeakerTranscript = null });

        Assert.Equal(withoutSpeakers, explicitNull);
        Assert.DoesNotContain("Speaker", withoutSpeakers);
    }

    // -----------------------------------------------------------------------
    // VTT formatter
    // -----------------------------------------------------------------------

    [Fact]
    public void VttFormatter_StartsWithWebVttHeader()
    {
        var formatter = new VttTranscriptFormatter();
        var output = formatter.Format(SampleSegments, DefaultContext);

        Assert.StartsWith("WEBVTT", output);
    }

    [Fact]
    public void VttFormatter_UsesDotMilliseconds()
    {
        var formatter = new VttTranscriptFormatter();
        var output = formatter.Format(SampleSegments, DefaultContext);

        Assert.Contains("00:00:01.200 --> 00:00:03.800", output);
        Assert.Contains("00:00:05.000 --> 00:00:08.500", output);
    }

    [Fact]
    public void VttFormatter_TimestampFormat_UsesDotSeparator()
    {
        var ts = TimeSpan.FromMilliseconds(3723456);
        var formatted = VttTranscriptFormatter.FormatVttTimestamp(ts);
        Assert.Equal("01:02:03.456", formatted);
    }

    [Fact]
    public void VttFormatter_EmptySegments_StillProducesHeader()
    {
        var formatter = new VttTranscriptFormatter();
        var output = formatter.Format(Array.Empty<FilteredSegment>(), DefaultContext);

        Assert.StartsWith("WEBVTT", output);
    }

    [Fact]
    public void VttFormatter_WithTwoSpeakerDocument_RendersVoiceTags()
    {
        var formatter = new VttTranscriptFormatter();
        var context = DefaultContext with { SpeakerTranscript = BuildSampleAlignedDocument() };

        var output = formatter.Format(SampleSegments, context);

        Assert.Contains("<v Speaker A>Hello, this is a test.", output);
        Assert.Contains("<v Speaker B>Second line here.", output);
    }

    [Fact]
    public void VttFormatter_WithNullSpeakerTranscript_ProducesLegacyOutputUnchanged()
    {
        var formatter = new VttTranscriptFormatter();
        var withoutSpeakers = formatter.Format(SampleSegments, DefaultContext);
        var explicitNull = formatter.Format(
            SampleSegments,
            DefaultContext with { SpeakerTranscript = null });

        Assert.Equal(withoutSpeakers, explicitNull);
        Assert.DoesNotContain("<v ", withoutSpeakers);
    }

    // -----------------------------------------------------------------------
    // JSON formatter
    // -----------------------------------------------------------------------

    [Fact]
    public void JsonFormatter_ProducesValidJson()
    {
        var formatter = new JsonTranscriptFormatter();
        var output = formatter.Format(SampleSegments, DefaultContext);

        // Should parse without error
        var doc = JsonDocument.Parse(output);
        Assert.NotNull(doc);
    }

    [Fact]
    public void JsonFormatter_IncludesMetadata()
    {
        var formatter = new JsonTranscriptFormatter();
        var output = formatter.Format(SampleSegments, DefaultContext);

        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        Assert.Equal("txt", root.GetProperty("format").GetString());
        Assert.Equal("English (en)", root.GetProperty("detectedLanguage").GetString());
        Assert.Equal(2, root.GetProperty("acceptedSegmentCount").GetInt32());
        Assert.Equal(1, root.GetProperty("skippedSegmentCount").GetInt32());
        Assert.Single(root.GetProperty("warnings").EnumerateArray());
    }

    [Fact]
    public void JsonFormatter_IncludesSegments()
    {
        var formatter = new JsonTranscriptFormatter();
        var output = formatter.Format(SampleSegments, DefaultContext);

        using var doc = JsonDocument.Parse(output);
        var segments = doc.RootElement.GetProperty("segments");

        Assert.Equal(2, segments.GetArrayLength());
        var first = segments[0];
        Assert.Equal("00:00:01.200", first.GetProperty("start").GetString());
        Assert.Equal("00:00:03.800", first.GetProperty("end").GetString());
        Assert.Equal("Hello, this is a test.", first.GetProperty("text").GetString());
    }

    [Fact]
    public void JsonFormatter_WithTwoSpeakerDocument_IncludesSpeakerTranscript()
    {
        var formatter = new JsonTranscriptFormatter();
        var context = DefaultContext with { SpeakerTranscript = BuildTwoSpeakerDocument() };

        var output = formatter.Format(SampleSegments, context);

        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("speakerTranscript", out var speakerTranscript),
            "expected top-level speakerTranscript property");

        var speakers = speakerTranscript.GetProperty("speakers");
        Assert.Equal(2, speakers.GetArrayLength());
        Assert.Equal("A", speakers[0].GetProperty("id").GetString());
        Assert.Equal("B", speakers[1].GetProperty("id").GetString());

        var turns = speakerTranscript.GetProperty("turns");
        Assert.Equal(2, turns.GetArrayLength());
        Assert.Equal("A", turns[0].GetProperty("speakerId").GetString());
        Assert.Equal("B", turns[1].GetProperty("speakerId").GetString());

        // Existing segment-based shape must still be present.
        Assert.Equal(2, root.GetProperty("segments").GetArrayLength());
    }

    [Fact]
    public void JsonFormatter_WithNullSpeakerTranscript_ProducesLegacyOutputUnchanged()
    {
        var formatter = new JsonTranscriptFormatter();
        var withoutSpeakers = formatter.Format(SampleSegments, DefaultContext);
        var explicitNull = formatter.Format(
            SampleSegments,
            DefaultContext with { SpeakerTranscript = null });

        Assert.Equal(withoutSpeakers, explicitNull);
        using var doc = JsonDocument.Parse(withoutSpeakers);
        Assert.False(doc.RootElement.TryGetProperty("speakerTranscript", out _));
    }

    [Fact]
    public void JsonFormatter_IncludesPlainTranscript()
    {
        var formatter = new JsonTranscriptFormatter();
        var output = formatter.Format(SampleSegments, DefaultContext);

        using var doc = JsonDocument.Parse(output);
        var transcript = doc.RootElement.GetProperty("transcript").GetString();
        Assert.Contains("Hello, this is a test.", transcript);
        Assert.Contains("Second line here.", transcript);
    }

    // -----------------------------------------------------------------------
    // MD formatter
    // -----------------------------------------------------------------------

    [Fact]
    public void MdFormatter_StartsWithTranscriptHeader()
    {
        var formatter = new MdTranscriptFormatter();
        var output = formatter.Format(SampleSegments, DefaultContext);

        Assert.StartsWith("# Transcript", output);
    }

    [Fact]
    public void MdFormatter_IncludesMetadata()
    {
        var formatter = new MdTranscriptFormatter();
        var output = formatter.Format(SampleSegments, DefaultContext);

        Assert.Contains("**Language:** English (en)", output);
        Assert.Contains("**Segments:** 2 accepted, 1 skipped", output);
        Assert.Contains("**Warnings:** low quality", output);
    }

    [Fact]
    public void MdFormatter_IncludesTimestampedEntries()
    {
        var formatter = new MdTranscriptFormatter();
        var output = formatter.Format(SampleSegments, DefaultContext);

        Assert.Contains("**[00:00:01]** Hello, this is a test.", output);
        Assert.Contains("**[00:00:05]** Second line here.", output);
    }

    [Fact]
    public void MdFormatter_IsReadableMarkdown()
    {
        var formatter = new MdTranscriptFormatter();
        var output = formatter.Format(SampleSegments, DefaultContext);

        // Should contain horizontal rule separator
        Assert.Contains("---", output);
        // Should not contain raw JSON
        Assert.DoesNotContain("{", output);
    }

    [Fact]
    public void MdFormatter_WithTwoSpeakerDocument_RendersSpeakerPrefixes()
    {
        var formatter = new MdTranscriptFormatter();
        var context = DefaultContext with { SpeakerTranscript = BuildTwoSpeakerDocument() };

        var output = formatter.Format(SampleSegments, context);

        Assert.Contains("**Speaker A:** Hello there", output);
        Assert.Contains("**Speaker B:** General Kenobi", output);
    }

    [Fact]
    public void MdFormatter_WithNullSpeakerTranscript_ProducesLegacyOutputUnchanged()
    {
        var formatter = new MdTranscriptFormatter();
        var withoutSpeakers = formatter.Format(SampleSegments, DefaultContext);
        var explicitNull = formatter.Format(
            SampleSegments,
            DefaultContext with { SpeakerTranscript = null });

        Assert.Equal(withoutSpeakers, explicitNull);
        Assert.DoesNotContain("**Speaker", withoutSpeakers);
    }

    // -----------------------------------------------------------------------
    // TranscriptFormatterFactory
    // -----------------------------------------------------------------------

    private static TranscriptDocument BuildSampleAlignedDocument()
    {
        // Two turns whose intervals align with SampleSegments, so SRT/VTT
        // segment→speaker overlap mapping is unambiguous.
        var words = new[]
        {
            new TranscriptWord(TimeSpan.FromMilliseconds(1200), TimeSpan.FromMilliseconds(3800), "first", "A"),
            new TranscriptWord(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(8500), "second", "B")
        };
        var turns = SpeakerTurn.GroupConsecutive(words);
        var speakers = new[]
        {
            new SpeakerInfo("A", "A", TimeSpan.FromMilliseconds(2600)),
            new SpeakerInfo("B", "B", TimeSpan.FromMilliseconds(3500))
        };
        return new TranscriptDocument(
            speakers,
            words,
            turns,
            new TranscriptMetadata(1, "pyannote/test", 1));
    }

    private static TranscriptDocument BuildTwoSpeakerDocument()
    {
        var words = new[]
        {
            new TranscriptWord(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1), "Hello", "A"),
            new TranscriptWord(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "there", "A"),
            new TranscriptWord(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), "General", "B"),
            new TranscriptWord(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4), "Kenobi", "B")
        };
        var turns = SpeakerTurn.GroupConsecutive(words);
        var speakers = new[]
        {
            new SpeakerInfo("A", "A", TimeSpan.FromSeconds(2)),
            new SpeakerInfo("B", "B", TimeSpan.FromSeconds(2))
        };
        return new TranscriptDocument(
            speakers,
            words,
            turns,
            new TranscriptMetadata(1, "pyannote/test", 1));
    }

    [Theory]
    [InlineData(ResultFormat.Txt)]
    [InlineData(ResultFormat.Srt)]
    [InlineData(ResultFormat.Vtt)]
    [InlineData(ResultFormat.Json)]
    [InlineData(ResultFormat.Md)]
    public void Factory_ReturnsFormatterForAllFormats(ResultFormat format)
    {
        var formatter = TranscriptFormatterFactory.GetFormatter(format);
        Assert.NotNull(formatter);

        // Should produce non-null output
        var output = formatter.Format(SampleSegments, new TranscriptOutputContext(format));
        Assert.NotNull(output);
        Assert.NotEmpty(output);
    }
}
