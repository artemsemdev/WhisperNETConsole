namespace VoxFlow.Core.Models;

/// <summary>
/// Describes a single-file transcription request.
/// </summary>
public sealed record TranscribeFileRequest(
    string InputPath,
    string? ResultFilePath = null,
    string? ConfigurationPath = null,
    IReadOnlyList<string>? ForceLanguages = null,
    bool OverwriteExistingResult = true,
    bool? EnableSpeakers = null);
