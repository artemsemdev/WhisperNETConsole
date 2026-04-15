using System.Collections.Generic;
using VoxFlow.Core.Services.Python;
using Xunit;

namespace VoxFlow.Core.Tests.Services.Python;

/// <summary>
/// Covers the <see cref="ManagedVenvBootstrapper"/> adapter that exposes
/// <see cref="ManagedVenvRuntime.CreateVenvAsync"/> through the
/// <c>IManagedVenvBootstrapper</c> interface consumed by the enrichment
/// orchestrator.
/// </summary>
public sealed class ManagedVenvBootstrapperTests
{
    [Fact]
    public async Task BootstrapAsync_InvokesVenvCreationAndPipInstall_ForwardsProgress()
    {
        using var paths = new FakeVenvPaths();
        var launcher = new FakeProcessLauncher();
        launcher.SetResponse("python3", exitCode: 0, stdOut: string.Empty);
        launcher.SetResponseFromStdin(paths.PipPath, _ =>
        {
            paths.MaterializeVenv();
            return new ProcessExecutionResult(0, string.Empty, string.Empty);
        });
        var runtime = new ManagedVenvRuntime(launcher, paths);
        var bootstrapper = new ManagedVenvBootstrapper(runtime);
        var stages = new List<VenvBootstrapStage>();
        var progress = new Progress<VenvBootstrapStage>(stages.Add);

        await bootstrapper.BootstrapAsync(progress, CancellationToken.None);

        await Task.Delay(50);
        Assert.Contains(VenvBootstrapStage.Complete, stages);
        Assert.Contains(launcher.Invocations, i => i.FileName == "python3");
        Assert.Contains(launcher.Invocations, i => i.FileName == paths.PipPath);
    }
}
