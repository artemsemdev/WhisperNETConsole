using VoxFlow.Core.Configuration;
using VoxFlow.Core.Services.Diarization;
using VoxFlow.Core.Services.Python;
using VoxFlow.Core.Tests.Services.Python;
using Xunit;

namespace VoxFlow.Core.Tests.Services.Diarization;

/// <summary>
/// Covers <see cref="CompositionSpeakerLabelingPreflight"/>: the real
/// <c>ISpeakerLabelingPreflight</c> implementation that probes the Python
/// runtime status (per <see cref="PythonRuntimeMode"/>) and checks whether
/// a Hugging Face model is cached under the configured hub root.
/// </summary>
public sealed class CompositionSpeakerLabelingPreflightTests
{
    [Fact]
    public async Task GetRuntimeStatusAsync_ManagedVenv_NotYetCreated_ReturnsBootstrapable()
    {
        using var paths = new FakeVenvPaths();
        var launcher = new FakeProcessLauncher();
        using var cacheDir = new TemporaryDirectory();
        var preflight = new CompositionSpeakerLabelingPreflight(launcher, paths, cacheDir.Path);
        var options = SpeakerLabelingOptions.Disabled with
        {
            Enabled = true,
            RuntimeMode = PythonRuntimeMode.ManagedVenv
        };

        var status = await preflight.GetRuntimeStatusAsync(options, CancellationToken.None);

        Assert.False(status.IsReady);
        Assert.True(status.CanBootstrap);
        Assert.Contains("venv", status.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRuntimeStatusAsync_ManagedVenv_InterpreterExists_ReturnsReady()
    {
        using var paths = new FakeVenvPaths();
        paths.MaterializeVenv();
        var launcher = new FakeProcessLauncher();
        using var cacheDir = new TemporaryDirectory();
        var preflight = new CompositionSpeakerLabelingPreflight(launcher, paths, cacheDir.Path);
        var options = SpeakerLabelingOptions.Disabled with
        {
            Enabled = true,
            RuntimeMode = PythonRuntimeMode.ManagedVenv
        };

        var status = await preflight.GetRuntimeStatusAsync(options, CancellationToken.None);

        Assert.True(status.IsReady);
        Assert.Equal(paths.InterpreterPath, status.InterpreterPath);
    }

    [Fact]
    public async Task GetRuntimeStatusAsync_SystemPython_MissingOnPath_ReturnsNotReady()
    {
        using var paths = new FakeVenvPaths();
        var launcher = new FakeProcessLauncher();
        launcher.SetThrow("python3", new InvalidOperationException("not found"));
        using var cacheDir = new TemporaryDirectory();
        var preflight = new CompositionSpeakerLabelingPreflight(launcher, paths, cacheDir.Path);
        var options = SpeakerLabelingOptions.Disabled with
        {
            Enabled = true,
            RuntimeMode = PythonRuntimeMode.SystemPython
        };

        var status = await preflight.GetRuntimeStatusAsync(options, CancellationToken.None);

        Assert.False(status.IsReady);
        Assert.Contains("python3 not found", status.Error);
    }

    [Fact]
    public void IsModelCached_WhenDirectoryExists_ReturnsTrue()
    {
        using var paths = new FakeVenvPaths();
        var launcher = new FakeProcessLauncher();
        using var cacheDir = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(cacheDir.Path, "models--pyannote--speaker-diarization-3.1"));
        var preflight = new CompositionSpeakerLabelingPreflight(launcher, paths, cacheDir.Path);

        var cached = preflight.IsModelCached("pyannote/speaker-diarization-3.1");

        Assert.True(cached);
    }

    [Fact]
    public void IsModelCached_WhenDirectoryMissing_ReturnsFalse()
    {
        using var paths = new FakeVenvPaths();
        var launcher = new FakeProcessLauncher();
        using var cacheDir = new TemporaryDirectory();
        var preflight = new CompositionSpeakerLabelingPreflight(launcher, paths, cacheDir.Path);

        var cached = preflight.IsModelCached("pyannote/speaker-diarization-3.1");

        Assert.False(cached);
    }

    [Fact]
    public void IsModelCached_WhenHubRootMissing_ReturnsFalse()
    {
        using var paths = new FakeVenvPaths();
        var launcher = new FakeProcessLauncher();
        var missingRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var preflight = new CompositionSpeakerLabelingPreflight(launcher, paths, missingRoot);

        var cached = preflight.IsModelCached("pyannote/speaker-diarization-3.1");

        Assert.False(cached);
    }
}
