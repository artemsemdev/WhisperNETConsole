using VoxFlow.Core.Services.Python;

namespace VoxFlow.Core.Tests.Services.Python;

internal sealed class FakeStandaloneRuntimePaths : IStandaloneRuntimePaths
{
    public string TreeRoot { get; init; } = "/non/existent/tree";
    public string InterpreterPath { get; init; } = "/non/existent/tree/bin/python3";
    public string SitePackagesPath { get; init; } = "/non/existent/tree/lib/python3.12/site-packages";
}
