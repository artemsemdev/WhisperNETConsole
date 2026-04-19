namespace VoxFlow.Core.Services.Python;

/// <summary>
/// Progress checkpoints emitted during <see cref="ManagedVenvRuntime.CreateVenvAsync"/>.
/// </summary>
public enum VenvBootstrapStage
{
    CreatingVenv,
    InstallingRequirements,
    Verifying,
    Complete
}
