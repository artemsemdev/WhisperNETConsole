using VoxFlow.Core.Models;
using Xunit;

namespace VoxFlow.Core.Tests.Models;

public sealed class TranscribeFileRequestTests
{
    [Fact]
    public void Construct_WithExistingPositionalArgs_StillCompiles_AndEnableSpeakersDefaultsToNull()
    {
        var request = new TranscribeFileRequest(
            "/tmp/input.m4a",
            "/tmp/result.txt",
            "/tmp/appsettings.json",
            new[] { "en", "uk" },
            true);

        Assert.Equal("/tmp/input.m4a", request.InputPath);
        Assert.Equal("/tmp/result.txt", request.ResultFilePath);
        Assert.Equal("/tmp/appsettings.json", request.ConfigurationPath);
        Assert.NotNull(request.ForceLanguages);
        Assert.Equal(2, request.ForceLanguages!.Count);
        Assert.True(request.OverwriteExistingResult);
        Assert.Null(request.EnableSpeakers);
    }

    [Fact]
    public void Construct_WithOnlyInputPath_EnableSpeakersDefaultsToNull()
    {
        var request = new TranscribeFileRequest("/tmp/input.m4a");

        Assert.Null(request.EnableSpeakers);
    }

    [Fact]
    public void Construct_WithEnableSpeakersTrue_PreservesValue()
    {
        var request = new TranscribeFileRequest(
            InputPath: "/tmp/input.m4a",
            EnableSpeakers: true);

        Assert.True(request.EnableSpeakers);
    }

    [Fact]
    public void Construct_WithEnableSpeakersFalse_PreservesValue()
    {
        var request = new TranscribeFileRequest(
            InputPath: "/tmp/input.m4a",
            EnableSpeakers: false);

        Assert.False(request.EnableSpeakers);
    }
}
