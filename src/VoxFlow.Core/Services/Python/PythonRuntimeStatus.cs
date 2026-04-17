namespace VoxFlow.Core.Services.Python;

/// <summary>
/// Outcome of probing an <see cref="VoxFlow.Core.Interfaces.IPythonRuntime"/>.
/// A ready status has a non-null <see cref="InterpreterPath"/> and
/// <see cref="Version"/>; a not-ready status has a non-null
/// <see cref="Error"/> explaining why. <see cref="CanBootstrap"/> is true
/// when the runtime is recoverable via a managed-venv bootstrap step (e.g.,
/// the venv has not been created yet).
/// </summary>
public sealed record PythonRuntimeStatus(
    bool IsReady,
    string? InterpreterPath,
    string? Version,
    string? Error,
    bool CanBootstrap = false)
{
    public static PythonRuntimeStatus Ready(string interpreterPath, string version)
        => new(IsReady: true, interpreterPath, version, Error: null);

    public static PythonRuntimeStatus NotReady(string error)
        => new(IsReady: false, InterpreterPath: null, Version: null, error);

    public static PythonRuntimeStatus NotReadyBootstrapable(string error)
        => new(IsReady: false, InterpreterPath: null, Version: null, error, CanBootstrap: true);
}
