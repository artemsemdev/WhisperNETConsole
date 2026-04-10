using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Interfaces;

/// <summary>
/// Resolves batch input files into concrete per-file work items.
/// </summary>
public interface IFileDiscoveryService
{
    /// <summary>
    /// Discovers input files for a batch run, optionally limiting the number of files that will be processed.
    /// </summary>
    /// <param name="batchOptions">Batch configuration options.</param>
    /// <param name="maxFiles">Optional maximum file count.</param>
    /// <param name="outputExtension">File extension for output files (e.g. ".txt", ".srt"). Defaults to ".txt".</param>
    IReadOnlyList<DiscoveredFile> DiscoverInputFiles(BatchOptions batchOptions, int? maxFiles = null, string outputExtension = ".txt");
}
