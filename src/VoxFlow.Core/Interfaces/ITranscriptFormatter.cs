using VoxFlow.Core.Models;

namespace VoxFlow.Core.Interfaces;

/// <summary>
/// Serializes filtered transcript segments into a specific output format.
/// </summary>
public interface ITranscriptFormatter
{
    /// <summary>
    /// Builds the formatted transcript text from the supplied segments and context.
    /// </summary>
    string Format(IReadOnlyList<FilteredSegment> segments, TranscriptOutputContext context);
}
