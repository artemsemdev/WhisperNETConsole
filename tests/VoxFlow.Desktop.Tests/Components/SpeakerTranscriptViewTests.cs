using Microsoft.AspNetCore.Components;
using VoxFlow.Core.Models;
using VoxFlow.Desktop.Components.Shared;
using Xunit;

namespace VoxFlow.Desktop.Tests.Components;

public sealed class SpeakerTranscriptViewTests
{
    private static TranscriptMetadata Meta() => new(
        SchemaVersion: 1,
        DiarizationModel: "test",
        SidecarVersion: 1);

    private static TranscriptWord Word(string speaker, double start, double end, string text) =>
        new(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), text, speaker);

    private static TranscriptDocument Doc(params SpeakerTurn[] turns)
    {
        var words = turns.SelectMany(t => t.Words).ToList();
        var speakerIds = turns.Select(t => t.SpeakerId).Distinct().ToList();
        var roster = speakerIds
            .Select(id => new SpeakerInfo(id, id, TimeSpan.Zero))
            .ToList();
        return new TranscriptDocument(roster, words, turns, Meta());
    }

    private static SpeakerTurn Turn(string speaker, double start, double end, params string[] words)
    {
        var tokens = words
            .Select((w, i) => Word(speaker, start + i * 0.1, start + (i + 1) * 0.1, w))
            .ToList();
        return new SpeakerTurn(speaker, TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), tokens);
    }

    private static ParameterView Params(TranscriptDocument? doc) =>
        ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(SpeakerTranscriptView.Document)] = doc,
        });

    [Fact]
    public async Task Renders_NullDocument_ProducesEmptyMarkup()
    {
        await using var context = DesktopUiTestContext.Create();

        var rendered = await context.RenderAsync<SpeakerTranscriptView>(Params(null));

        Assert.Equal(string.Empty, rendered.TextContent.Trim());
    }

    [Fact]
    public async Task Renders_EmptyTurns_ShowsNoSegmentsMessage()
    {
        await using var context = DesktopUiTestContext.Create();
        var doc = new TranscriptDocument(
            Array.Empty<SpeakerInfo>(),
            Array.Empty<TranscriptWord>(),
            Array.Empty<SpeakerTurn>(),
            Meta());

        var rendered = await context.RenderAsync<SpeakerTranscriptView>(Params(doc));

        Assert.Contains("No speaker segments detected", rendered.TextContent);
    }

    [Fact]
    public async Task Renders_SingleSpeakerTurn_HasCorrectLabelAndColor()
    {
        await using var context = DesktopUiTestContext.Create();
        var doc = Doc(Turn("A", 0, 2, "hello", "world"));

        var rendered = await context.RenderAsync<SpeakerTranscriptView>(Params(doc));

        var turnElement = rendered.FindElement(
            e => e.Attributes.TryGetValue("data-speaker-label", out var v) && v?.ToString() == "A",
            "turn element with data-speaker-label=A");

        Assert.Contains("hello world", rendered.TextContent);
        var style = turnElement.Attributes["style"]?.ToString() ?? string.Empty;
        Assert.Contains("#E69F00", style, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Renders_TwoSpeakers_AlternatingTurns_HaveDifferentColors()
    {
        await using var context = DesktopUiTestContext.Create();
        var doc = Doc(
            Turn("A", 0, 1, "one"),
            Turn("B", 1, 2, "two"),
            Turn("A", 2, 3, "three"),
            Turn("B", 3, 4, "four"));

        var rendered = await context.RenderAsync<SpeakerTranscriptView>(Params(doc));

        var turns = rendered.FindElements(
            e => e.Attributes.ContainsKey("data-speaker-label"));

        Assert.Equal(4, turns.Count);
        var aStyle = turns[0].Attributes["style"]?.ToString() ?? string.Empty;
        var bStyle = turns[1].Attributes["style"]?.ToString() ?? string.Empty;
        Assert.Contains("#E69F00", aStyle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#56B4E9", bStyle, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(aStyle, bStyle);
    }

    [Fact]
    public async Task Renders_TimestampRange_PerTurn()
    {
        await using var context = DesktopUiTestContext.Create();
        var doc = Doc(Turn("A", 3, 12, "hi"));

        var rendered = await context.RenderAsync<SpeakerTranscriptView>(Params(doc));

        Assert.Contains("00:03", rendered.TextContent);
        Assert.Contains("00:12", rendered.TextContent);
    }

    [Fact]
    public async Task Renders_NineSpeakers_ColorsWrapAroundPalette()
    {
        await using var context = DesktopUiTestContext.Create();
        var labels = new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I" };
        var turns = labels.Select((l, i) => Turn(l, i, i + 1, "x")).ToArray();
        var doc = Doc(turns);

        var rendered = await context.RenderAsync<SpeakerTranscriptView>(Params(doc));

        var turnElements = rendered.FindElements(
            e => e.Attributes.ContainsKey("data-speaker-label"));

        Assert.Equal(9, turnElements.Count);
        var firstStyle = turnElements[0].Attributes["style"]?.ToString() ?? string.Empty;
        var ninthStyle = turnElements[8].Attributes["style"]?.ToString() ?? string.Empty;
        Assert.Equal(firstStyle, ninthStyle);
    }

    [Fact]
    public async Task Renders_SpeakerLabel_InTurn()
    {
        await using var context = DesktopUiTestContext.Create();
        var doc = Doc(Turn("A", 0, 1, "hello"));

        var rendered = await context.RenderAsync<SpeakerTranscriptView>(Params(doc));

        Assert.Contains("Speaker A", rendered.TextContent);
    }
}
