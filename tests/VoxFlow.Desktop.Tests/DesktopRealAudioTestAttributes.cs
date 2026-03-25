using Xunit;

namespace VoxFlow.Desktop.Tests;

internal sealed class DesktopRealAudioFactAttribute : FactAttribute
{
    public DesktopRealAudioFactAttribute()
    {
        Skip = DesktopRealAudioTestRequirements.GetSkipReason();
    }
}

internal sealed class DesktopRealAudioTheoryAttribute : TheoryAttribute
{
    public DesktopRealAudioTheoryAttribute()
    {
        Skip = DesktopRealAudioTestRequirements.GetSkipReason();
    }
}

internal static class DesktopRealAudioTestRequirements
{
    private const string OptInEnvironmentVariable = "VOXFLOW_RUN_DESKTOP_REAL_AUDIO_TESTS";

    public static string? GetSkipReason()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            return $"Set {OptInEnvironmentVariable}=1 to run desktop component tests that depend on local audio/model fixtures.";
        }

        var repositoryRoot = TryFindRepositoryRoot();
        if (repositoryRoot is null)
        {
            return "Could not locate the repository root for desktop real-audio test fixtures.";
        }

        var requiredPaths = new[]
        {
            Path.Combine(repositoryRoot, "models", "ggml-base.bin"),
            Path.Combine(repositoryRoot, "artifacts", "Input", "Test 1.m4a"),
            Path.Combine(repositoryRoot, "artifacts", "Input", "Test 2.m4a")
        };

        var missingPath = requiredPaths.FirstOrDefault(path => !File.Exists(path));
        if (missingPath is not null)
        {
            return $"Desktop real-audio test fixture is missing: {missingPath}";
        }

        return null;
    }

    private static string? TryFindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VoxFlow.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
