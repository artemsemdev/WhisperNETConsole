namespace VoxFlow.Core.Services.Python;

/// <summary>
/// Result of running a child process: exit code plus captured stdout/stderr.
/// </summary>
public sealed record ProcessExecutionResult(int ExitCode, string StdOut, string StdErr);
