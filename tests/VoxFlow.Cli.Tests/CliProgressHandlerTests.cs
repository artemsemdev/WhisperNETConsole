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
    public void Report_PhaseTransition_FinalizesPreviousPhaseAtLocal100()
    {
        // Whisper's progress callback stops emitting well before 100% of the
        // file (its last callback typically lands in the 75-90% range with no
        // closing update), so the Transcription bar would freeze mid-way
        // even though the stage is logically done. When a phase transition
        // is observed, the CLI must render one final frame of the previous
        // phase at local 100% before committing its line; otherwise users
        // see a stacked history of "86.5%" / "0.0%" / "100%" that looks
        // wrong even though the work completed successfully.
        using var handler = new CliProgressHandler(DefaultOptions());
        var output = Capture(() =>
        {
            handler.Report(new ProgressUpdate(
                Stage: ProgressStage.Transcribing,
                PercentComplete: 70.0, // local 77.8% -- never reaches 100
                Elapsed: TimeSpan.FromSeconds(5)));
            handler.Report(new ProgressUpdate(
                Stage: ProgressStage.Diarizing,
                PercentComplete: 90.0,
                Elapsed: TimeSpan.FromSeconds(6),
                Message: "starting"));
        });

        var transcriptionIdx = output.IndexOf("Transcription", StringComparison.Ordinal);
        var diarizationIdx = output.IndexOf("Diarization", StringComparison.Ordinal);
        Assert.True(transcriptionIdx >= 0 && diarizationIdx > transcriptionIdx,
            "expected Transcription label to precede Diarization label");
        var between = output[transcriptionIdx..diarizationIdx];
        Assert.Contains(" 100.0%", between);
    }

    [Fact]
    public void Report_DiarizationToMergeTransition_FinalizesDiarizationAtLocal100()
    {
        // pyannote's ProgressHook emits step-boundary events without
        // total/completed for most sub-steps (segmentation, embeddings,
        // discrete_diarization), so Fraction stays null and the mapped local
        // percent stays at 0 for the entire 30-60s diarization run. The
        // phase-finalize rule above ensures the committed Diarization line
        // shows 100% as soon as we transition to Merge, instead of the
        // misleading "Diarization 0.0%" the user currently sees.
        using var handler = new CliProgressHandler(DefaultOptions());
        var output = Capture(() =>
        {
            handler.Report(new ProgressUpdate(
                Stage: ProgressStage.Diarizing,
                PercentComplete: 90.0, // local 0%
                Elapsed: TimeSpan.FromSeconds(5),
                Message: "discrete_diarization"));
            handler.Report(new ProgressUpdate(
                Stage: ProgressStage.Writing,
                PercentComplete: 95.0,
                Elapsed: TimeSpan.FromSeconds(6)));
        });

        var diarizationIdx = output.IndexOf("Diarization", StringComparison.Ordinal);
        var mergeIdx = output.IndexOf("Merge", StringComparison.Ordinal);
        Assert.True(diarizationIdx >= 0 && mergeIdx > diarizationIdx,
            "expected Diarization label to precede Merge label");
        var between = output[diarizationIdx..mergeIdx];
        Assert.Contains(" 100.0%", between);
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
