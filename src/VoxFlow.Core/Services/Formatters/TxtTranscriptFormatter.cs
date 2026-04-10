using System.Text;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Services.Formatters;

/// <summary>
/// Formats transcript segments in the legacy timestamped text format.
/// This preserves exact backward compatibility with the original output.
/// </summary>
internal sealed class TxtTranscriptFormatter : ITranscriptFormatter
{
    public string Format(IReadOnlyList<FilteredSegment> segments, TranscriptOutputContext context)
    {
        var builder = new StringBuilder();

        foreach (var segment in segments)
        {
            builder.Append(segment.Start);
            builder.Append("->");
            builder.Append(segment.End);
            builder.Append(": ");
            builder.AppendLine(segment.Text);
        }

        return builder.ToString();
    }
}
