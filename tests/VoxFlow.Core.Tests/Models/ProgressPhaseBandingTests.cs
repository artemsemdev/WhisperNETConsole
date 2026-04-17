using VoxFlow.Core.Models;
using Xunit;

namespace VoxFlow.Core.Tests.Models;

public sealed class ProgressPhaseBandingTests
{
    [Theory]
    [InlineData(ProgressStage.Validating, ProgressPhase.Transcription)]
    [InlineData(ProgressStage.Converting, ProgressPhase.Transcription)]
    [InlineData(ProgressStage.LoadingModel, ProgressPhase.Transcription)]
    [InlineData(ProgressStage.Transcribing, ProgressPhase.Transcription)]
    [InlineData(ProgressStage.Filtering, ProgressPhase.Transcription)]
    [InlineData(ProgressStage.Diarizing, ProgressPhase.Diarization)]
    [InlineData(ProgressStage.Writing, ProgressPhase.Merge)]
    [InlineData(ProgressStage.Complete, ProgressPhase.Merge)]
    [InlineData(ProgressStage.Failed, ProgressPhase.Transcription)]
    public void PhaseOf_MapsStageToCorrectPhase(ProgressStage stage, ProgressPhase expected)
    {
        Assert.Equal(expected, ProgressPhaseBanding.PhaseOf(stage));
    }

    [Theory]
    // Transcription 0..90 maps 0..100 in-band
    [InlineData(ProgressStage.Transcribing, 0.0, 0.0)]
    [InlineData(ProgressStage.Transcribing, 45.0, 50.0)]
    [InlineData(ProgressStage.Transcribing, 90.0, 100.0)]
    // Diarization 90..95
    [InlineData(ProgressStage.Diarizing, 90.0, 0.0)]
    [InlineData(ProgressStage.Diarizing, 92.5, 50.0)]
    [InlineData(ProgressStage.Diarizing, 95.0, 100.0)]
    // Merge 95..100
    [InlineData(ProgressStage.Writing, 95.0, 0.0)]
    [InlineData(ProgressStage.Writing, 97.5, 50.0)]
    [InlineData(ProgressStage.Writing, 100.0, 100.0)]
    public void LocalPercent_RemapsOverallPercentToBand(
        ProgressStage stage,
        double overall,
        double expectedLocal)
    {
        var actual = ProgressPhaseBanding.LocalPercent(stage, overall);
        Assert.Equal(expectedLocal, actual, 3);
    }

    [Fact]
    public void LocalPercent_Failed_ReturnsRawOverallClamped()
    {
        Assert.Equal(42.0, ProgressPhaseBanding.LocalPercent(ProgressStage.Failed, 42.0), 3);
        Assert.Equal(0.0, ProgressPhaseBanding.LocalPercent(ProgressStage.Failed, -10.0), 3);
        Assert.Equal(100.0, ProgressPhaseBanding.LocalPercent(ProgressStage.Failed, 150.0), 3);
    }

    [Fact]
    public void LocalPercent_OutOfBand_IsClampedTo0And100()
    {
        Assert.Equal(0.0, ProgressPhaseBanding.LocalPercent(ProgressStage.Transcribing, -5.0), 3);
        Assert.Equal(100.0, ProgressPhaseBanding.LocalPercent(ProgressStage.Transcribing, 150.0), 3);
    }

    [Theory]
    [InlineData(ProgressStage.Transcribing, 90.0)]
    [InlineData(ProgressStage.Diarizing, 95.0)]
    [InlineData(ProgressStage.Writing, 100.0)]
    [InlineData(ProgressStage.Complete, 100.0)]
    public void PhaseUpperBound_ReturnsBandCeiling(ProgressStage stage, double expected)
    {
        Assert.Equal(expected, ProgressPhaseBanding.PhaseUpperBound(stage));
    }
}
