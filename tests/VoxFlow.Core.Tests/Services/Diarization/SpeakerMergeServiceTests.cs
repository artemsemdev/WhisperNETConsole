using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using VoxFlow.Core.Models;
using VoxFlow.Core.Services.Diarization;
using Whisper.net;
using Xunit;

namespace VoxFlow.Core.Tests.Services.Diarization;

public sealed class SpeakerMergeServiceTests
{
    private static readonly TranscriptMetadata DefaultMetadata = new(
        SchemaVersion: 1,
        DiarizationModel: "pyannote/speaker-diarization-3.1",
        SidecarVersion: 1);

    [Fact]
    public void Merge_EmptyInputs_ReturnsEmptyDocument()
    {
        var service = new SpeakerMergeService();

        var result = service.Merge(
            segments: Array.Empty<FilteredSegment>(),
            diarization: new DiarizationResult(
                Version: 1,
                Speakers: Array.Empty<DiarizationSpeaker>(),
                Segments: Array.Empty<DiarizationSegment>()),
            metadata: DefaultMetadata);

        Assert.Empty(result.Words);
        Assert.Empty(result.Turns);
        Assert.Empty(result.Speakers);
        Assert.Equal(DefaultMetadata, result.Metadata);
    }

    [Fact]
    public void Merge_SingleSpeaker_AllWordsAssignedToA()
    {
        var service = new SpeakerMergeService();
        var segments = new[]
        {
            SegmentFactory.Create(0.0, 3.0, "hello world today",
                Tok("hello", 0, 50),
                Tok("world", 60, 120),
                Tok("today", 140, 290))
        };
        var diarization = new DiarizationResult(
            Version: 1,
            Speakers: new[] { new DiarizationSpeaker("A", 3.0) },
            Segments: new[] { new DiarizationSegment("A", 0.0, 3.0) });

        var result = service.Merge(segments, diarization, DefaultMetadata);

        Assert.Equal(3, result.Words.Count);
        Assert.All(result.Words, w => Assert.Equal("A", w.SpeakerId));
        Assert.Equal("hello", result.Words[0].Text);
        Assert.Equal("world", result.Words[1].Text);
        Assert.Equal("today", result.Words[2].Text);
    }

    [Fact]
    public void Merge_TwoSpeakers_AssignsByMaxTimeOverlap()
    {
        var service = new SpeakerMergeService();
        // Six tokens within a single filtered segment starting at 0s.
        // First four tokens inside A (0..2s), last two inside B (2..3s).
        var segments = new[]
        {
            SegmentFactory.Create(0.0, 3.0, "one two three four five six",
                Tok("one",   0,   20),
                Tok("two",   30,  60),
                Tok("three", 80,  120),
                Tok("four",  140, 190),
                Tok("five",  210, 240),
                Tok("six",   260, 290))
        };
        var diarization = new DiarizationResult(
            Version: 1,
            Speakers: new[]
            {
                new DiarizationSpeaker("A", 2.0),
                new DiarizationSpeaker("B", 1.0)
            },
            Segments: new[]
            {
                new DiarizationSegment("A", 0.0, 2.0),
                new DiarizationSegment("B", 2.0, 3.0)
            });

        var result = service.Merge(segments, diarization, DefaultMetadata);

        Assert.Equal(6, result.Words.Count);
        Assert.Equal("A", result.Words[0].SpeakerId);
        Assert.Equal("A", result.Words[1].SpeakerId);
        Assert.Equal("A", result.Words[2].SpeakerId);
        Assert.Equal("A", result.Words[3].SpeakerId);
        Assert.Equal("B", result.Words[4].SpeakerId);
        Assert.Equal("B", result.Words[5].SpeakerId);
    }

    [Fact]
    public void Merge_WordStraddlingTwoSpeakers_AssignsToMaxOverlap()
    {
        var service = new SpeakerMergeService();
        // "first" sits firmly in A (anchors ordinal "A"); "boundary" spans
        // 1.8..2.3s — 0.2s in A, 0.3s in B → B wins.
        var segments = new[]
        {
            SegmentFactory.Create(0.0, 3.0, "first boundary",
                Tok("first",    0,   50),
                Tok("boundary", 180, 230))
        };
        var diarization = new DiarizationResult(
            Version: 1,
            Speakers: new[]
            {
                new DiarizationSpeaker("A", 2.0),
                new DiarizationSpeaker("B", 1.0)
            },
            Segments: new[]
            {
                new DiarizationSegment("A", 0.0, 2.0),
                new DiarizationSegment("B", 2.0, 3.0)
            });

        var result = service.Merge(segments, diarization, DefaultMetadata);

        Assert.Equal(2, result.Words.Count);
        Assert.Equal("A", result.Words[0].SpeakerId);
        Assert.Equal("B", result.Words[1].SpeakerId);
    }

    [Fact]
    public void Merge_WordNotCoveredByAnySegment_AssignsToNearestSpeaker()
    {
        var service = new SpeakerMergeService();
        // Word at 2.1..2.3s falls in the silence gap between A (ends 2.0s) and B (starts 2.5s).
        // A's end (2.0) is 0.1s away; B's start (2.5) is 0.2s away → A wins.
        var segments = new[]
        {
            SegmentFactory.Create(0.0, 3.0, "gap",
                Tok("gap", 210, 230))
        };
        var diarization = new DiarizationResult(
            Version: 1,
            Speakers: new[]
            {
                new DiarizationSpeaker("A", 2.0),
                new DiarizationSpeaker("B", 0.5)
            },
            Segments: new[]
            {
                // B first in list to ensure we don't accidentally pick it via first-segment fallback.
                new DiarizationSegment("B", 2.5, 3.0),
                new DiarizationSegment("A", 0.0, 2.0)
            });

        var result = service.Merge(segments, diarization, DefaultMetadata);

        var word = Assert.Single(result.Words);
        Assert.Equal("A", word.SpeakerId);
    }

    [Fact]
    public void Merge_OrdinalLabels_FirstAppearanceWins()
    {
        var service = new SpeakerMergeService();
        // Sidecar reports "B" first (at 0s) and "A" later (at 5s). Merge must
        // renormalize so the earliest speaker is labeled "A" and the later one "B".
        var segments = new[]
        {
            SegmentFactory.Create(0.0, 10.0, "one two",
                Tok("one", 0, 100),
                Tok("two", 500, 600))
        };
        var diarization = new DiarizationResult(
            Version: 1,
            Speakers: new[]
            {
                new DiarizationSpeaker("B", 5.0),
                new DiarizationSpeaker("A", 5.0)
            },
            Segments: new[]
            {
                new DiarizationSegment("B", 0.0, 5.0),
                new DiarizationSegment("A", 5.0, 10.0)
            });

        var result = service.Merge(segments, diarization, DefaultMetadata);

        Assert.Equal("A", result.Words[0].SpeakerId);
        Assert.Equal("B", result.Words[1].SpeakerId);
    }

    [Fact]
    public void Merge_ProducesCorrectTurnBoundaries()
    {
        var service = new SpeakerMergeService();
        var segments = new[]
        {
            SegmentFactory.Create(0.0, 6.0, "a1 a2 b1 a3",
                Tok("a1", 0,   50),
                Tok("a2", 60,  110),
                Tok("b1", 250, 320),
                Tok("a3", 450, 500))
        };
        var diarization = new DiarizationResult(
            Version: 1,
            Speakers: new[]
            {
                new DiarizationSpeaker("A", 4.0),
                new DiarizationSpeaker("B", 2.0)
            },
            Segments: new[]
            {
                new DiarizationSegment("A", 0.0, 2.0),
                new DiarizationSegment("B", 2.0, 4.0),
                new DiarizationSegment("A", 4.0, 6.0)
            });

        var result = service.Merge(segments, diarization, DefaultMetadata);

        var expected = SpeakerTurn.GroupConsecutive(result.Words);
        Assert.Equal(expected.Count, result.Turns.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].SpeakerId, result.Turns[i].SpeakerId);
            Assert.Equal(expected[i].StartTime, result.Turns[i].StartTime);
            Assert.Equal(expected[i].EndTime, result.Turns[i].EndTime);
            Assert.Equal(expected[i].Words.Count, result.Turns[i].Words.Count);
        }
        // Sanity: three distinct turns A-B-A.
        Assert.Equal(3, result.Turns.Count);
        Assert.Equal("A", result.Turns[0].SpeakerId);
        Assert.Equal("B", result.Turns[1].SpeakerId);
        Assert.Equal("A", result.Turns[2].SpeakerId);
    }

    [Fact]
    public void Merge_ComputesTotalSpeechDurationPerSpeaker()
    {
        var service = new SpeakerMergeService();
        var segments = new[]
        {
            SegmentFactory.Create(0.0, 6.0, "a1 a2 b1 a3",
                Tok("a1", 0,   50),
                Tok("a2", 60,  110),
                Tok("b1", 250, 320),
                Tok("a3", 450, 500))
        };
        var diarization = new DiarizationResult(
            Version: 1,
            Speakers: new[]
            {
                new DiarizationSpeaker("A", 4.0),
                new DiarizationSpeaker("B", 2.0)
            },
            Segments: new[]
            {
                new DiarizationSegment("A", 0.0, 2.0),
                new DiarizationSegment("B", 2.0, 4.0),
                new DiarizationSegment("A", 4.0, 6.0)
            });

        var result = service.Merge(segments, diarization, DefaultMetadata);

        var roster = result.Speakers.ToDictionary(s => s.Id);
        // Expected = sum of turn durations per speaker.
        var expectedA = TimeSpan.Zero;
        var expectedB = TimeSpan.Zero;
        foreach (var turn in result.Turns)
        {
            var duration = turn.EndTime - turn.StartTime;
            if (turn.SpeakerId == "A")
            {
                expectedA += duration;
            }
            else
            {
                expectedB += duration;
            }
        }
        Assert.Equal(expectedA, roster["A"].TotalSpeechDuration);
        Assert.Equal(expectedB, roster["B"].TotalSpeechDuration);
        Assert.NotEqual(TimeSpan.Zero, roster["A"].TotalSpeechDuration);
        Assert.NotEqual(TimeSpan.Zero, roster["B"].TotalSpeechDuration);
    }

    [Fact]
    public void Merge_ThreeSpeakers_AllAssignedCorrectly()
    {
        var service = new SpeakerMergeService();
        var segments = new[]
        {
            SegmentFactory.Create(0.0, 9.0, "x1 x2 y1 z1 z2",
                Tok("x1", 0,   50),
                Tok("x2", 100, 200),
                Tok("y1", 400, 500),
                Tok("z1", 700, 750),
                Tok("z2", 800, 850))
        };
        var diarization = new DiarizationResult(
            Version: 1,
            Speakers: new[]
            {
                new DiarizationSpeaker("spk0", 3.0),
                new DiarizationSpeaker("spk1", 3.0),
                new DiarizationSpeaker("spk2", 3.0)
            },
            Segments: new[]
            {
                new DiarizationSegment("spk0", 0.0, 3.0),
                new DiarizationSegment("spk1", 3.0, 6.0),
                new DiarizationSegment("spk2", 6.0, 9.0)
            });

        var result = service.Merge(segments, diarization, DefaultMetadata);

        Assert.Equal(5, result.Words.Count);
        Assert.Equal(new[] { "A", "A", "B", "C", "C" },
            result.Words.Select(w => w.SpeakerId).ToArray());
        Assert.Equal(3, result.Speakers.Count);
        Assert.Equal("A", result.Speakers[0].Id);
        Assert.Equal("B", result.Speakers[1].Id);
        Assert.Equal("C", result.Speakers[2].Id);
    }

    [Fact]
    public void Merge_DiarizationFromSingleSpeakerFixture_MatchesExpected()
    {
        var service = new SpeakerMergeService();
        var segments = LoadSegmentsFixture("single-speaker-tokens.json");
        var diarization = LoadDiarizationFixture("single-speaker.json");

        var result = service.Merge(segments, diarization, DefaultMetadata);

        Assert.Equal(3, result.Words.Count);
        Assert.All(result.Words, w => Assert.Equal("A", w.SpeakerId));
        Assert.Single(result.Speakers);
        Assert.Equal("A", result.Speakers[0].Id);
    }

    [Fact]
    public void Merge_DiarizationFromTwoSpeakerFixture_MatchesExpected()
    {
        var service = new SpeakerMergeService();
        var segments = LoadSegmentsFixture("two-speaker-tokens.json");
        var diarization = LoadDiarizationFixture("two-speaker.json");

        var result = service.Merge(segments, diarization, DefaultMetadata);

        Assert.Equal(6, result.Words.Count);
        // Four tokens end before 2.0s → A, last two start after 2.0s → B.
        Assert.Equal(new[] { "A", "A", "A", "A", "B", "B" },
            result.Words.Select(w => w.SpeakerId).ToArray());
        Assert.Equal(2, result.Speakers.Count);
    }

    [Fact]
    public void Merge_DiarizationFromThreeSpeakerFixture_MatchesExpected()
    {
        var service = new SpeakerMergeService();
        var segments = LoadSegmentsFixture("three-speaker-tokens.json");
        var diarization = LoadDiarizationFixture("three-speaker.json");

        var result = service.Merge(segments, diarization, DefaultMetadata);

        Assert.Equal(6, result.Words.Count);
        // Tokens grouped into speakers 0/1/2 by fixture timings.
        Assert.Equal(new[] { "A", "A", "B", "B", "C", "C" },
            result.Words.Select(w => w.SpeakerId).ToArray());
        Assert.Equal(3, result.Speakers.Count);
    }

    [Fact]
    public void Merge_RecordsProvidedMetadata()
    {
        var service = new SpeakerMergeService();
        var metadata = new TranscriptMetadata(
            SchemaVersion: 1,
            DiarizationModel: "pyannote/custom-model-42",
            SidecarVersion: 7);

        var result = service.Merge(
            LoadSegmentsFixture("single-speaker-tokens.json"),
            LoadDiarizationFixture("single-speaker.json"),
            metadata);

        Assert.Equal("pyannote/custom-model-42", result.Metadata.DiarizationModel);
        Assert.Equal(7, result.Metadata.SidecarVersion);
        Assert.Equal(1, result.Metadata.SchemaVersion);
    }

    [Fact]
    public void Merge_OrdinalLabelsAreStableAcrossCalls()
    {
        var service = new SpeakerMergeService();
        var segments = LoadSegmentsFixture("two-speaker-tokens.json");
        var diarization = LoadDiarizationFixture("two-speaker.json");

        var first = service.Merge(segments, diarization, DefaultMetadata);
        var second = service.Merge(segments, diarization, DefaultMetadata);

        Assert.Equal(first.Words.Count, second.Words.Count);
        for (var i = 0; i < first.Words.Count; i++)
        {
            Assert.Equal(first.Words[i].SpeakerId, second.Words[i].SpeakerId);
            Assert.Equal(first.Words[i].Start, second.Words[i].Start);
            Assert.Equal(first.Words[i].End, second.Words[i].End);
            Assert.Equal(first.Words[i].Text, second.Words[i].Text);
        }
        Assert.Equal(
            first.Speakers.Select(s => s.Id).ToArray(),
            second.Speakers.Select(s => s.Id).ToArray());
    }

    [Fact]
    public void Merge_DetectedSpeakerCountReflectsRosterSize()
    {
        var service = new SpeakerMergeService();

        var single = service.Merge(
            LoadSegmentsFixture("single-speaker-tokens.json"),
            LoadDiarizationFixture("single-speaker.json"),
            DefaultMetadata);
        var two = service.Merge(
            LoadSegmentsFixture("two-speaker-tokens.json"),
            LoadDiarizationFixture("two-speaker.json"),
            DefaultMetadata);
        var three = service.Merge(
            LoadSegmentsFixture("three-speaker-tokens.json"),
            LoadDiarizationFixture("three-speaker.json"),
            DefaultMetadata);

        Assert.Single(single.Speakers);
        Assert.Equal(2, two.Speakers.Count);
        Assert.Equal(3, three.Speakers.Count);
    }

    [Fact]
    public void Merge_DropsWhisperSpecialTokens_BeginOfSegmentAndTimestampTokens()
    {
        // whisper.cpp emits special tokens like [_BEG_] and [_TT_832] as raw
        // strings in WhisperToken.Text alongside real word tokens. They must
        // not appear in the per-word transcript; otherwise the speaker-labeled
        // markdown renders "**Speaker A:** [_BEG_] Today's guest ... [_TT_832]".
        var service = new SpeakerMergeService();
        var segments = new[]
        {
            SegmentFactory.Create(0.0, 3.0, "Today's guest",
                Tok("[_BEG_]",   0,   0),
                Tok(" Today's",  0,   40),
                Tok(" guest",    50,  90),
                Tok("[_TT_832]", 90,  90))
        };
        var diarization = new DiarizationResult(
            Version: 1,
            Speakers: new[] { new DiarizationSpeaker("A", 3.0) },
            Segments: new[] { new DiarizationSegment("A", 0.0, 3.0) });

        var result = service.Merge(segments, diarization, DefaultMetadata);

        Assert.Equal(2, result.Words.Count);
        Assert.Equal(" Today's", result.Words[0].Text);
        Assert.Equal(" guest",   result.Words[1].Text);
        Assert.DoesNotContain(result.Words, w => w.Text.Contains("[_BEG_]", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Words, w => w.Text.Contains("[_TT_", StringComparison.Ordinal));
    }

    [Fact]
    public void Merge_SegmentWithOnlySpecialTokens_ContributesNoWords()
    {
        var service = new SpeakerMergeService();
        var segments = new[]
        {
            SegmentFactory.Create(0.0, 1.0, string.Empty,
                Tok("[_BEG_]",  0, 0),
                Tok("[_EOT_]",  0, 0)),
            SegmentFactory.Create(1.0, 2.0, "hello",
                Tok("hello", 0, 40))
        };
        var diarization = new DiarizationResult(
            Version: 1,
            Speakers: new[] { new DiarizationSpeaker("A", 2.0) },
            Segments: new[] { new DiarizationSegment("A", 0.0, 2.0) });

        var result = service.Merge(segments, diarization, DefaultMetadata);

        Assert.Single(result.Words);
        Assert.Equal("hello", result.Words[0].Text);
    }

    [Fact]
    public void Merge_FlattenWordsAcrossSegments_PreservesStartTimeOrdering()
    {
        var service = new SpeakerMergeService();
        // Three segments in chronological order, with a middle segment containing
        // no tokens (guards the contract with P0.1: Array.Empty<WhisperToken>()
        // is a valid default and must not crash the merge).
        var segments = new[]
        {
            SegmentFactory.Create(0.0, 1.5, "hello world",
                Tok("hello", 0,   40),
                Tok("world", 50,  90)),
            SegmentFactory.Create(1.5, 2.5, string.Empty),
            SegmentFactory.Create(2.5, 5.0, "how are you",
                Tok("how", 0,   20),
                Tok("are", 30,  50),
                Tok("you", 60,  120))
        };
        var diarization = new DiarizationResult(
            Version: 1,
            Speakers: new[] { new DiarizationSpeaker("spk0", 5.0) },
            Segments: new[] { new DiarizationSegment("spk0", 0.0, 5.0) });

        var result = service.Merge(segments, diarization, DefaultMetadata);

        // 2 + 0 + 3 = 5 words total
        Assert.Equal(5, result.Words.Count);
        Assert.Equal("hello", result.Words[0].Text);
        Assert.Equal("world", result.Words[1].Text);
        Assert.Equal("how",   result.Words[2].Text);
        Assert.Equal("are",   result.Words[3].Text);
        Assert.Equal("you",   result.Words[4].Text);
        // Chronologically sorted start times.
        for (var i = 1; i < result.Words.Count; i++)
        {
            Assert.True(result.Words[i].Start >= result.Words[i - 1].Start,
                $"word {i} start {result.Words[i].Start} < prev {result.Words[i - 1].Start}");
        }
        // Absolute times respect segment offset.
        Assert.Equal(TimeSpan.FromSeconds(2.5), result.Words[2].Start);
        Assert.Equal(TimeSpan.FromSeconds(2.5) + TimeSpan.FromMilliseconds(200), result.Words[2].End);
    }

    private static IReadOnlyList<FilteredSegment> LoadSegmentsFixture(string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "sidecar", "words", filename);
        var json = File.ReadAllText(path);
        var entries = JsonSerializer.Deserialize<List<TokenFixture>>(json, FixtureJsonOptions)!;
        if (entries.Count == 0)
        {
            return Array.Empty<FilteredSegment>();
        }

        var tokens = new List<WhisperToken>(entries.Count);
        foreach (var e in entries)
        {
            tokens.Add(new WhisperToken
            {
                Text = e.Text,
                Start = (long)Math.Round(e.StartSec * 100),
                End = (long)Math.Round(e.EndSec * 100),
                Probability = 1.0f
            });
        }
        var segmentEnd = entries[^1].EndSec;
        var text = string.Join(" ", entries.Select(e => e.Text));
        return new[]
        {
            new FilteredSegment(TimeSpan.Zero, TimeSpan.FromSeconds(segmentEnd), text, 1.0, tokens)
        };
    }

    private static DiarizationResult LoadDiarizationFixture(string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "sidecar", "responses", filename);
        var json = File.ReadAllText(path);
        var envelope = JsonSerializer.Deserialize<DiarizationFixtureEnvelope>(json, FixtureJsonOptions)!;
        return new DiarizationResult(
            Version: envelope.Version,
            Speakers: envelope.Speakers.Select(s => new DiarizationSpeaker(s.Id, s.TotalDuration)).ToArray(),
            Segments: envelope.Segments.Select(s => new DiarizationSegment(s.Speaker, s.Start, s.End)).ToArray());
    }

    private static readonly JsonSerializerOptions FixtureJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private sealed record TokenFixture(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("startSec")] double StartSec,
        [property: JsonPropertyName("endSec")] double EndSec);

    private sealed record DiarizationFixtureEnvelope(
        [property: JsonPropertyName("version")] int Version,
        [property: JsonPropertyName("speakers")] List<DiarizationFixtureSpeaker> Speakers,
        [property: JsonPropertyName("segments")] List<DiarizationFixtureSegment> Segments);

    private sealed record DiarizationFixtureSpeaker(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("totalDuration")] double TotalDuration);

    private sealed record DiarizationFixtureSegment(
        [property: JsonPropertyName("speaker")] string Speaker,
        [property: JsonPropertyName("start")] double Start,
        [property: JsonPropertyName("end")] double End);

    private static WhisperToken Tok(string text, long start, long end) => new()
    {
        Text = text,
        Start = start,
        End = end,
        Probability = 1.0f
    };

    private static class SegmentFactory
    {
        public static FilteredSegment Create(double startSec, double endSec, string text, params WhisperToken[] tokens)
            => new(TimeSpan.FromSeconds(startSec), TimeSpan.FromSeconds(endSec), text, Probability: 1.0, tokens);
    }
}
