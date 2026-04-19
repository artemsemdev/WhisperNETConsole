namespace VoxFlow.Core.Models;

/// <summary>
/// Describes a batch transcription run requested by a host.
/// </summary>
public sealed record BatchTranscribeRequest(
    string InputDirectory,
    string OutputDirectory,
    string? FilePattern = null,
    string? SummaryFilePath = null,
    bool StopOnFirstError = false,
    bool KeepIntermediateFiles = false,
    string? ConfigurationPath = null,
    int? MaxFiles = null,
    bool? EnableSpeakers = null);
