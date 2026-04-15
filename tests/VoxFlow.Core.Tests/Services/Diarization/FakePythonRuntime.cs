using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Services.Python;

namespace VoxFlow.Core.Tests.Services.Diarization;

/// <summary>
/// Test double for <see cref="IPythonRuntime"/>. Tests can mark the runtime
/// ready/not-ready and inspect what <see cref="CreateStartInfo"/> was asked
/// to build.
/// </summary>
internal sealed class FakePythonRuntime : IPythonRuntime
{
    public string InterpreterPath { get; set; } = "/fake/venv/bin/python3";
    public PythonRuntimeStatus NextStatus { get; set; }
        = PythonRuntimeStatus.Ready("/fake/venv/bin/python3", "3.11.0");
    public Queue<PythonRuntimeStatus> StatusQueue { get; } = new();
    public int GetStatusCallCount { get; private set; }
    public List<(string ScriptPath, string[] Args)> StartInfoRequests { get; } = new();

    public Task<PythonRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        GetStatusCallCount++;
        if (StatusQueue.Count > 0)
        {
            return Task.FromResult(StatusQueue.Dequeue());
        }
        return Task.FromResult(NextStatus);
    }

    public ProcessStartInfo CreateStartInfo(string scriptPath, IEnumerable<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);
        ArgumentNullException.ThrowIfNull(arguments);

        var args = new List<string>();
        foreach (var a in arguments)
        {
            args.Add(a);
        }
        StartInfoRequests.Add((scriptPath, args.ToArray()));

        var psi = new ProcessStartInfo
        {
            FileName = InterpreterPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add(scriptPath);
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }
        return psi;
    }
}
