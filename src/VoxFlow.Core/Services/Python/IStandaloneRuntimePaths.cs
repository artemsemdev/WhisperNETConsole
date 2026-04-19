namespace VoxFlow.Core.Services.Python;

/// <summary>
/// On-disk layout of a bundled <c>python-build-standalone</c> tree. Injected
/// so tests can point at a temp directory instead of the real bundle path.
/// </summary>
public interface IStandaloneRuntimePaths
{
    string TreeRoot { get; }
    string InterpreterPath { get; }
    string SitePackagesPath { get; }
}
