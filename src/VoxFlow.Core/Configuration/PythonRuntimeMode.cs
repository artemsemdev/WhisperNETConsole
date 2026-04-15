namespace VoxFlow.Core.Configuration;

/// <summary>
/// Selects which Python runtime implementation hosts the speaker-labeling sidecar.
/// Standalone is declared now so the config schema stays stable; its runtime lands in Phase 3.
/// </summary>
public enum PythonRuntimeMode
{
    SystemPython,
    ManagedVenv,
    Standalone
}
