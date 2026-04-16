using System.Text.Json;
using NJsonSchema;
using VoxFlow.Core.Models;
using VoxFlow.Core.Services;
using Xunit;

namespace VoxFlow.Core.Tests.Services;

public sealed class VoxflowTranscriptArtifactWriterTests
{
    [Fact]
    public async Task WriteAsync_WithDocument_WritesFileAtExpectedPath()
    {
        using var directory = new TemporaryDirectory();
        var writer = new VoxflowTranscriptArtifactWriter();
        var resultPath = Path.Combine(directory.Path, "out.txt");

        await writer.WriteAsync(resultPath, BuildDocument(), CancellationToken.None);

        Assert.True(File.Exists(resultPath + ".voxflow.json"));
    }

    [Fact]
    public async Task WriteAsync_RoundTrip_ProducesEqualDocument()
    {
        using var directory = new TemporaryDirectory();
        var writer = new VoxflowTranscriptArtifactWriter();
        var resultPath = Path.Combine(directory.Path, "out.srt");
        var original = BuildDocument();

        await writer.WriteAsync(resultPath, original, CancellationToken.None);

        var json = await File.ReadAllTextAsync(resultPath + ".voxflow.json");
        var roundTripped = JsonSerializer.Deserialize<TranscriptDocument>(
            json, TranscriptDocument.JsonSerializerOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.Speakers.Count, roundTripped!.Speakers.Count);
        Assert.Equal(original.Words.Count, roundTripped.Words.Count);
        Assert.Equal(original.Turns.Count, roundTripped.Turns.Count);
        Assert.Equal(original.Metadata.DiarizationModel, roundTripped.Metadata.DiarizationModel);
        for (var i = 0; i < original.Words.Count; i++)
        {
            Assert.Equal(original.Words[i].Text, roundTripped.Words[i].Text);
            Assert.Equal(original.Words[i].SpeakerId, roundTripped.Words[i].SpeakerId);
            Assert.Equal(original.Words[i].Start, roundTripped.Words[i].Start);
            Assert.Equal(original.Words[i].End, roundTripped.Words[i].End);
        }
    }

    [Fact]
    public async Task WriteAsync_ValidatesAgainstVoxflowTranscriptSchema()
    {
        using var directory = new TemporaryDirectory();
        var writer = new VoxflowTranscriptArtifactWriter();
        var resultPath = Path.Combine(directory.Path, "out.txt");

        await writer.WriteAsync(resultPath, BuildDocument(), CancellationToken.None);

        var json = await File.ReadAllTextAsync(resultPath + ".voxflow.json");
        var schema = await JsonSchema.FromFileAsync(LocateSchemaFile());
        var errors = schema.Validate(json);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task WriteAsync_Cancelled_DoesNotLeavePartialFile()
    {
        using var directory = new TemporaryDirectory();
        var writer = new VoxflowTranscriptArtifactWriter();
        var resultPath = Path.Combine(directory.Path, "out.txt");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            writer.WriteAsync(resultPath, BuildDocument(), cts.Token));

        Assert.False(File.Exists(resultPath + ".voxflow.json"));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.voxflow.json*"));
    }

    private static TranscriptDocument BuildDocument()
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
            new SpeakerInfo("A", "Speaker A", TimeSpan.FromSeconds(2)),
            new SpeakerInfo("B", "Speaker B", TimeSpan.FromSeconds(2))
        };
        var metadata = new TranscriptMetadata(
            SchemaVersion: 1,
            DiarizationModel: "pyannote/speaker-diarization-3.1",
            SidecarVersion: 1);
        return new TranscriptDocument(speakers, words, turns, metadata);
    }

    private static string LocateSchemaFile()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "docs", "contracts", "voxflow-transcript-v1.schema.json");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException("voxflow-transcript-v1.schema.json not found");
    }
}
