using System.Text.Json;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Services;

/// <summary>
/// Writes a <see cref="TranscriptDocument"/> as a sidecar
/// <c>{resultPath}.voxflow.json</c> file using the voxflow-transcript-v1
/// schema. Uses a temp-file + atomic rename so a cancelled write never
/// leaves a partial artifact on disk.
/// </summary>
public sealed class VoxflowTranscriptArtifactWriter : IVoxflowTranscriptArtifactWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task WriteAsync(
        string resultPath,
        TranscriptDocument document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultPath);
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var finalPath = resultPath + ".voxflow.json";
        var tempPath = finalPath + ".tmp";

        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                await JsonSerializer.SerializeAsync(
                    stream, document, SerializerOptions, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, finalPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup; a stray .tmp is preferable to masking the original error.
        }
    }
}
