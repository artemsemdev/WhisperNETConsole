using System.Text.Json.Nodes;
using VoxFlow.Core.Models;
using VoxFlow.Desktop.Services;
using Xunit;

namespace VoxFlow.Desktop.Tests;

public sealed class DesktopCliTranscriptionServiceTests
{
    [Fact]
    public void BuildTranscriptionMutator_WritesInputPath()
    {
        var request = new TranscribeFileRequest(InputPath: "/tmp/audio.m4a");
        var transcription = new JsonObject();

        DesktopCliTranscriptionService
            .BuildTranscriptionMutator(request)
            .Invoke(transcription);

        Assert.Equal("/tmp/audio.m4a", transcription["inputFilePath"]?.GetValue<string>());
    }

    [Fact]
    public void BuildTranscriptionMutator_WritesResultFilePath_WhenProvided()
    {
        var request = new TranscribeFileRequest(
            InputPath: "/tmp/audio.m4a",
            ResultFilePath: "/tmp/result.txt");
        var transcription = new JsonObject();

        DesktopCliTranscriptionService
            .BuildTranscriptionMutator(request)
            .Invoke(transcription);

        Assert.Equal("/tmp/result.txt", transcription["resultFilePath"]?.GetValue<string>());
    }

    [Fact]
    public void BuildTranscriptionMutator_EnableSpeakersTrue_SetsSpeakerLabelingEnabledTrue()
    {
        var request = new TranscribeFileRequest(
            InputPath: "/tmp/audio.m4a",
            EnableSpeakers: true);
        var transcription = new JsonObject();

        DesktopCliTranscriptionService
            .BuildTranscriptionMutator(request)
            .Invoke(transcription);

        var speakerLabeling = transcription["speakerLabeling"]?.AsObject();
        Assert.NotNull(speakerLabeling);
        Assert.True(speakerLabeling["enabled"]?.GetValue<bool>());
    }

    [Fact]
    public void BuildTranscriptionMutator_EnableSpeakersFalse_SetsSpeakerLabelingEnabledFalse()
    {
        var request = new TranscribeFileRequest(
            InputPath: "/tmp/audio.m4a",
            EnableSpeakers: false);
        var transcription = new JsonObject
        {
            ["speakerLabeling"] = new JsonObject { ["enabled"] = true }
        };

        DesktopCliTranscriptionService
            .BuildTranscriptionMutator(request)
            .Invoke(transcription);

        Assert.False(transcription["speakerLabeling"]?["enabled"]?.GetValue<bool>());
    }

    [Fact]
    public void BuildTranscriptionMutator_EnableSpeakersNull_LeavesExistingSpeakerLabelingUntouched()
    {
        var request = new TranscribeFileRequest(
            InputPath: "/tmp/audio.m4a",
            EnableSpeakers: null);
        var transcription = new JsonObject
        {
            ["speakerLabeling"] = new JsonObject
            {
                ["enabled"] = true,
                ["timeoutSeconds"] = 900
            }
        };

        DesktopCliTranscriptionService
            .BuildTranscriptionMutator(request)
            .Invoke(transcription);

        Assert.True(transcription["speakerLabeling"]?["enabled"]?.GetValue<bool>());
        Assert.Equal(900, transcription["speakerLabeling"]?["timeoutSeconds"]?.GetValue<int>());
    }

    [Fact]
    public void BuildTranscriptionMutator_EnableSpeakersTrue_PreservesSiblingSpeakerLabelingKeys()
    {
        var request = new TranscribeFileRequest(
            InputPath: "/tmp/audio.m4a",
            EnableSpeakers: true);
        var transcription = new JsonObject
        {
            ["speakerLabeling"] = new JsonObject
            {
                ["enabled"] = false,
                ["timeoutSeconds"] = 900,
                ["modelId"] = "pyannote/speaker-diarization-3.1"
            }
        };

        DesktopCliTranscriptionService
            .BuildTranscriptionMutator(request)
            .Invoke(transcription);

        var speakerLabeling = transcription["speakerLabeling"]?.AsObject();
        Assert.NotNull(speakerLabeling);
        Assert.True(speakerLabeling["enabled"]?.GetValue<bool>());
        Assert.Equal(900, speakerLabeling["timeoutSeconds"]?.GetValue<int>());
        Assert.Equal("pyannote/speaker-diarization-3.1", speakerLabeling["modelId"]?.GetValue<string>());
    }
}
