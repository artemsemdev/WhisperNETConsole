#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Services;

/// <summary>
/// Discovers and validates input files for batch processing.
/// </summary>
internal sealed class FileDiscoveryService : IFileDiscoveryService
{
    /// <summary>
    /// Scans the configured input directory for files matching the batch file pattern.
    /// When the file pattern is the multi-format wildcard ("*"), all supported audio
    /// formats are discovered automatically using <see cref="SupportedInputFormats"/>.
    /// </summary>
    public IReadOnlyList<DiscoveredFile> DiscoverInputFiles(BatchOptions batchOptions, int? maxFiles = null, string outputExtension = ".txt")
    {
        ArgumentNullException.ThrowIfNull(batchOptions);

        if (maxFiles is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFiles), "maxFiles must be greater than zero when specified.");
        }

        if (!Directory.Exists(batchOptions.InputDirectory))
        {
            throw new InvalidOperationException($"Batch input directory not found: {batchOptions.InputDirectory}");
        }

        IEnumerable<string> matchingFiles = EnumerateMatchingFiles(batchOptions)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        if (maxFiles.HasValue)
        {
            matchingFiles = matchingFiles.Take(maxFiles.Value);
        }

        var discoveredPaths = matchingFiles.ToArray();

        if (discoveredPaths.Length == 0)
        {
            throw new InvalidOperationException(
                $"No files matching '{batchOptions.FilePattern}' found in: {batchOptions.InputDirectory}");
        }

        var discoveredFiles = new List<DiscoveredFile>(discoveredPaths.Length);

        foreach (var inputPath in discoveredPaths)
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(inputPath);
            var outputPath = Path.Combine(batchOptions.OutputDirectory, $"{fileNameWithoutExtension}{outputExtension}");
            var tempWavPath = Path.Combine(batchOptions.TempDirectory, $"{fileNameWithoutExtension}_{Guid.NewGuid():N}.wav");

            var fileInfo = new FileInfo(inputPath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                discoveredFiles.Add(new DiscoveredFile(inputPath, outputPath, tempWavPath, DiscoveryStatus.Skipped, "File is empty (0 bytes)"));
                continue;
            }

            discoveredFiles.Add(new DiscoveredFile(inputPath, outputPath, tempWavPath, DiscoveryStatus.Ready, null));
        }

        return discoveredFiles;
    }

    /// <summary>
    /// Returns matching file paths for the configured batch options.
    /// The special pattern "*" triggers multi-format discovery across all
    /// extensions defined in <see cref="SupportedInputFormats"/>.
    /// </summary>
    private static IEnumerable<string> EnumerateMatchingFiles(BatchOptions batchOptions)
    {
        if (batchOptions.FilePattern == "*")
        {
            return SupportedInputFormats.GlobPatterns
                .SelectMany(pattern => Directory.EnumerateFiles(batchOptions.InputDirectory, pattern))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        return Directory.EnumerateFiles(batchOptions.InputDirectory, batchOptions.FilePattern);
    }
}
