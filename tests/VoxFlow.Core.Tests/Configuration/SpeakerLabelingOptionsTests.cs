using System;
using VoxFlow.Core.Configuration;
using Xunit;

namespace VoxFlow.Core.Tests.Configuration;

public sealed class SpeakerLabelingOptionsTests
{
    [Fact]
    public void Disabled_StaticInstance_HasEnabledFalse_AndHarmlessDefaults()
    {
        var disabled = SpeakerLabelingOptions.Disabled;

        Assert.False(disabled.Enabled);
        Assert.Equal(600, disabled.TimeoutSeconds);
        Assert.Equal(PythonRuntimeMode.ManagedVenv, disabled.RuntimeMode);
        Assert.Equal("pyannote/speaker-diarization-3.1", disabled.ModelId);
    }

    [Fact]
    public void Construct_ValidInputs_ExposesFields()
    {
        var options = new SpeakerLabelingOptions(
            Enabled: true,
            TimeoutSeconds: 900,
            RuntimeMode: PythonRuntimeMode.SystemPython,
            ModelId: "pyannote/custom-model");

        Assert.True(options.Enabled);
        Assert.Equal(900, options.TimeoutSeconds);
        Assert.Equal(PythonRuntimeMode.SystemPython, options.RuntimeMode);
        Assert.Equal("pyannote/custom-model", options.ModelId);
    }

    [Fact]
    public void Construct_NegativeTimeout_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new SpeakerLabelingOptions(
            Enabled: true,
            TimeoutSeconds: -1,
            RuntimeMode: PythonRuntimeMode.ManagedVenv,
            ModelId: "pyannote/speaker-diarization-3.1"));

        Assert.Contains("TimeoutSeconds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Construct_ZeroTimeout_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new SpeakerLabelingOptions(
            Enabled: true,
            TimeoutSeconds: 0,
            RuntimeMode: PythonRuntimeMode.ManagedVenv,
            ModelId: "pyannote/speaker-diarization-3.1"));
    }

    [Fact]
    public void Construct_EmptyModelId_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new SpeakerLabelingOptions(
            Enabled: true,
            TimeoutSeconds: 600,
            RuntimeMode: PythonRuntimeMode.ManagedVenv,
            ModelId: "   "));

        Assert.Contains("ModelId", exception.Message, StringComparison.Ordinal);
    }
}
