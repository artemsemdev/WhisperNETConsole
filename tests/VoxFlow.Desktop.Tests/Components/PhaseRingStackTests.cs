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

    private static bool IsPhaseArc(RenderedElement e, string phaseSlug)
        => e.Name == "circle"
            && e.Attributes.TryGetValue("data-phase", out var p)
            && (p?.ToString() ?? string.Empty) == phaseSlug;

    [Fact]
    public async Task RendersThreeNestedArcs_InPhaseOrder()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));

        var arcs = rendered
            .FindElements(e => e.Name == "circle" && e.Attributes.ContainsKey("data-phase"))
            .ToArray();
        Assert.Equal(3, arcs.Length);
        Assert.Equal("transcription", arcs[0].Attributes["data-phase"]?.ToString());
        Assert.Equal("diarization", arcs[1].Attributes["data-phase"]?.ToString());
        Assert.Equal("merge", arcs[2].Attributes["data-phase"]?.ToString());
    }

    [Fact]
    public async Task ArcsUseCorrectPhaseColorTokens()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));

        var transcription = rendered.FindElement(e => IsPhaseArc(e, "transcription"), "transcription arc");
        var diarization = rendered.FindElement(e => IsPhaseArc(e, "diarization"), "diarization arc");
        var merge = rendered.FindElement(e => IsPhaseArc(e, "merge"), "merge arc");

        Assert.Contains("--phase-transcription", transcription.Attributes["style"]?.ToString() ?? "");
        Assert.Contains("--phase-diarization", diarization.Attributes["style"]?.ToString() ?? "");
        Assert.Contains("--phase-merge", merge.Attributes["style"]?.ToString() ?? "");
    }

    [Fact]
    public async Task ArcRadiiMatchConcentricContract()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));

        var transcription = rendered.FindElement(e => IsPhaseArc(e, "transcription"), "transcription arc");
        var diarization = rendered.FindElement(e => IsPhaseArc(e, "diarization"), "diarization arc");
        var merge = rendered.FindElement(e => IsPhaseArc(e, "merge"), "merge arc");

        Assert.Equal("92", transcription.Attributes["r"]?.ToString());
        Assert.Equal("72", diarization.Attributes["r"]?.ToString());
        Assert.Equal("52", merge.Attributes["r"]?.ToString());
    }

    [Fact]
    public async Task ArcDashOffset_ReflectsLocalPercent()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));
        tracker.OnProgress(Frame(ProgressStage.Transcribing, 45.0));
        await rendered.SynchronizeAsync();

        var transcription = rendered.FindElement(e => IsPhaseArc(e, "transcription"), "transcription arc");
        var dashOffset = transcription.Attributes["stroke-dashoffset"]?.ToString() ?? "";
        var parsed = double.Parse(dashOffset, System.Globalization.CultureInfo.InvariantCulture);
        // Local percent for Transcribing @ 45% overall = 50%. Circumference 2π*92 ≈ 578.053.
        // Expected dashoffset at 50% = ~289.027.
        Assert.InRange(parsed, 288.5, 289.6);
    }

    [Fact]
    public async Task SkippedDiarization_MiddleArcUsesSkippedToken()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: false, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));

        var diarization = rendered.FindElement(e => IsPhaseArc(e, "diarization"), "diarization arc");
        Assert.Contains("--phase-skipped", diarization.Attributes["style"]?.ToString() ?? "");
    }

    [Fact]
    public async Task Center_ShowsPercentAndStageLabel_DuringRunning()
    {
        await using var context = DesktopUiTestContext.Create();
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        var rendered = await context.RenderAsync<PhaseRingStack>(Params(tracker));
        tracker.OnProgress(Frame(ProgressStage.Transcribing, 45.0));
        await rendered.SynchronizeAsync();

        var percent = rendered.FindElement(
            e => e.HasClass("phase-center-percent"),
            ".phase-center-percent");
        Assert.Contains("50", percent.TextContent);

        var label = rendered.FindElement(
            e => e.HasClass("phase-center-label"),
            ".phase-center-label");
        Assert.Contains("TRANSCRIPTION", label.TextContent, StringComparison.OrdinalIgnoreCase);
    }
}
