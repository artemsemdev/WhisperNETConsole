using System.Diagnostics;
using VoxFlow.Core.Services.Python;

namespace VoxFlow.Core.Interfaces;

/// <summary>
/// Thin abstraction over <see cref="System.Diagnostics.Process"/> that launches
/// a child process, waits for it to exit, and returns captured stdout/stderr.
/// Injected so tests can fake Python without spawning real interpreters.
/// </summary>
public interface IProcessLauncher
{
    Task<ProcessExecutionResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken);
}
