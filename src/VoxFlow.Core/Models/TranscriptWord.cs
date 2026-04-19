namespace VoxFlow.Core.Models;

/// <summary>
/// One word (or sub-word token) inside a <see cref="TranscriptDocument"/>,
/// assigned to a speaker via <see cref="SpeakerId"/>.
/// </summary>
public sealed record TranscriptWord(
    TimeSpan Start,
    TimeSpan End,
    string Text,
    string SpeakerId);
