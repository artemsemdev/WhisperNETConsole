using System.Collections.Generic;

namespace VoxFlow.Core.Models;

/// <summary>
/// Output of <c>IDiarizationSidecar.DiarizeAsync</c>: one speaker roster plus
/// the per-speaker time spans.
/// </summary>
public sealed record DiarizationResult(
    int Version,
    IReadOnlyList<DiarizationSpeaker> Speakers,
    IReadOnlyList<DiarizationSegment> Segments);
