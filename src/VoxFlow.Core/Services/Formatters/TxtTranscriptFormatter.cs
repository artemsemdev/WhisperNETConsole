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
        if (context.SpeakerTranscript is not null)
        {
            return FormatSpeakerAware(context.SpeakerTranscript);
        }

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

    private static string FormatSpeakerAware(TranscriptDocument document)
    {
        var builder = new StringBuilder();
        foreach (var turn in document.Turns)
        {
            builder.Append("Speaker ");
            builder.Append(turn.SpeakerId);
            builder.Append(": ");
            builder.AppendLine(string.Join(" ", turn.Words.Select(w => w.Text)));
        }
        return builder.ToString();
    }
}
