using VoxFlow.Core.Models;

namespace VoxFlow.Core.Interfaces;

/// <summary>
/// Writes a <see cref="TranscriptDocument"/> as a sidecar
/// <c>.voxflow.json</c> artifact alongside a primary transcript file.
/// </summary>
public interface IVoxflowTranscriptArtifactWriter
{
    /// <summary>
    /// Writes <paramref name="document"/> to <c>{resultPath}.voxflow.json</c>.
    /// Implementations should be atomic — no partial file on cancellation.
    /// </summary>
    Task WriteAsync(
        string resultPath,
        TranscriptDocument document,
        CancellationToken cancellationToken);
}
