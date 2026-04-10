using System.Globalization;
using System.Text;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Services.Formatters;

/// <summary>
/// Formats transcript segments as WebVTT subtitles.
/// Includes the WEBVTT header and uses HH:mm:ss.mmm timestamps with --> separator.
/// </summary>
internal sealed class VttTranscriptFormatter : ITranscriptFormatter
{
    public string Format(IReadOnlyList<FilteredSegment> segments, TranscriptOutputContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("WEBVTT");
        builder.AppendLine();

        for (var i = 0; i < segments.Count; i++)
        {
            if (i > 0)
                builder.AppendLine();

            var segment = segments[i];
            builder.Append(FormatVttTimestamp(segment.Start));
            builder.Append(" --> ");
            builder.AppendLine(FormatVttTimestamp(segment.End));
            builder.AppendLine(segment.Text.Trim());
        }

        return builder.ToString();
    }

    internal static string FormatVttTimestamp(TimeSpan ts)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:D2}:{1:D2}:{2:D2}.{3:D3}",
            (int)ts.TotalHours,
            ts.Minutes,
            ts.Seconds,
            ts.Milliseconds);
    }
}
