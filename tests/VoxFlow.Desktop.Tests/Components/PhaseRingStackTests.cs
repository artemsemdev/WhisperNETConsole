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
    public async Task Renders_ThreeRings_InPhaseOrder()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));

        var ringRoots = rendered.FindElements(
            e => e.Name == "div"
                && e.Attributes.TryGetValue("class", out var c)
                && (c?.ToString() ?? "") == "phase-ring");
        Assert.Equal(3, ringRoots.Count);
    }

    [Fact]
    public async Task AssignsPhaseColorTokens_CyanMagentaGreen()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));

        var ringRoots = rendered
            .FindElements(
                e => e.Name == "div"
                    && e.Attributes.TryGetValue("class", out var c)
                    && (c?.ToString() ?? "") == "phase-ring")
            .ToArray();

        Assert.Equal(3, ringRoots.Length);
        Assert.Contains("--phase-transcription", ringRoots[0].Attributes["style"]?.ToString() ?? "");
        Assert.Contains("--phase-diarization", ringRoots[1].Attributes["style"]?.ToString() ?? "");
        Assert.Contains("--phase-merge", ringRoots[2].Attributes["style"]?.ToString() ?? "");
    }

    [Fact]
    public async Task RendersChevronSeparatorsBetweenRings()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));

        var chevrons = rendered.FindElements(
            e => e.Attributes.TryGetValue("class", out var c)
                && (c?.ToString() ?? "").Contains("phase-ring-chevron"));
        Assert.Equal(2, chevrons.Count);
    }

    [Fact]
    public async Task ReflectsRunningPhase_AfterTrackerProgressUpdate()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));
        tracker.OnProgress(Frame(ProgressStage.Transcribing, 45.0));
        await rendered.SynchronizeAsync();

        Assert.Contains("50", rendered.TextContent);
        Assert.Contains("transcribing", rendered.TextContent);
    }

    [Fact]
    public async Task SkippedDiarization_RendersSkippedRingInMiddle()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: false, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));

        Assert.Contains("skipped", rendered.TextContent, StringComparison.OrdinalIgnoreCase);
    }
}
