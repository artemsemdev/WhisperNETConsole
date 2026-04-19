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

    /// <summary>
    /// Streaming variant that invokes <paramref name="onStdErrLine"/> for each
    /// stderr line as it arrives (not after process exit). The captured stderr
    /// is also accumulated into <see cref="ProcessExecutionResult.StdErr"/> so
    /// callers that want the buffer for diagnostics still get it. Used by the
    /// diarization sidecar to animate the CLI progress bar in real time while
    /// pyannote.audio's pipeline is running.
    /// </summary>
    Task<ProcessExecutionResult> RunAsync(
        ProcessStartInfo startInfo,
        string stdIn,
        Action<string>? onStdErrLine,
        CancellationToken cancellationToken);
}
