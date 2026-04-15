namespace VoxFlow.Core.Services.Python;

/// <summary>
/// Production <see cref="IVenvPaths"/> implementation. Roots the managed
/// virtual environment under the OS application-support directory
/// (<c>~/Library/Application Support/VoxFlow/python-runtime/</c> on macOS;
/// <c>%AppData%\VoxFlow\python-runtime\</c> on Windows) and points
/// <see cref="RequirementsFilePath"/> at the host-bundled requirements
/// file under <c>{AppContext.BaseDirectory}/python/python-requirements.txt</c>.
/// </summary>
public sealed class DefaultVenvPaths : IVenvPaths
{
    public DefaultVenvPaths()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Root = Path.Combine(appData, "VoxFlow", "python-runtime");
        RequirementsFilePath = Path.Combine(AppContext.BaseDirectory, "python", "python-requirements.txt");
    }

    public string Root { get; }

    public string InterpreterPath => OperatingSystem.IsWindows()
        ? Path.Combine(Root, "Scripts", "python.exe")
        : Path.Combine(Root, "bin", "python3");

    public string PipPath => OperatingSystem.IsWindows()
        ? Path.Combine(Root, "Scripts", "pip.exe")
        : Path.Combine(Root, "bin", "pip");

    public string RequirementsFilePath { get; }
}
