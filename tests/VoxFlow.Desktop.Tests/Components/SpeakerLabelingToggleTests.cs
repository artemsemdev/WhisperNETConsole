using VoxFlow.Desktop.Components.Shared;
using VoxFlow.Desktop.Configuration;
using Xunit;

namespace VoxFlow.Desktop.Tests.Components;

internal sealed class RecordingDesktopConfigurationService : DesktopConfigurationService
{
    public RecordingDesktopConfigurationService()
        : base(
            Path.Combine(Path.GetTempPath(), $"voxflow-recording-cfg-{Guid.NewGuid():N}"),
            Path.Combine(Path.GetTempPath(), $"voxflow-recording-docs-{Guid.NewGuid():N}"))
    {
    }

    public List<Dictionary<string, object>> SavedOverrides { get; } = [];

    public Exception? SaveException { get; set; }

    public override Task SaveUserOverridesAsync(Dictionary<string, object> overrides)
    {
        if (SaveException is not null) throw SaveException;
        SavedOverrides.Add(new Dictionary<string, object>(overrides));
        return Task.CompletedTask;
    }
}

public sealed class SpeakerLabelingToggleTests
{
    [Fact]
    public async Task SpeakerLabelingToggle_WhenFlagDisabled_AriaCheckedIsFalse()
    {
        await using var context = DesktopUiTestContext.Create();
        context.ViewModel.SpeakerLabelingEnabled = false;

        var rendered = await context.RenderAsync<SpeakerLabelingToggle>();

        var switchEl = rendered.FindElement(
            e => e.Name == "button"
                && e.Attributes.TryGetValue("role", out var role)
                && (role?.ToString() ?? string.Empty) == "switch",
            "speaker-labeling switch");
        Assert.Equal("false", switchEl.Attributes["aria-checked"]?.ToString());
    }

    [Fact]
    public async Task SpeakerLabelingToggle_WhenFlagEnabled_AriaCheckedIsTrue()
    {
        await using var context = DesktopUiTestContext.Create();
        context.ViewModel.SpeakerLabelingEnabled = true;

        var rendered = await context.RenderAsync<SpeakerLabelingToggle>();

        var switchEl = rendered.FindElement(
            e => e.Name == "button"
                && e.Attributes.TryGetValue("role", out var role)
                && (role?.ToString() ?? string.Empty) == "switch",
            "speaker-labeling switch");
        Assert.Equal("true", switchEl.Attributes["aria-checked"]?.ToString());
    }

    [Fact]
    public async Task SpeakerLabelingToggle_Click_FlipsViewModelFlag()
    {
        await using var context = DesktopUiTestContext.Create();
        context.ViewModel.SpeakerLabelingEnabled = false;

        var rendered = await context.RenderAsync<SpeakerLabelingToggle>();

        await rendered.ClickAsync(
            e => e.Name == "button"
                && e.Attributes.TryGetValue("role", out var role)
                && (role?.ToString() ?? string.Empty) == "switch",
            "speaker-labeling switch");

        Assert.True(context.ViewModel.SpeakerLabelingEnabled);
    }

    [Fact]
    public async Task SpeakerLabelingToggle_Click_WhenEnabled_TurnsOff()
    {
        await using var context = DesktopUiTestContext.Create();
        context.ViewModel.SpeakerLabelingEnabled = true;

        var rendered = await context.RenderAsync<SpeakerLabelingToggle>();

        await rendered.ClickAsync(
            e => e.Name == "button"
                && e.Attributes.TryGetValue("role", out var role)
                && (role?.ToString() ?? string.Empty) == "switch",
            "speaker-labeling switch");

        Assert.False(context.ViewModel.SpeakerLabelingEnabled);
    }

    [Fact]
    public async Task SpeakerLabelingToggle_Click_PersistsNestedSpeakerLabelingOverride()
    {
        var recorder = new RecordingDesktopConfigurationService();
        await using var context = DesktopUiTestContext.Create(desktopConfigurationService: recorder);
        context.ViewModel.SpeakerLabelingEnabled = false;

        var rendered = await context.RenderAsync<SpeakerLabelingToggle>();

        await rendered.ClickAsync(
            e => e.Name == "button"
                && e.Attributes.TryGetValue("role", out var role)
                && (role?.ToString() ?? string.Empty) == "switch",
            "speaker-labeling switch");

        var saved = Assert.Single(recorder.SavedOverrides);
        var speakerLabeling = Assert.IsAssignableFrom<IDictionary<string, object>>(saved["speakerLabeling"]);
        Assert.True((bool)speakerLabeling["enabled"]);
    }

    [Fact]
    public async Task SpeakerLabelingToggle_Click_WhenPersistThrows_DoesNotBubbleAndKeepsInMemoryFlag()
    {
        var recorder = new RecordingDesktopConfigurationService
        {
            SaveException = new IOException("disk full"),
        };
        await using var context = DesktopUiTestContext.Create(desktopConfigurationService: recorder);
        context.ViewModel.SpeakerLabelingEnabled = false;

        var rendered = await context.RenderAsync<SpeakerLabelingToggle>();

        await rendered.ClickAsync(
            e => e.Name == "button"
                && e.Attributes.TryGetValue("role", out var role)
                && (role?.ToString() ?? string.Empty) == "switch",
            "speaker-labeling switch");

        Assert.True(context.ViewModel.SpeakerLabelingEnabled);
    }
}
