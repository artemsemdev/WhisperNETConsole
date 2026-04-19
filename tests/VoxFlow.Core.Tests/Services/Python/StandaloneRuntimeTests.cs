using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VoxFlow.Core.Services.Python;
using Xunit;

namespace VoxFlow.Core.Tests.Services.Python;

public sealed class StandaloneRuntimeTests : IDisposable
{
    private readonly string _treeRoot;

    public StandaloneRuntimeTests()
    {
        _treeRoot = Path.Combine(Path.GetTempPath(), $"voxflow-standalone-tests-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_treeRoot))
        {
            try { Directory.Delete(_treeRoot, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task GetStatusAsync_TreeMissing_ReturnsNotReady()
    {
        var paths = new FakeStandaloneRuntimePaths
        {
            TreeRoot = "/definitely/not/a/real/path/voxflow-standalone",
            InterpreterPath = "/definitely/not/a/real/path/voxflow-standalone/bin/python3",
            SitePackagesPath = "/definitely/not/a/real/path/voxflow-standalone/lib/python3.12/site-packages",
        };
        var runtime = new StandaloneRuntime(paths, new FakeProcessLauncher());

        var status = await runtime.GetStatusAsync(CancellationToken.None);

        Assert.False(status.IsReady);
        Assert.NotNull(status.Error);
        Assert.Contains(paths.TreeRoot, status.Error);
    }

    [Fact]
    public async Task GetStatusAsync_TreeExistsButInterpreterMissing_ReturnsNotReady()
    {
        Directory.CreateDirectory(_treeRoot);
        var interpreterPath = Path.Combine(_treeRoot, "bin", "python3");
        var paths = new FakeStandaloneRuntimePaths
        {
            TreeRoot = _treeRoot,
            InterpreterPath = interpreterPath,
            SitePackagesPath = Path.Combine(_treeRoot, "lib", "python3.12", "site-packages"),
        };
        var runtime = new StandaloneRuntime(paths, new FakeProcessLauncher());

        var status = await runtime.GetStatusAsync(CancellationToken.None);

        Assert.False(status.IsReady);
        Assert.NotNull(status.Error);
        Assert.Contains("python3", status.Error);
    }

    [Fact]
    public async Task GetStatusAsync_InterpreterPresentButVersionBelow310_ReturnsNotReady()
    {
        var paths = CreatePopulatedTree(out var interpreterPath);
        var launcher = new FakeProcessLauncher();
        launcher.SetResponse(interpreterPath, exitCode: 0, stdOut: "Python 3.9.6\n");
        var runtime = new StandaloneRuntime(paths, launcher);

        var status = await runtime.GetStatusAsync(CancellationToken.None);

        Assert.False(status.IsReady);
        Assert.NotNull(status.Error);
        Assert.Contains("3.10", status.Error);
    }

    [Fact]
    public async Task GetStatusAsync_TreeAndInterpreterAndModernVersion_ReturnsReady()
    {
        var paths = CreatePopulatedTree(out var interpreterPath);
        var launcher = new FakeProcessLauncher();
        launcher.SetResponse(interpreterPath, exitCode: 0, stdOut: "Python 3.12.2\n");
        var runtime = new StandaloneRuntime(paths, launcher);

        var status = await runtime.GetStatusAsync(CancellationToken.None);

        Assert.True(status.IsReady);
        Assert.Equal("3.12.2", status.Version);
        Assert.Equal(interpreterPath, status.InterpreterPath);
        Assert.Null(status.Error);
    }

    [Fact]
    public void CreateStartInfo_ValidInputs_PointsAtBundledInterpreter_AndSetsPythonHomeAndPythonPath()
    {
        var paths = new FakeStandaloneRuntimePaths
        {
            TreeRoot = "/bundled/python-standalone",
            InterpreterPath = "/bundled/python-standalone/bin/python3",
            SitePackagesPath = "/bundled/python-standalone/lib/python3.12/site-packages",
        };
        var runtime = new StandaloneRuntime(paths, new FakeProcessLauncher());

        var psi = runtime.CreateStartInfo("/tmp/voxflow_diarize.py", new[] { "--input", "file.wav" });

        Assert.Equal(paths.InterpreterPath, psi.FileName);
        Assert.True(psi.RedirectStandardInput);
        Assert.True(psi.RedirectStandardOutput);
        Assert.True(psi.RedirectStandardError);
        Assert.False(psi.UseShellExecute);
        Assert.Contains("/tmp/voxflow_diarize.py", psi.ArgumentList);
        Assert.Contains("--input", psi.ArgumentList);
        Assert.Contains("file.wav", psi.ArgumentList);

        Assert.Equal(paths.TreeRoot, psi.Environment["PYTHONHOME"]);
        Assert.Equal(paths.SitePackagesPath, psi.Environment["PYTHONPATH"]);
    }

    private FakeStandaloneRuntimePaths CreatePopulatedTree(out string interpreterPath)
    {
        var binDir = Path.Combine(_treeRoot, "bin");
        Directory.CreateDirectory(binDir);
        interpreterPath = Path.Combine(binDir, "python3");
        File.WriteAllText(interpreterPath, "#!/bin/sh\n# placeholder interpreter\n");
        var sitePackages = Path.Combine(_treeRoot, "lib", "python3.12", "site-packages");
        Directory.CreateDirectory(sitePackages);
        return new FakeStandaloneRuntimePaths
        {
            TreeRoot = _treeRoot,
            InterpreterPath = interpreterPath,
            SitePackagesPath = sitePackages,
        };
    }
}
