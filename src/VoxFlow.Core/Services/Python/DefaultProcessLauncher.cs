using System.Diagnostics;
using System.Text;
using VoxFlow.Core.Interfaces;

namespace VoxFlow.Core.Services.Python;

/// <summary>
/// Production <see cref="IProcessLauncher"/> implementation backed by
/// <see cref="System.Diagnostics.Process"/>. Launches the requested child
/// process, captures stdout and stderr to memory, and returns once the
/// process exits. Cancellation kills the child process. The streaming
/// overload also invokes a per-line callback as each stderr line arrives,
/// so callers (the diarization sidecar) can animate progress in real time.
/// </summary>
public sealed class DefaultProcessLauncher : IProcessLauncher
{
    public Task<ProcessExecutionResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        => RunInternalAsync(startInfo, stdIn: null, onStdErrLine: null, cancellationToken);

    public Task<ProcessExecutionResult> RunAsync(
        ProcessStartInfo startInfo,
        string stdIn,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stdIn);
        return RunInternalAsync(startInfo, stdIn, onStdErrLine: null, cancellationToken);
    }

    public Task<ProcessExecutionResult> RunAsync(
        ProcessStartInfo startInfo,
        string stdIn,
        Action<string>? onStdErrLine,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stdIn);
        return RunInternalAsync(startInfo, stdIn, onStdErrLine, cancellationToken);
    }

    private static async Task<ProcessExecutionResult> RunInternalAsync(
        ProcessStartInfo startInfo,
        string? stdIn,
        Action<string>? onStdErrLine,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        if (stdIn is not null)
        {
            startInfo.RedirectStandardInput = true;
        }

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

        if (stdIn is not null)
        {
            await process.StandardInput.WriteAsync(stdIn.AsMemory(), cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = ReadStdErrAsync(process.StandardError, onStdErrLine, cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var stdOut = await stdOutTask.ConfigureAwait(false);
        var stdErr = await stdErrTask.ConfigureAwait(false);

        return new ProcessExecutionResult(process.ExitCode, stdOut, stdErr);
    }

    private static async Task<string> ReadStdErrAsync(
        StreamReader reader,
        Action<string>? onLine,
        CancellationToken cancellationToken)
    {
        var buffer = new StringBuilder();
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            buffer.AppendLine(line);
            if (onLine is not null)
            {
                try
                {
                    onLine(line);
                }
                catch
                {
                    // Callback failures must never abort the process read loop;
                    // progress is best-effort and buffered stderr is still useful
                    // for diagnostics even if a consumer threw.
                }
            }
        }
        return buffer.ToString();
    }
}
