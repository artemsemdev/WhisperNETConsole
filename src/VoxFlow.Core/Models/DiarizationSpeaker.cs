namespace VoxFlow.Core.Models;

/// <summary>
/// One speaker in a <see cref="DiarizationResult"/>. <paramref name="Id"/> is an
/// ordinal label (A, B, C...) in first-appearance order, matching the
/// sidecar-diarization-v1 contract.
/// </summary>
public sealed record DiarizationSpeaker(string Id, double TotalDuration);
