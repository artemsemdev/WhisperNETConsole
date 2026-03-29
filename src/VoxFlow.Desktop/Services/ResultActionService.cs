using System.Diagnostics;

namespace VoxFlow.Desktop.Services;

public interface IResultActionService
{
    Task CopyTextAsync(string text, CancellationToken cancellationToken = default);

    Task OpenResultFolderAsync(string resultFilePath, CancellationToken cancellationToken = default);
}

public sealed class ResultActionService : IResultActionService
{
    public Task CopyTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Transcript text is unavailable.");
        }

        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Clipboard.Default.SetTextAsync(text);
            return true;
        });
    }

    public async Task OpenResultFolderAsync(string resultFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resultFilePath))
        {
            throw new InvalidOperationException("Result location is unavailable.");
        }

        var fullResultPath = Path.GetFullPath(resultFilePath);
        var directory = Path.GetDirectoryName(fullResultPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new InvalidOperationException("Result folder is unavailable.");
        }

        using var process = Process.Start(CreateOpenFolderProcessStartInfo(directory))
            ?? throw new InvalidOperationException("Could not start Finder.");

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode == 0)
        {
            return;
        }

        var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var detail = string.IsNullOrWhiteSpace(error) ? output : error;

        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(detail)
                ? $"Finder exited with code {process.ExitCode}."
                : detail.Trim());
    }

    private static ProcessStartInfo CreateOpenFolderProcessStartInfo(string directory)
    {
        var startInfo = new ProcessStartInfo("/usr/bin/open")
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        startInfo.ArgumentList.Add(directory);
        return startInfo;
    }
}
