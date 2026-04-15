namespace VoxFlow.Core.Models;

/// <summary>
/// Progress checkpoint forwarded from the diarization sidecar's stderr stream.
/// The sidecar emits NDJSON progress lines; <see cref="PyannoteSidecarClient"/>
/// parses them and reports each as a <see cref="SpeakerLabelingProgress"/>.
/// Phase 0 only surfaces the two fields pyannote itself provides; richer UX
/// state (e.g., bytes downloaded) can be added later without a contract break.
/// </summary>
public sealed record SpeakerLabelingProgress(string Stage, double? Fraction);
