namespace VoxFlow.Core.Services.Python;

/// <summary>
/// Production <see cref="IStandaloneRuntimePaths"/>. Locates the bundled
/// <c>python-build-standalone</c> tree under
/// <c>{AppContext.BaseDirectory}/python-standalone/</c>. The standalone
/// tarballs use CPython's usual <c>bin/python3</c> + <c>lib/pythonX.Y/site-packages</c>
/// layout, so <see cref="SitePackagesPath"/> probes for the first
/// <c>python3.*</c> directory and falls back to a placeholder when the
/// bundle is not yet materialized (<see cref="StandaloneRuntime.GetStatusAsync"/>
/// catches that case).
/// </summary>
public sealed class DefaultStandaloneRuntimePaths : IStandaloneRuntimePaths
{
    public DefaultStandaloneRuntimePaths()
    {
        TreeRoot = Path.Combine(AppContext.BaseDirectory, "python-standalone");
        InterpreterPath = OperatingSystem.IsWindows()
            ? Path.Combine(TreeRoot, "python.exe")
            : Path.Combine(TreeRoot, "bin", "python3");
        SitePackagesPath = ResolveSitePackages(TreeRoot);
    }

    public string TreeRoot { get; }
    public string InterpreterPath { get; }
    public string SitePackagesPath { get; }

    private static string ResolveSitePackages(string treeRoot)
    {
        var libDir = Path.Combine(treeRoot, "lib");
        if (Directory.Exists(libDir))
        {
            foreach (var candidate in Directory.EnumerateDirectories(libDir, "python3.*"))
            {
                return Path.Combine(candidate, "site-packages");
            }
        }
        return Path.Combine(libDir, "python3", "site-packages");
    }
}
