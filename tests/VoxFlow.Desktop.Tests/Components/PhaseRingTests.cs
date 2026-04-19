using Microsoft.AspNetCore.Components;
using VoxFlow.Core.Models;
using VoxFlow.Desktop.Components.Pages;
using VoxFlow.Desktop.ViewModels;
using Xunit;

namespace VoxFlow.Desktop.Tests.Components;

public sealed class PhaseRingTests
{
    // Ring geometry kept in sync with the component: r=50, circumference=2πr.
    private const double Radius = 50.0;
    private static readonly double Circumference = 2.0 * Math.PI * Radius;

    private static ParameterView Params(PhaseState state, string phaseToken = "--phase-transcription")
        => ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(PhaseRing.State)] = state,
            [nameof(PhaseRing.PhaseToken)] = phaseToken,
        });

    [Fact]
    public async Task Idle_RendersTrackCircleOnly_NoArcNoElapsed()
    {
        await using var context = DesktopUiTestContext.Create();
        var state = new PhaseState(ProgressPhase.Transcription, PhaseStatus.Idle, 0, null, TimeSpan.Zero);

        var rendered = await context.RenderAsync<PhaseRing>(Params(state));

        var tracks = rendered.FindElements(
            e => e.Name == "circle"
                && e.Attributes.TryGetValue("class", out var c)
                && (c?.ToString() ?? "").Contains("phase-ring-track"));
        Assert.Single(tracks);

        var arcs = rendered.FindElements(
            e => e.Name == "circle"
                && e.Attributes.TryGetValue("class", out var c)
                && (c?.ToString() ?? "").Contains("phase-ring-arc"));
        Assert.Empty(arcs);

        var elapsed = rendered.FindElements(
            e => e.Attributes.TryGetValue("class", out var c)
                && (c?.ToString() ?? "").Contains("phase-ring-elapsed"));
        Assert.Empty(elapsed);
    }

    [Fact]
    public async Task Running_RendersArc_WithDashoffsetProportionalToLocalPercent()
    {
        await using var context = DesktopUiTestContext.Create();
        var state = new PhaseState(ProgressPhase.Transcription, PhaseStatus.Running, 25.0, "transcribing", TimeSpan.FromSeconds(3));

        var rendered = await context.RenderAsync<PhaseRing>(Params(state));

        var arc = rendered.FindElement(
            e => e.Name == "circle"
                && e.Attributes.TryGetValue("class", out var c)
                && (c?.ToString() ?? "").Contains("phase-ring-arc"),
            "phase-ring arc");

        var dashoffsetValue = arc.Attributes["stroke-dashoffset"]?.ToString() ?? "";
        var parsed = double.Parse(dashoffsetValue, System.Globalization.CultureInfo.InvariantCulture);
        var expected = Circumference * 0.75;
        Assert.InRange(parsed, expected - 0.5, expected + 0.5);
    }

    [Fact]
    public async Task Running_ShowsLocalPercentValueInCenter()
    {
        await using var context = DesktopUiTestContext.Create();
        var state = new PhaseState(ProgressPhase.Transcription, PhaseStatus.Running, 45.0, "transcribing", TimeSpan.FromSeconds(12));

        var rendered = await context.RenderAsync<PhaseRing>(Params(state));

        Assert.Contains("45", rendered.TextContent);
        Assert.Contains("%", rendered.TextContent);
        Assert.Contains("transcribing", rendered.TextContent);
        Assert.Contains("0:12", rendered.TextContent);
    }

    [Fact]
    public async Task Done_RendersFullArc_AndDoneLabel_AndFrozenElapsed()
    {
        await using var context = DesktopUiTestContext.Create();
        var state = new PhaseState(ProgressPhase.Transcription, PhaseStatus.Done, 100.0, "done", TimeSpan.FromSeconds(22));

        var rendered = await context.RenderAsync<PhaseRing>(Params(state));

        var arc = rendered.FindElement(
            e => e.Name == "circle"
                && e.Attributes.TryGetValue("class", out var c)
                && (c?.ToString() ?? "").Contains("phase-ring-arc"),
            "phase-ring arc");

        var dashoffsetValue = arc.Attributes["stroke-dashoffset"]?.ToString() ?? "";
        var parsed = double.Parse(dashoffsetValue, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(parsed, -0.5, 0.5);

        Assert.Contains("done", rendered.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0:22", rendered.TextContent);
    }

    [Fact]
    public async Task Skipped_RendersSkippedLabel_NoElapsed_NoArc()
    {
        await using var context = DesktopUiTestContext.Create();
        var state = new PhaseState(ProgressPhase.Diarization, PhaseStatus.Skipped, 0, "skipped", TimeSpan.Zero);

        var rendered = await context.RenderAsync<PhaseRing>(Params(state, "--phase-diarization"));

        Assert.Contains("skipped", rendered.TextContent, StringComparison.OrdinalIgnoreCase);

        var arcs = rendered.FindElements(
            e => e.Name == "circle"
                && e.Attributes.TryGetValue("class", out var c)
                && (c?.ToString() ?? "").Contains("phase-ring-arc"));
        Assert.Empty(arcs);

        var elapsed = rendered.FindElements(
            e => e.Attributes.TryGetValue("class", out var c)
                && (c?.ToString() ?? "").Contains("phase-ring-elapsed"));
        Assert.Empty(elapsed);
    }

    [Fact]
    public async Task Failed_RendersFailedLabel()
    {
        await using var context = DesktopUiTestContext.Create();
        var state = new PhaseState(ProgressPhase.Transcription, PhaseStatus.Failed, 30.0, "model load failed", TimeSpan.FromSeconds(5));

        var rendered = await context.RenderAsync<PhaseRing>(Params(state));

        Assert.Contains("failed", rendered.TextContent, StringComparison.OrdinalIgnoreCase);
    }
}
