using System;
using System.Threading;
using System.Threading.Tasks;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Interfaces;

/// <summary>
/// Bridges the .NET speaker-labeling pipeline to the <c>voxflow_diarize.py</c>
/// sidecar. Implementations own the process lifetime, stdin/stdout/stderr
/// plumbing, schema validation, and the failure taxonomy surfaced via
/// <c>DiarizationSidecarException</c>.
/// </summary>
public interface IDiarizationSidecar
{
    Task<DiarizationResult> DiarizeAsync(
        DiarizationRequest request,
        IProgress<SpeakerLabelingProgress>? progress,
        CancellationToken cancellationToken);
}
