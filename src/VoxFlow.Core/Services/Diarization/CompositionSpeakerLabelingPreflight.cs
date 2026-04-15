using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Services.Python;

namespace VoxFlow.Core.Services.Diarization;

/// <summary>
/// Real <see cref="ISpeakerLabelingPreflight"/>: probes the Python runtime
/// status for the requested <see cref="PythonRuntimeMode"/> and checks
/// whether a pyannote model is present in the local Hugging Face hub cache.
/// Runs entirely in-process so startup validation can surface informational
/// warnings without spawning the sidecar.
/// </summary>
public sealed class CompositionSpeakerLabelingPreflight : ISpeakerLabelingPreflight
{
    private readonly IProcessLauncher _launcher;
    private readonly IVenvPaths _venvPaths;
    private readonly string _hubCacheRoot;

    public CompositionSpeakerLabelingPreflight(
        IProcessLauncher launcher,
        IVenvPaths venvPaths,
        string hubCacheRoot)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(venvPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(hubCacheRoot);
        _launcher = launcher;
        _venvPaths = venvPaths;
        _hubCacheRoot = hubCacheRoot;
    }

    /// <summary>
    /// Resolves the default Hugging Face hub cache root using the same
    /// precedence rules huggingface_hub uses: HF_HUB_CACHE, then HF_HOME/hub,
    /// then the platform-specific default (~/.cache/huggingface/hub on
    /// macOS/Linux, %USERPROFILE%\.cache\huggingface\hub on Windows).
    /// </summary>
    public static string ResolveDefaultHubCacheRoot()
    {
        var explicitRoot = Environment.GetEnvironmentVariable("HF_HUB_CACHE");
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            return explicitRoot;
        }

        var hfHome = Environment.GetEnvironmentVariable("HF_HOME");
        if (!string.IsNullOrWhiteSpace(hfHome))
        {
            return Path.Combine(hfHome, "hub");
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".cache", "huggingface", "hub");
    }

    public Task<PythonRuntimeStatus> GetRuntimeStatusAsync(
        SpeakerLabelingOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        IPythonRuntime runtime = options.RuntimeMode switch
        {
            PythonRuntimeMode.ManagedVenv => new ManagedVenvRuntime(_launcher, _venvPaths),
            PythonRuntimeMode.SystemPython => new SystemPythonRuntime(_launcher),
            PythonRuntimeMode.Standalone => null!,
            _ => null!
        };

        if (runtime is null)
        {
            return Task.FromResult(PythonRuntimeStatus.NotReady(
                $"runtime mode {options.RuntimeMode} is not yet supported in Phase 1."));
        }

        return runtime.GetStatusAsync(cancellationToken);
    }

    public bool IsModelCached(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        if (!Directory.Exists(_hubCacheRoot))
        {
            return false;
        }

        var parts = modelId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var slug = parts.Length == 2
            ? $"models--{parts[0]}--{parts[1]}"
            : $"models--{modelId}";
        return Directory.Exists(Path.Combine(_hubCacheRoot, slug));
    }
}
