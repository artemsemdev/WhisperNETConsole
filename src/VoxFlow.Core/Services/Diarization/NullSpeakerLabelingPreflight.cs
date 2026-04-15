using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Services.Python;

namespace VoxFlow.Core.Services.Diarization;

/// <summary>
/// Default <see cref="ISpeakerLabelingPreflight"/> used until the real
/// runtime + cache composition is wired into DI. Reports the runtime as
/// NotReady and the model cache as empty, which drives startup validation
/// to emit an informational warning rather than block the pipeline.
/// </summary>
public sealed class NullSpeakerLabelingPreflight : ISpeakerLabelingPreflight
{
    public Task<PythonRuntimeStatus> GetRuntimeStatusAsync(CancellationToken cancellationToken)
        => Task.FromResult(PythonRuntimeStatus.NotReady("speaker labeling runtime not configured"));

    public bool IsModelCached(string modelId) => false;
}
