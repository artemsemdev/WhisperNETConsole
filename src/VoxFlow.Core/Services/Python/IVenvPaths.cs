namespace VoxFlow.Core.Services.Python;

/// <summary>
/// Abstraction over the on-disk layout of a managed Python virtual environment.
/// Injected so tests can point at a temp directory instead of the real
/// app-support location.
/// </summary>
public interface IVenvPaths
{
    string Root { get; }
    string InterpreterPath { get; }
    string PipPath { get; }
    string RequirementsFilePath { get; }
}
