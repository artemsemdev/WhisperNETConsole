using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NJsonSchema;
using VoxFlow.Core.Models;
using Xunit;

namespace VoxFlow.Core.Tests.Models;

public sealed class TranscriptDocumentTests
{
    [Fact]
    public void Construct_WithSpeakersAndWords_ExposesAllFields()
    {
        var speakers = new[]
        {
            new SpeakerInfo("A", "Speaker A", TimeSpan.FromSeconds(3)),
            new SpeakerInfo("B", "Speaker B", TimeSpan.FromSeconds(2))
        };

        var words = new[]
        {
            new TranscriptWord(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1), "Hello", "A"),
            new TranscriptWord(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "world", "A"),
            new TranscriptWord(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), "good", "A"),
            new TranscriptWord(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4), "morning", "B")
        };

        var turns = new[]
        {
            new SpeakerTurn(
                "A",
                TimeSpan.FromSeconds(0),
                TimeSpan.FromSeconds(3),
                new[] { words[0], words[1], words[2] }),
            new SpeakerTurn(
                "B",
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(4),
                new[] { words[3] })
        };

        var metadata = new TranscriptMetadata(
            SchemaVersion: 1,
            DiarizationModel: "pyannote/speaker-diarization-community-1",
            SidecarVersion: 1);

        var document = new TranscriptDocument(speakers, words, turns, metadata);

        Assert.NotNull(document.Speakers);
        Assert.Equal(2, document.Speakers.Count);
        Assert.Equal("A", document.Speakers[0].Id);
        Assert.Equal("Speaker A", document.Speakers[0].DisplayName);
        Assert.Equal(TimeSpan.FromSeconds(3), document.Speakers[0].TotalSpeechDuration);

        Assert.NotNull(document.Words);
        Assert.Equal(4, document.Words.Count);
        Assert.Equal("Hello", document.Words[0].Text);
        Assert.Equal(TimeSpan.FromSeconds(0), document.Words[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(1), document.Words[0].End);
        Assert.Equal("A", document.Words[0].SpeakerId);
        Assert.Equal("B", document.Words[3].SpeakerId);

        Assert.NotNull(document.Turns);
        Assert.Equal(2, document.Turns.Count);
        Assert.Equal("A", document.Turns[0].SpeakerId);
        Assert.Equal(TimeSpan.FromSeconds(0), document.Turns[0].StartTime);
        Assert.Equal(TimeSpan.FromSeconds(3), document.Turns[0].EndTime);
        Assert.Equal(3, document.Turns[0].Words.Count);

        Assert.NotNull(document.Metadata);
        Assert.Equal(1, document.Metadata.SchemaVersion);
        Assert.Equal("pyannote/speaker-diarization-community-1", document.Metadata.DiarizationModel);
        Assert.Equal(1, document.Metadata.SidecarVersion);
    }

    [Fact]
    public void Serialize_RoundTrip_ProducesEqualDocument()
    {
        var original = BuildSampleDocument();
        var options = TranscriptDocument.JsonSerializerOptions;

        var json = JsonSerializer.Serialize(original, options);
        var roundTripped = JsonSerializer.Deserialize<TranscriptDocument>(json, options);

        Assert.NotNull(roundTripped);

        // Structural equality: re-serializing the round-tripped document must
        // produce byte-identical JSON. This catches naming/attribute issues
        // without relying on record equality (which compares collection
        // references, not contents).
        var reserialized = JsonSerializer.Serialize(roundTripped, options);
        Assert.Equal(json, reserialized);

        Assert.Equal(original.Speakers.Count, roundTripped!.Speakers.Count);
        Assert.Equal(original.Words.Count, roundTripped.Words.Count);
        Assert.Equal(original.Turns.Count, roundTripped.Turns.Count);
        Assert.Equal(original.Words[0].Text, roundTripped.Words[0].Text);
        Assert.Equal(original.Words[0].Start, roundTripped.Words[0].Start);
        Assert.Equal(original.Words[0].End, roundTripped.Words[0].End);
        Assert.Equal(original.Words[0].SpeakerId, roundTripped.Words[0].SpeakerId);
        Assert.Equal(original.Metadata.DiarizationModel, roundTripped.Metadata.DiarizationModel);
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidatesAgainstVoxflowTranscriptSchema()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "contracts", "voxflow-transcript-v1.schema.json");
        Assert.True(File.Exists(schemaPath), $"Schema not found at {schemaPath}");

        var schema = await JsonSchema.FromFileAsync(schemaPath);
        var document = BuildSampleDocument();
        var json = JsonSerializer.Serialize(document, TranscriptDocument.JsonSerializerOptions);

        var errors = schema.Validate(json);

        Assert.Empty(errors);
    }

    private static TranscriptDocument BuildSampleDocument()
    {
        var speakers = new[]
        {
            new SpeakerInfo("A", "Speaker A", TimeSpan.FromSeconds(3)),
            new SpeakerInfo("B", "Speaker B", TimeSpan.FromSeconds(1))
        };

        var words = new[]
        {
            new TranscriptWord(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1), "Hello", "A"),
            new TranscriptWord(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "world", "A"),
            new TranscriptWord(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), "good", "A"),
            new TranscriptWord(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4), "morning", "B")
        };

        var turns = new[]
        {
            new SpeakerTurn("A", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(3), new[] { words[0], words[1], words[2] }),
            new SpeakerTurn("B", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4), new[] { words[3] })
        };

        var metadata = new TranscriptMetadata(
            SchemaVersion: 1,
            DiarizationModel: "pyannote/speaker-diarization-community-1",
            SidecarVersion: 1);

        return new TranscriptDocument(speakers, words, turns, metadata);
    }
}
