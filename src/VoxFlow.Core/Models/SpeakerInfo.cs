namespace VoxFlow.Core.Models;

/// <summary>
/// Represents one speaker in a <see cref="TranscriptDocument"/>.
/// </summary>
public sealed record SpeakerInfo(
    string Id,
    string DisplayName,
    TimeSpan TotalSpeechDuration);
