namespace VoxFlow.Core.Models;

/// <summary>
/// Provenance metadata for a <see cref="TranscriptDocument"/>.
/// </summary>
public sealed record TranscriptMetadata(
    int SchemaVersion,
    string DiarizationModel,
    int SidecarVersion);
