#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VoxFlow.Core.Configuration;

/// <summary>
/// Defines the audio file formats that the transcription pipeline can accept as input.
/// All listed formats are convertible to WAV via ffmpeg before Whisper inference.
/// </summary>
public static class SupportedInputFormats
{
    /// <summary>
    /// File extensions (with leading dot, lowercase) accepted by the pipeline.
    /// </summary>
    public static IReadOnlyList<string> Extensions { get; } = new[]
    {
        ".m4a",
        ".wav",
        ".mp3",
        ".aac",
        ".flac",
        ".ogg",
        ".aif",
        ".aiff",
        ".mp4"
    };

    /// <summary>
    /// Glob patterns matching all supported extensions, suitable for batch file discovery.
    /// </summary>
    public static IReadOnlyList<string> GlobPatterns { get; } =
        Extensions.Select(ext => $"*{ext}").ToArray();

    /// <summary>
    /// Comma-separated accept string for HTML file inputs (e.g. ".m4a,.wav,.mp3").
    /// </summary>
    public static string HtmlAcceptString { get; } =
        string.Join(",", Extensions) + ",audio/*";

    private static readonly HashSet<string> ExtensionSet =
        new(Extensions, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true when the file extension is a recognized input format.
    /// </summary>
    public static bool IsSupported(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(extension) && ExtensionSet.Contains(extension);
    }

    /// <summary>
    /// Returns a human-readable summary of all supported formats.
    /// </summary>
    public static string GetDisplaySummary()
    {
        return string.Join(", ", Extensions.Select(ext => ext.TrimStart('.').ToUpperInvariant()));
    }
}
