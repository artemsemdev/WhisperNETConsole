using VoxFlow.Core.Models;
using Xunit;

namespace VoxFlow.Core.Tests.Models;

public sealed class BatchTranscribeRequestTests
{
    [Fact]
    public void Construct_WithOnlyRequiredArgs_EnableSpeakersDefaultsToNull()
    {
        var request = new BatchTranscribeRequest("/tmp/input", "/tmp/output");

        Assert.Null(request.EnableSpeakers);
    }

    [Fact]
    public void Construct_WithEnableSpeakersTrue_PreservesValue()
    {
        var request = new BatchTranscribeRequest(
            InputDirectory: "/tmp/input",
            OutputDirectory: "/tmp/output",
            EnableSpeakers: true);

        Assert.True(request.EnableSpeakers);
    }

    [Fact]
    public void Construct_WithEnableSpeakersFalse_PreservesValue()
    {
        var request = new BatchTranscribeRequest(
            InputDirectory: "/tmp/input",
            OutputDirectory: "/tmp/output",
            EnableSpeakers: false);

        Assert.False(request.EnableSpeakers);
    }
}
