using VoxFlow.Core.Configuration;
using VoxFlow.Core.Services.Python;

namespace VoxFlow.Core.Interfaces;

/// <summary>
/// Surface queried by <c>ValidationService</c> during startup to decide
/// whether speaker labeling prerequisites are in place. Wraps the Python
/// runtime status probe and the pyannote model cache probe so tests can
/// substitute both without spinning up a real interpreter or touching disk.
/// </summary>
public interface ISpeakerLabelingPreflight
{
    Task<PythonRuntimeStatus> GetRuntimeStatusAsync(
        SpeakerLabelingOptions options,
        CancellationToken cancellationToken);

    bool IsModelCached(string modelId);
}
