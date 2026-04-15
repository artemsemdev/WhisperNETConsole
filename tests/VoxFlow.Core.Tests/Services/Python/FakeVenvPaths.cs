using System;
using System.IO;
using VoxFlow.Core.Services.Python;

namespace VoxFlow.Core.Tests.Services.Python;

/// <summary>
/// Test double for <see cref="IVenvPaths"/>. Points at a unique temp directory
/// that callers can inspect, create files in, or let the runtime populate.
/// The root is NOT created on construction — tests decide when it appears.
/// </summary>
internal sealed class FakeVenvPaths : IVenvPaths, IDisposable
{
    public FakeVenvPaths()
    {
        Root = Path.Combine(Path.GetTempPath(), "voxflow-venv-tests", Guid.NewGuid().ToString("N"));
        RequirementsFilePath = Path.Combine(Root, "requirements.txt");
    }

    public string Root { get; }

    public string InterpreterPath => Path.Combine(Root, "bin", "python3");

    public string PipPath => Path.Combine(Root, "bin", "pip");

    public string RequirementsFilePath { get; }

    /// <summary>Simulates successful venv creation by materializing the interpreter file.</summary>
    public void MaterializeVenv()
    {
        Directory.CreateDirectory(Path.Combine(Root, "bin"));
        File.WriteAllText(InterpreterPath, "#!/usr/bin/env fake-python\n");
        File.WriteAllText(PipPath, "#!/usr/bin/env fake-pip\n");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
