using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VoxFlow.Core.Services.Python;
using Xunit;

namespace VoxFlow.Core.Tests.Services.Python;

public sealed class SystemPythonRuntimeTests
{
    [Fact]
    public async Task GetStatus_PythonInPath_ReturnsReady()
    {
        var launcher = new FakeProcessLauncher();
        launcher.SetResponse("python3", exitCode: 0, stdOut: "Python 3.11.5\n");

        var runtime = new SystemPythonRuntime(launcher);

        var status = await runtime.GetStatusAsync(CancellationToken.None);

        Assert.True(status.IsReady);
        Assert.Equal("3.11.5", status.Version);
        Assert.Equal("python3", status.InterpreterPath);
        Assert.Null(status.Error);
    }

    [Fact]
    public async Task GetStatus_PythonNotFound_ReturnsNotReady()
    {
        var launcher = new FakeProcessLauncher();
        launcher.SetThrow("python3", new System.ComponentModel.Win32Exception("command not found"));

        var runtime = new SystemPythonRuntime(launcher);

        var status = await runtime.GetStatusAsync(CancellationToken.None);

        Assert.False(status.IsReady);
        Assert.NotNull(status.Error);
        Assert.Null(status.InterpreterPath);
        Assert.Null(status.Version);
    }

    [Fact]
    public async Task GetStatus_PythonTooOld_ReturnsNotReady()
    {
        var launcher = new FakeProcessLauncher();
        launcher.SetResponse("python3", exitCode: 0, stdOut: "Python 3.8.10\n");

        var runtime = new SystemPythonRuntime(launcher);

        var status = await runtime.GetStatusAsync(CancellationToken.None);

        Assert.False(status.IsReady);
        Assert.NotNull(status.Error);
        Assert.Contains("3.10", status.Error);
    }

    [Fact]
    public void CreateStartInfo_ValidInputs_ProducesRunnableProcessInfo()
    {
        var runtime = new SystemPythonRuntime(new FakeProcessLauncher());

        var psi = runtime.CreateStartInfo("/tmp/voxflow_diarize.py", new[] { "--input", "file.wav" });

        Assert.Equal("python3", psi.FileName);
        Assert.True(psi.RedirectStandardInput);
        Assert.True(psi.RedirectStandardOutput);
        Assert.True(psi.RedirectStandardError);
        Assert.False(psi.UseShellExecute);
        Assert.Contains("/tmp/voxflow_diarize.py", psi.ArgumentList.ToList());
        Assert.Contains("--input", psi.ArgumentList.ToList());
        Assert.Contains("file.wav", psi.ArgumentList.ToList());
    }

    [Fact]
    public async Task GetStatus_Cancelled_ThrowsOperationCanceled()
    {
        var launcher = new FakeProcessLauncher();
        launcher.SetNeverReturns("python3");

        var runtime = new SystemPythonRuntime(launcher);
        using var cts = new CancellationTokenSource();

        var task = runtime.GetStatusAsync(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }
}
