using VoxFlow.Core.Models;

namespace VoxFlow.Core.Interfaces;

/// <summary>
/// Merges Whisper word tokens with pyannote diarization segments into a
/// speaker-labeled <see cref="TranscriptDocument"/>. Pure function: no I/O,
/// no mutation of inputs.
/// </summary>
public interface ISpeakerMergeService
{
    TranscriptDocument Merge(
        IReadOnlyList<FilteredSegment> segments,
        DiarizationResult diarization,
        TranscriptMetadata metadata);
}
