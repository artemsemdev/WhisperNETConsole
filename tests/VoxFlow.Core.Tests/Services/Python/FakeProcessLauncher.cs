using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Services.Python;

namespace VoxFlow.Core.Tests.Services.Python;

/// <summary>
/// Test double for <see cref="IProcessLauncher"/>. Canned responses are keyed by
/// the requested <see cref="ProcessStartInfo.FileName"/>; tests can also throw
/// from a scripted hook or simulate a never-returning process via cancellation.
/// </summary>
internal sealed class FakeProcessLauncher : IProcessLauncher
{
    private readonly Dictionary<string, Func<ProcessStartInfo, string?, Action<string>?, CancellationToken, Task<ProcessExecutionResult>>> _handlers = new();
    public List<ProcessStartInfo> Invocations { get; } = new();
    public List<string?> StdInputs { get; } = new();

    public void SetResponse(string fileName, int exitCode, string stdOut, string stdErr = "")
    {
        _handlers[fileName] = (_, _, onStdErrLine, _) =>
        {
            // Preserve the stderr-line contract for streaming callers: split the
            // canned buffer on newlines and invoke the callback per non-empty
            // line, the same way DefaultProcessLauncher's per-line reader would.
            EmitStdErrLines(stdErr, onStdErrLine);
            return Task.FromResult(new ProcessExecutionResult(exitCode, stdOut, stdErr));
        };
    }

    public void SetResponseFromStdin(string fileName, Func<string, ProcessExecutionResult> factory)
    {
        _handlers[fileName] = (_, stdIn, onStdErrLine, _) =>
        {
            var result = factory(stdIn ?? string.Empty);
            EmitStdErrLines(result.StdErr, onStdErrLine);
            return Task.FromResult(result);
        };
    }

    public void SetStreamingResponse(
        string fileName,
        Func<Action<string>, CancellationToken, Task<ProcessExecutionResult>> factory)
    {
        // Streaming variant: the factory itself decides when to emit stderr
        // lines relative to completion. Used to prove PyannoteSidecarClient
        // reports progress *before* the process exits.
        _handlers[fileName] = (_, _, onStdErrLine, ct) => factory(
            onStdErrLine ?? (static _ => { }), ct);
    }

    public void SetThrow(string fileName, Exception ex)
    {
        _handlers[fileName] = (_, _, _, _) => throw ex;
    }

    public void SetNeverReturns(string fileName)
    {
        _handlers[fileName] = async (_, _, _, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            throw new InvalidOperationException("unreachable");
        };
    }

    public Task<ProcessExecutionResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        => RunInternalAsync(startInfo, stdIn: null, onStdErrLine: null, cancellationToken);

    public Task<ProcessExecutionResult> RunAsync(ProcessStartInfo startInfo, string stdIn, CancellationToken cancellationToken)
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

    private Task<ProcessExecutionResult> RunInternalAsync(
        ProcessStartInfo startInfo,
        string? stdIn,
        Action<string>? onStdErrLine,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        Invocations.Add(startInfo);
        StdInputs.Add(stdIn);
        if (!_handlers.TryGetValue(startInfo.FileName, out var handler))
        {
            throw new InvalidOperationException($"No fake response configured for '{startInfo.FileName}'.");
        }

        return handler(startInfo, stdIn, onStdErrLine, cancellationToken);
    }

    private static void EmitStdErrLines(string stdErr, Action<string>? onStdErrLine)
    {
        if (onStdErrLine is null || string.IsNullOrEmpty(stdErr))
        {
            return;
        }

        foreach (var raw in stdErr.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;
            onStdErrLine(line);
        }
    }
}
