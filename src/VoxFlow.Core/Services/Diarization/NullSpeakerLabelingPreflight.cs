using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Services.Python;

namespace VoxFlow.Core.Services.Diarization;

/// <summary>
/// Fallback <see cref="ISpeakerLabelingPreflight"/> kept for tests and for the
/// rare host that never composes a real runtime. Reports NotReady and empty
/// cache so startup validation emits an informational warning rather than
/// blocking the pipeline.
/// </summary>
public sealed class NullSpeakerLabelingPreflight : ISpeakerLabelingPreflight
{
    public Task<PythonRuntimeStatus> GetRuntimeStatusAsync(
        SpeakerLabelingOptions options,
        CancellationToken cancellationToken)
        => Task.FromResult(PythonRuntimeStatus.NotReady("speaker labeling runtime not configured"));

    public bool IsModelCached(string modelId) => false;
}
