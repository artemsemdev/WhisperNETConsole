using System.Diagnostics;
using VoxFlow.Core.Services.Python;

namespace VoxFlow.Core.Interfaces;

/// <summary>
/// Abstracts how VoxFlow discovers and launches a Python interpreter for the
/// diarization sidecar. Implementations include <c>SystemPythonRuntime</c>
/// (resolves <c>python3</c> from PATH) and <c>ManagedVenvRuntime</c> (uses an
/// app-managed virtual environment).
/// </summary>
public interface IPythonRuntime
{
    Task<PythonRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken);

    ProcessStartInfo CreateStartInfo(string scriptPath, IEnumerable<string> arguments);
}
