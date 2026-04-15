using VoxFlow.Core.Interfaces;

namespace VoxFlow.Core.Services.Python;

/// <summary>
/// Default <see cref="IManagedVenvBootstrapper"/> that forwards to
/// <see cref="ManagedVenvRuntime.CreateVenvAsync"/>. Kept as a thin adapter
/// so <c>SpeakerEnrichmentService</c> can remain decoupled from the concrete
/// runtime and tests can substitute a fake bootstrapper.
/// </summary>
public sealed class ManagedVenvBootstrapper : IManagedVenvBootstrapper
{
    private readonly ManagedVenvRuntime _runtime;

    public ManagedVenvBootstrapper(ManagedVenvRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public Task BootstrapAsync(IProgress<VenvBootstrapStage>? progress, CancellationToken cancellationToken)
        => _runtime.CreateVenvAsync(progress, cancellationToken);
}
