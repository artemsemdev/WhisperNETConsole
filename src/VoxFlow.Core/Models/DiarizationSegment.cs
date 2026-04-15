namespace VoxFlow.Core.Models;

/// <summary>
/// One speaker-homogeneous time span emitted by the diarization sidecar.
/// Speaker IDs are the ordinal labels from <see cref="DiarizationSpeaker.Id"/>.
/// </summary>
public sealed record DiarizationSegment(string Speaker, double Start, double End);
