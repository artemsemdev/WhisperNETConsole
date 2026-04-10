namespace VoxFlow.Core.Configuration;

/// <summary>
/// Supported transcript output formats.
/// </summary>
public enum ResultFormat
{
    /// <summary>Legacy timestamped text (default).</summary>
    Txt,

    /// <summary>SubRip subtitle format.</summary>
    Srt,

    /// <summary>WebVTT subtitle format.</summary>
    Vtt,

    /// <summary>Structured JSON output.</summary>
    Json,

    /// <summary>Human-readable Markdown.</summary>
    Md
}

/// <summary>
/// Extension methods for <see cref="ResultFormat"/>.
/// </summary>
public static class ResultFormatExtensions
{
    /// <summary>
    /// Returns the file extension (including the leading dot) for the given format.
    /// </summary>
    public static string ToFileExtension(this ResultFormat format) => format switch
    {
        ResultFormat.Txt => ".txt",
        ResultFormat.Srt => ".srt",
        ResultFormat.Vtt => ".vtt",
        ResultFormat.Json => ".json",
        ResultFormat.Md => ".md",
        _ => ".txt"
    };

    /// <summary>
    /// Parses a format string (case-insensitive) into a <see cref="ResultFormat"/>.
    /// Returns null if the value is not recognized.
    /// </summary>
    public static ResultFormat? TryParseFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToLowerInvariant() switch
        {
            "txt" => ResultFormat.Txt,
            "srt" => ResultFormat.Srt,
            "vtt" => ResultFormat.Vtt,
            "json" => ResultFormat.Json,
            "md" => ResultFormat.Md,
            _ => null
        };
    }

    /// <summary>
    /// Parses a format string (case-insensitive) into a <see cref="ResultFormat"/>.
    /// Throws <see cref="InvalidOperationException"/> if the value is not recognized.
    /// </summary>
    public static ResultFormat ParseFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ResultFormat.Txt;

        return TryParseFormat(value)
            ?? throw new InvalidOperationException(
                $"Unsupported result format '{value.Trim()}'. Allowed values are: txt, srt, vtt, json, md.");
    }

    /// <summary>
    /// Normalizes an output file path so its extension matches the selected format.
    /// </summary>
    public static string NormalizeOutputPath(string outputPath, ResultFormat format)
    {
        var directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(outputPath);
        return Path.Combine(directory, stem + format.ToFileExtension());
    }
}
