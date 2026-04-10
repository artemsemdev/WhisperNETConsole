using VoxFlow.Core.Models;

namespace VoxFlow.Core.Interfaces;

/// <summary>
/// Writes accepted transcript segments to the final output representation.
/// </summary>
public interface IOutputWriter
{
    /// <summary>
    /// Persists the transcript to disk using the format specified in the context.
    /// </summary>
    Task WriteAsync(
        string outputPath,
        IReadOnlyList<FilteredSegment> segments,
        TranscriptOutputContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the text representation of the supplied transcript segments without writing to disk.
    /// </summary>
    string BuildOutputText(IReadOnlyList<FilteredSegment> segments, TranscriptOutputContext context);
}
