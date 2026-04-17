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
            // Whisper emits BPE subwords; word-initial tokens already carry
            // a leading " ". Concatenate as-is, then trim the leading space
            // on the first token of each turn.
            builder.AppendLine(string.Concat(turn.Words.Select(w => w.Text)).Trim());
        }
        return builder.ToString();
    }
}
