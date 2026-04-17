using System.Globalization;
using System.Text;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Services.Formatters;

/// <summary>
/// Formats transcript segments as SubRip (SRT) subtitles.
/// Uses HH:mm:ss,mmm timestamps with --> separator.
/// </summary>
internal sealed class SrtTranscriptFormatter : ITranscriptFormatter
{
    public string Format(IReadOnlyList<FilteredSegment> segments, TranscriptOutputContext context)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < segments.Count; i++)
        {
            if (i > 0)
                builder.AppendLine();

            var segment = segments[i];
            builder.AppendLine((i + 1).ToString(CultureInfo.InvariantCulture));
            builder.Append(FormatSrtTimestamp(segment.Start));
            builder.Append(" --> ");
            builder.AppendLine(FormatSrtTimestamp(segment.End));

            var text = segment.Text.Trim();
            if (context.SpeakerTranscript is { } document
                && SpeakerSegmentMapper.ResolveSpeakerId(segment, document) is { } speakerId)
            {
                builder.Append("Speaker ");
                builder.Append(speakerId);
                builder.Append(": ");
            }
            builder.AppendLine(text);
        }

        return builder.ToString();
    }

    internal static string FormatSrtTimestamp(TimeSpan ts)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:D2}:{1:D2}:{2:D2},{3:D3}",
            (int)ts.TotalHours,
            ts.Minutes,
            ts.Seconds,
            ts.Milliseconds);
    }
}
