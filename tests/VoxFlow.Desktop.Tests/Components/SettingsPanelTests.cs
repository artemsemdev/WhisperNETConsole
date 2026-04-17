using VoxFlow.Desktop.Components.Shared;
using Xunit;

namespace VoxFlow.Desktop.Tests.Components;

public sealed class SettingsPanelTests
{
    [Fact]
    public async Task SettingsPanel_RendersSpeakerLabelingToggle()
    {
        await using var context = DesktopUiTestContext.Create();

        var rendered = await context.RenderAsync<SettingsPanel>();

        var toggle = rendered.FindElements(
            e => e.Attributes.TryGetValue("id", out var id)
                && (id?.ToString() ?? string.Empty) == "speaker-labeling-toggle");
        Assert.Single(toggle);
    }

    [Fact]
    public async Task SettingsPanel_RendersSpeakerToggleAfterFormatPicker()
    {
        await using var context = DesktopUiTestContext.Create();

        var rendered = await context.RenderAsync<SettingsPanel>();

        Assert.Contains("Speaker labeling", rendered.TextContent);
        Assert.Contains("Output Format", rendered.TextContent);
        var formatIndex = rendered.TextContent.IndexOf("Output Format", StringComparison.Ordinal);
        var speakerIndex = rendered.TextContent.IndexOf("Speaker labeling", StringComparison.Ordinal);
        Assert.True(speakerIndex > formatIndex, "Speaker toggle must render after format picker");
    }

    [Fact]
    public async Task SettingsPanel_WhenDisabled_PropagatesIsDisabledToToggle()
    {
        await using var context = DesktopUiTestContext.Create();
        context.ViewModel.SpeakerLabelingEnabled = false;
        var parameters = Microsoft.AspNetCore.Components.ParameterView.FromDictionary(
            new Dictionary<string, object?> { [nameof(SettingsPanel.IsDisabled)] = true });

        var rendered = await context.RenderAsync<SettingsPanel>(parameters);

        var switchEl = rendered.FindElement(
            e => e.Name == "button"
                && e.Attributes.TryGetValue("role", out var role)
                && (role?.ToString() ?? string.Empty) == "switch",
            "speaker-labeling switch");
        Assert.True(switchEl.Attributes.ContainsKey("disabled"));
    }
}
