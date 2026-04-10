using System.Globalization;
using System.Text;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Services.Formatters;

/// <summary>
/// Formats transcript segments as human-readable Markdown.
/// Includes a metadata header and timestamped transcript entries.
/// </summary>
internal sealed class MdTranscriptFormatter : ITranscriptFormatter
{
    public string Format(IReadOnlyList<FilteredSegment> segments, TranscriptOutputContext context)
    {
        var builder = new StringBuilder();

        builder.AppendLine("# Transcript");
        builder.AppendLine();

        // Metadata block
        if (context.DetectedLanguage is not null || context.AcceptedSegmentCount > 0)
        {
            if (context.DetectedLanguage is not null)
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- **Language:** {context.DetectedLanguage}"));
            builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- **Segments:** {context.AcceptedSegmentCount} accepted, {context.SkippedSegmentCount} skipped"));

            if (context.Warnings is { Count: > 0 })
            {
                builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- **Warnings:** {string.Join("; ", context.Warnings)}"));
            }

            builder.AppendLine();
        }

        builder.AppendLine("---");
        builder.AppendLine();

        foreach (var segment in segments)
        {
            var timestamp = FormatTimestamp(segment.Start);
            builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"**[{timestamp}]** {segment.Text.Trim()}"));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string FormatTimestamp(TimeSpan ts)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:D2}:{1:D2}:{2:D2}",
            (int)ts.TotalHours,
            ts.Minutes,
            ts.Seconds);
    }
}
