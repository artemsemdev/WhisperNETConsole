using System;
using System.IO;
using System.Threading.Tasks;
using VoxFlow.Cli;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;
using Xunit;

namespace VoxFlow.Cli.Tests;

public sealed class CliProgressHandlerTests
{
    private static ConsoleProgressOptions DefaultOptions() => new(
        Enabled: true,
        UseColors: false,
        ProgressBarWidth: 10,
        RefreshIntervalMilliseconds: 0);

    [Fact]
    public void Report_TranscribingStage_RendersTranscriptionLabel()
    {
        using var handler = new CliProgressHandler(DefaultOptions());
        var output = Capture(() => handler.Report(new ProgressUpdate(
            Stage: ProgressStage.Transcribing,
            PercentComplete: 45.0,
            Elapsed: TimeSpan.FromSeconds(1))));

        Assert.Contains("Transcription", output);
        Assert.DoesNotContain("Working", output);
    }

    [Fact]
    public void Report_DiarizingStage_RendersDiarizationLabel()
    {
        using var handler = new CliProgressHandler(DefaultOptions());
        var output = Capture(() => handler.Report(new ProgressUpdate(
            Stage: ProgressStage.Diarizing,
            PercentComplete: 92.0,
            Elapsed: TimeSpan.FromSeconds(3),
            Message: "segmentation")));

        Assert.Contains("Diarization", output);
        Assert.DoesNotContain("Working", output);
    }

    [Fact]
    public void Report_WritingStage_RendersMergeLabel()
    {
        using var handler = new CliProgressHandler(DefaultOptions());
        var output = Capture(() => handler.Report(new ProgressUpdate(
            Stage: ProgressStage.Writing,
            PercentComplete: 97.0,
            Elapsed: TimeSpan.FromSeconds(1))));

        Assert.Contains("Merge", output);
        Assert.DoesNotContain("Working", output);
    }

    [Fact]
    public void Report_TranscribingStage_MapsOverallPercentToLocal()
    {
        // Transcription phase spans overall [0, 90]; overall 45 == local 50.
        // Each phase renders its own 0-100 bar so users see per-operation
        // progress, not a single meter smeared across unrelated workloads.
        using var handler = new CliProgressHandler(DefaultOptions());
        var output = Capture(() => handler.Report(new ProgressUpdate(
            Stage: ProgressStage.Transcribing,
            PercentComplete: 45.0,
            Elapsed: TimeSpan.FromSeconds(1))));

        Assert.Contains(" 50.0%", output);
        Assert.DoesNotContain(" 45.0%", output);
    }

    [Fact]
    public void Report_DiarizingStage_MapsOverallPercentToLocal()
    {
        // Diarization phase spans overall [90, 95]; overall 92.5 == local 50.
        using var handler = new CliProgressHandler(DefaultOptions());
        var output = Capture(() => handler.Report(new ProgressUpdate(
            Stage: ProgressStage.Diarizing,
            PercentComplete: 92.5,
            Elapsed: TimeSpan.FromSeconds(1))));

        Assert.Contains(" 50.0%", output);
    }

    [Fact]
    public void Report_WritingStage_MapsOverallPercentToLocal()
    {
        // Merge phase spans overall [95, 100]; overall 97.5 == local 50.
        using var handler = new CliProgressHandler(DefaultOptions());
        var output = Capture(() => handler.Report(new ProgressUpdate(
            Stage: ProgressStage.Writing,
            PercentComplete: 97.5,
            Elapsed: TimeSpan.FromSeconds(1))));

        Assert.Contains(" 50.0%", output);
    }

    [Fact]
    public void Report_PhaseTransition_FinalizesPreviousPhaseLineWithNewline()
    {
        // Crossing a phase boundary must finish the previous phase's bar on
        // its own line so the user sees a stacked history of completed
        // phases instead of one bar being overwritten for unrelated work.
        using var handler = new CliProgressHandler(DefaultOptions());
        var output = Capture(() =>
        {
            handler.Report(new ProgressUpdate(
                Stage: ProgressStage.Transcribing,
                PercentComplete: 85.0,
                Elapsed: TimeSpan.FromSeconds(10)));
            handler.Report(new ProgressUpdate(
                Stage: ProgressStage.Diarizing,
                PercentComplete: 90.0,
                Elapsed: TimeSpan.FromSeconds(11),
                Message: "starting"));
        });

        var transcriptionIdx = output.IndexOf("Transcription", StringComparison.Ordinal);
        var diarizationIdx = output.IndexOf("Diarization", StringComparison.Ordinal);
        Assert.True(transcriptionIdx >= 0, "expected Transcription label in output");
        Assert.True(diarizationIdx > transcriptionIdx, "expected Diarization to follow Transcription");
        var between = output[transcriptionIdx..diarizationIdx];
        Assert.Contains("\n", between);
    }

    [Fact]
    public void Report_SameStageTwice_DoesNotInjectNewline()
    {
        // Updates inside the same phase must overwrite in place via \r so we
        // only stack one line per phase, not one line per sub-step.
        using var handler = new CliProgressHandler(DefaultOptions());
        var output = Capture(() =>
        {
            handler.Report(new ProgressUpdate(
                Stage: ProgressStage.Transcribing,
                PercentComplete: 45.0,
                Elapsed: TimeSpan.FromSeconds(1)));
            handler.Report(new ProgressUpdate(
                Stage: ProgressStage.Transcribing,
                PercentComplete: 60.0,
                Elapsed: TimeSpan.FromSeconds(2)));
        });

        var firstIdx = output.IndexOf("Transcription", StringComparison.Ordinal);
        var secondIdx = output.IndexOf("Transcription", firstIdx + 1, StringComparison.Ordinal);
        Assert.True(secondIdx > firstIdx, "expected the Transcription label to be rendered twice");
        var between = output[firstIdx..secondIdx];
        Assert.DoesNotContain("\n", between);
    }

    [Fact]
    public void Report_DiarizingStage_IncludesSidecarStageMessage()
    {
        using var handler = new CliProgressHandler(DefaultOptions());
        var output = Capture(() => handler.Report(new ProgressUpdate(
            Stage: ProgressStage.Diarizing,
            PercentComplete: 92.0,
            Elapsed: TimeSpan.FromSeconds(5),
            Message: "embeddings")));

        Assert.Contains("embeddings", output);
    }

    [Fact]
    public async Task Report_HeartbeatTicks_RerenderElapsedBetweenReports()
    {
        // The CLI bar stopped animating between pyannote ProgressHook events
        // because the rendered elapsed time came from the producer's
        // ProgressUpdate.Elapsed snapshot, so 10-60s silences between pyannote
        // sub-steps looked like a frozen clock. The handler owns a heartbeat
        // that re-renders the last update with a wall-clock-projected elapsed
        // every tick, even when no new Report arrives.
        using var handler = new CliProgressHandler(DefaultOptions(), heartbeatInterval: TimeSpan.FromMilliseconds(40));

        var output = await CaptureAsync(async () =>
        {
            handler.Report(new ProgressUpdate(
                Stage: ProgressStage.Diarizing,
                PercentComplete: 90.0,
                Elapsed: TimeSpan.FromSeconds(10),
                Message: "loading pyannote"));

            await Task.Delay(TimeSpan.FromMilliseconds(1250));
        });

        Assert.Contains("0:10", output);
        Assert.Contains("0:11", output);
    }

    private static string Capture(Action act)
    {
        using var stdout = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(stdout);
        try { act(); }
        finally { Console.SetOut(originalOut); }
        return stdout.ToString();
    }

    private static async Task<string> CaptureAsync(Func<Task> act)
    {
        using var stdout = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(stdout);
        try { await act().ConfigureAwait(false); }
        finally { Console.SetOut(originalOut); }
        return stdout.ToString();
    }
}
