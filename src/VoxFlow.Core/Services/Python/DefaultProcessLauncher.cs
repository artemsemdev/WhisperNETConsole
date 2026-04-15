using System.Diagnostics;
using VoxFlow.Core.Interfaces;

namespace VoxFlow.Core.Services.Python;

/// <summary>
/// Production <see cref="IProcessLauncher"/> implementation backed by
/// <see cref="System.Diagnostics.Process"/>. Launches the requested child
/// process, captures stdout and stderr to memory, and returns once the
/// process exits. Cancellation kills the child process.
/// </summary>
public sealed class DefaultProcessLauncher : IProcessLauncher
{
    public async Task<ProcessExecutionResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Process may have exited between HasExited check and Kill; swallow.
            }
        });

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var stdOut = await stdOutTask.ConfigureAwait(false);
        var stdErr = await stdErrTask.ConfigureAwait(false);

        return new ProcessExecutionResult(process.ExitCode, stdOut, stdErr);
    }
}
