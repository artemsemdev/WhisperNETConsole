namespace VoxFlow.Core.Services.Python;

/// <summary>
/// Outcome of probing an <see cref="IPythonRuntime"/>. A ready status has a
/// non-null <see cref="InterpreterPath"/> and <see cref="Version"/>; a
/// not-ready status has a non-null <see cref="Error"/> explaining why.
/// </summary>
public sealed record PythonRuntimeStatus(
    bool IsReady,
    string? InterpreterPath,
    string? Version,
    string? Error)
{
    public static PythonRuntimeStatus Ready(string interpreterPath, string version)
        => new(IsReady: true, interpreterPath, version, Error: null);

    public static PythonRuntimeStatus NotReady(string error)
        => new(IsReady: false, InterpreterPath: null, Version: null, error);
}
