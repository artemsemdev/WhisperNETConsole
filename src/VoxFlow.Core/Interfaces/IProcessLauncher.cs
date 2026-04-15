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

    /// <summary>
    /// Variant that writes <paramref name="stdIn"/> to the child's standard
    /// input (closing it afterwards) and captures stdout/stderr as strings.
    /// Phase 0 buffers stderr and forwards it to the caller after the process
    /// exits; real-time stderr streaming can be added later without breaking
    /// the interface.
    /// </summary>
    Task<ProcessExecutionResult> RunAsync(
        ProcessStartInfo startInfo,
        string stdIn,
        CancellationToken cancellationToken);
}
