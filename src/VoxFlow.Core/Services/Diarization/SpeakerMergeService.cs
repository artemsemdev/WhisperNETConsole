using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;
using Whisper.net;

namespace VoxFlow.Core.Services.Diarization;

/// <summary>
/// Merges Whisper word tokens and diarization segments into a
/// speaker-labeled <see cref="TranscriptDocument"/>. Pure logic: no I/O.
/// </summary>
public sealed class SpeakerMergeService : ISpeakerMergeService
{
    public TranscriptDocument Merge(
        IReadOnlyList<FilteredSegment> segments,
        DiarizationResult diarization,
        TranscriptMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(diarization);
        ArgumentNullException.ThrowIfNull(metadata);

        var flattened = FlattenTokens(segments);
        if (flattened.Count == 0)
        {
            return new TranscriptDocument(
                Speakers: Array.Empty<SpeakerInfo>(),
                Words: Array.Empty<TranscriptWord>(),
                Turns: Array.Empty<SpeakerTurn>(),
                Metadata: metadata);
        }

        var rawToOrdinal = new Dictionary<string, string>();
        var words = new List<TranscriptWord>(flattened.Count);
        foreach (var (token, segmentStart) in flattened)
        {
            var wordStart = segmentStart + TimeSpan.FromMilliseconds(token.Start * 10);
            var wordEnd = segmentStart + TimeSpan.FromMilliseconds(token.End * 10);
            var rawId = AssignSpeaker(wordStart, wordEnd, diarization.Segments);
            if (!rawToOrdinal.TryGetValue(rawId, out var ordinalId))
            {
                ordinalId = OrdinalLabel(rawToOrdinal.Count);
                rawToOrdinal[rawId] = ordinalId;
            }
            words.Add(new TranscriptWord(wordStart, wordEnd, token.Text ?? string.Empty, ordinalId));
        }

        var turns = SpeakerTurn.GroupConsecutive(words);
        var speakers = BuildRoster(turns);
        return new TranscriptDocument(speakers, words, turns, metadata);
    }

    private static string OrdinalLabel(int index)
    {
        // 0 → "A", 1 → "B", ... 25 → "Z", 26 → "AA" (defensive; real cases stay <26).
        if (index < 26)
        {
            return ((char)('A' + index)).ToString();
        }
        var first = (char)('A' + (index / 26) - 1);
        var second = (char)('A' + (index % 26));
        return string.Concat(first, second);
    }

    private static List<(WhisperToken Token, TimeSpan SegmentStart)> FlattenTokens(
        IReadOnlyList<FilteredSegment> segments)
    {
        var list = new List<(WhisperToken, TimeSpan)>();
        foreach (var segment in segments)
        {
            foreach (var token in segment.Words)
            {
                list.Add((token, segment.Start));
            }
        }
        return list;
    }

    private static string AssignSpeaker(
        TimeSpan wordStart,
        TimeSpan wordEnd,
        IReadOnlyList<DiarizationSegment> diarSegments)
    {
        if (diarSegments.Count == 0)
        {
            return "A";
        }

        var wordStartSec = wordStart.TotalSeconds;
        var wordEndSec = wordEnd.TotalSeconds;
        string? bestOverlapId = null;
        var bestOverlap = 0.0;
        string? nearestId = null;
        var nearestGap = double.PositiveInfinity;
        foreach (var seg in diarSegments)
        {
            var overlap = Math.Min(wordEndSec, seg.End) - Math.Max(wordStartSec, seg.Start);
            if (overlap > bestOverlap)
            {
                bestOverlap = overlap;
                bestOverlapId = seg.Speaker;
            }

            var gap = wordStartSec >= seg.End
                ? wordStartSec - seg.End
                : wordEndSec <= seg.Start ? seg.Start - wordEndSec : 0.0;
            if (gap < nearestGap)
            {
                nearestGap = gap;
                nearestId = seg.Speaker;
            }
        }

        return bestOverlapId ?? nearestId!;
    }

    private static IReadOnlyList<SpeakerInfo> BuildRoster(IReadOnlyList<SpeakerTurn> turns)
    {
        var order = new List<string>();
        var totals = new Dictionary<string, TimeSpan>();
        foreach (var turn in turns)
        {
            if (!totals.ContainsKey(turn.SpeakerId))
            {
                order.Add(turn.SpeakerId);
                totals[turn.SpeakerId] = TimeSpan.Zero;
            }
            totals[turn.SpeakerId] += turn.EndTime - turn.StartTime;
        }

        var roster = new List<SpeakerInfo>(order.Count);
        for (var i = 0; i < order.Count; i++)
        {
            roster.Add(new SpeakerInfo(order[i], order[i], totals[order[i]]));
        }
        return roster;
    }
}
