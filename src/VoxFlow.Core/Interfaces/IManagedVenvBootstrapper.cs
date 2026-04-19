using VoxFlow.Core.Services.Python;

namespace VoxFlow.Core.Interfaces;

/// <summary>
/// Creates and populates the managed Python virtual environment used by the
/// diarization sidecar. Abstracted from <c>ManagedVenvRuntime</c> so the
/// enrichment orchestrator can remain decoupled from runtime-specific
/// bootstrap mechanics and so tests can simulate bootstrap outcomes.
/// </summary>
public interface IManagedVenvBootstrapper
{
    Task BootstrapAsync(IProgress<VenvBootstrapStage>? progress, CancellationToken cancellationToken);
}
