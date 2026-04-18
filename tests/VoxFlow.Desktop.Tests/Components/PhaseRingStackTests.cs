using Microsoft.AspNetCore.Components;
using VoxFlow.Core.Models;
using VoxFlow.Desktop.Components.Pages;
using VoxFlow.Desktop.ViewModels;
using Xunit;

namespace VoxFlow.Desktop.Tests.Components;

public sealed class PhaseRingStackTests
{
    private static readonly DateTimeOffset T0 = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static ParameterView Params(PhaseProgressTracker tracker)
        => ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(PhaseRingStack.Tracker)] = tracker,
        });

    private static ProgressUpdate Frame(ProgressStage stage, double pct, string? msg = null)
        => new(stage, pct, TimeSpan.Zero, Message: msg);

    [Fact]
    public async Task RendersThreePhaseRings_InPhaseOrder()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));

        var cells = rendered
            .FindElements(e => e.HasClass("phase-cell"))
            .ToArray();
        Assert.Equal(3, cells.Length);
        Assert.Equal("transcription", cells[0].Attributes["data-phase"]?.ToString());
        Assert.Equal("diarization", cells[1].Attributes["data-phase"]?.ToString());
        Assert.Equal("merge", cells[2].Attributes["data-phase"]?.ToString());
    }

    [Fact]
    public async Task EachPhaseCell_PassesCorrectPhaseToken_ToItsRing()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));

        var rings = rendered
            .FindElements(e => e.HasClass("phase-ring") && !e.HasClass("phase-ring-stack"))
            .ToArray();
        Assert.Equal(3, rings.Length);
        Assert.Contains("--phase-transcription", rings[0].Attributes["style"]?.ToString() ?? "");
        Assert.Contains("--phase-diarization", rings[1].Attributes["style"]?.ToString() ?? "");
        Assert.Contains("--phase-merge", rings[2].Attributes["style"]?.ToString() ?? "");
    }

    [Fact]
    public async Task ChevronDividers_SeparateRings()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));

        var dividers = rendered.FindElements(e => e.HasClass("phase-divider")).ToArray();
        Assert.Equal(2, dividers.Length);
    }

    [Fact]
    public async Task TranscriptionRunning_TranscriptionCellHasRunningStatus()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));
        tracker.OnProgress(Frame(ProgressStage.Transcribing, 45.0));
        await rendered.SynchronizeAsync();

        var transcriptionCell = rendered.FindElement(
            e => e.HasClass("phase-cell")
                 && e.Attributes.TryGetValue("data-phase", out var p)
                 && (p?.ToString() ?? "") == "transcription",
            "transcription phase-cell");
        Assert.Equal("running", transcriptionCell.Attributes["data-status"]?.ToString());

        // Transcription local at ProgressStage.Transcribing 45% overall = 50%
        Assert.Contains("50", transcriptionCell.TextContent);
    }

    [Fact]
    public async Task SkippedDiarization_DiarizationCellHasSkippedStatus()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: false, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));

        var diarizationCell = rendered.FindElement(
            e => e.HasClass("phase-cell")
                 && e.Attributes.TryGetValue("data-phase", out var p)
                 && (p?.ToString() ?? "") == "diarization",
            "diarization phase-cell");
        Assert.Equal("skipped", diarizationCell.Attributes["data-status"]?.ToString());
        Assert.Contains("skipped", diarizationCell.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OuterStack_HasProgressbarRole_ReflectingFocusPhase()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));
        tracker.OnProgress(Frame(ProgressStage.Transcribing, 45.0));
        await rendered.SynchronizeAsync();

        var stack = rendered.FindElement(
            e => e.Name == "div" && e.HasClass("phase-ring-stack"),
            "phase-ring-stack");
        Assert.Equal("progressbar", stack.Attributes["role"]?.ToString());
        Assert.Equal("Transcription progress", stack.Attributes["aria-label"]?.ToString());
        Assert.Equal("50", stack.Attributes["aria-valuenow"]?.ToString());
    }
}
