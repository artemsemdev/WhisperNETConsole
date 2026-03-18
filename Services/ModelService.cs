using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;

/// <summary>
/// Loads, validates, and downloads Whisper models used by the application.
/// </summary>
internal static class ModelService
{
    /// <summary>
    /// Creates a Whisper factory from the configured model, downloading the model if needed.
    /// </summary>
    public static async Task<WhisperFactory> CreateFactoryAsync(
        TranscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var modelType = ParseModelType(options.ModelType);

        // Prefer reuse because model download is large, slow, and unnecessary when
        // the configured file already exists and can be loaded successfully.
        if (TryCreateFactory(options.ModelFilePath, out var whisperFactory, out _))
        {
            Console.WriteLine($"Model reused: {options.ModelFilePath}");
            return whisperFactory;
        }

        if (File.Exists(options.ModelFilePath))
        {
            Console.WriteLine($"Existing model is invalid or incomplete, re-downloading: {options.ModelFilePath}");
        }

        Console.WriteLine($"Model download started: {options.ModelFilePath}");
        await DownloadModelAsync(options.ModelFilePath, modelType, cancellationToken).ConfigureAwait(false);

        if (TryCreateFactory(options.ModelFilePath, out whisperFactory, out var error))
        {
            Console.WriteLine("Model download completed.");
            return whisperFactory;
        }

        throw new InvalidOperationException(
            $"Model download completed but the model could not be loaded: {error}");
    }

    /// <summary>
    /// Parses the configured model type into the Whisper.net enum used by the downloader.
    /// </summary>
    public static GgmlType ParseModelType(string modelType)
    {
        if (Enum.TryParse<GgmlType>(modelType, ignoreCase: true, out var parsedModelType))
        {
            return parsedModelType;
        }

        throw new InvalidOperationException($"Unsupported model type configured: {modelType}");
    }

    /// <summary>
    /// Attempts to create a Whisper factory without downloading any model data.
    /// </summary>
    private static bool TryCreateFactory(string modelFilePath, out WhisperFactory whisperFactory, out string error)
    {
        whisperFactory = null!;
        error = string.Empty;

        try
        {
            var fileInfo = new FileInfo(modelFilePath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                error = "Model file is missing or empty.";
                return false;
            }

            whisperFactory = WhisperFactory.FromPath(modelFilePath);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            whisperFactory?.Dispose();
            whisperFactory = null!;
            return false;
        }
    }

    /// <summary>
    /// Downloads the configured model to a temporary file and then replaces the target file atomically.
    /// </summary>
    private static async Task DownloadModelAsync(
        string modelFilePath,
        GgmlType ggmlType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(Path.GetFullPath(modelFilePath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryFilePath = modelFilePath + ".download";

        try
        {
            using var modelStream = await WhisperGgmlDownloader.Default
                .GetGgmlModelAsync(ggmlType, QuantizationType.NoQuantization)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            await using (var fileWriter = File.Create(temporaryFilePath))
            {
                // Write to a temporary file first so cancellation or partial downloads
                // never leave the configured model path in a corrupted state.
                await modelStream.CopyToAsync(fileWriter, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryFilePath, modelFilePath, overwrite: true);
        }
        catch (Exception)
        {
            Console.WriteLine("Model download failed.");
            throw;
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
    }
}
