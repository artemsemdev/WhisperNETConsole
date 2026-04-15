using VoxFlow.Core.Models;

namespace VoxFlow.Core.Services.Formatters;

/// <summary>
/// Maps a <see cref="FilteredSegment"/> to the <see cref="SpeakerTurn"/> it
/// overlaps most, so subtitle formatters (SRT/VTT) that keep their cue
/// boundaries segment-based can still attach a single speaker label per cue.
/// </summary>
internal static class SpeakerSegmentMapper
{
    public static string? ResolveSpeakerId(FilteredSegment segment, TranscriptDocument document)
    {
        SpeakerTurn? best = null;
        var bestOverlap = TimeSpan.Zero;

        foreach (var turn in document.Turns)
        {
            var start = turn.StartTime > segment.Start ? turn.StartTime : segment.Start;
            var end = turn.EndTime < segment.End ? turn.EndTime : segment.End;
            var overlap = end - start;
            if (overlap > bestOverlap)
            {
                bestOverlap = overlap;
                best = turn;
            }
        }

        return best?.SpeakerId;
    }
}
