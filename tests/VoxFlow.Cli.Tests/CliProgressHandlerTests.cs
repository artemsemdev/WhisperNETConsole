using System;
using System.IO;
using VoxFlow.Cli;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;
using Xunit;

namespace VoxFlow.Cli.Tests;

public sealed class CliProgressHandlerTests
{
    [Fact]
    public void Report_DiarizingStage_RendersDiarizingLabel()
    {
        var options = new ConsoleProgressOptions(
            Enabled: true,
            UseColors: false,
            ProgressBarWidth: 10,
            RefreshIntervalMilliseconds: 0);
        var handler = new CliProgressHandler(options);

        using var stdout = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(stdout);
        try
        {
            handler.Report(new ProgressUpdate(
                Stage: ProgressStage.Diarizing,
                PercentComplete: 42.0,
                Elapsed: TimeSpan.FromSeconds(3),
                Message: "segmentation"));
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = stdout.ToString();
        Assert.Contains("Diarizing", output);
        Assert.DoesNotContain("Working", output);
    }

    [Fact]
    public void Report_DiarizingStage_IncludesSidecarStageMessage()
    {
        var options = new ConsoleProgressOptions(
            Enabled: true,
            UseColors: false,
            ProgressBarWidth: 10,
            RefreshIntervalMilliseconds: 0);
        var handler = new CliProgressHandler(options);

        using var stdout = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(stdout);
        try
        {
            handler.Report(new ProgressUpdate(
                Stage: ProgressStage.Diarizing,
                PercentComplete: 60.0,
                Elapsed: TimeSpan.FromSeconds(5),
                Message: "embeddings"));
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Contains("embeddings", stdout.ToString());
    }
}
