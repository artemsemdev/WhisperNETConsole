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
        var wordGroups = GroupTokensByWord(flattened);
        foreach (var group in wordGroups)
        {
            // Speaker is chosen once per whisper word, using the word's full
            // time span, so BPE subword continuations ("oxide" after " mon")
            // and attached punctuation ("," after " hello") inherit the same
            // speaker as the word's initial token. Assigning each subword
            // independently produces the "dihydrogen mon / oxide" cross-
            // speaker split observed in output.
            var (firstToken, firstSegStart) = group[0];
            var (lastToken, lastSegStart) = group[^1];
            var wordStart = firstSegStart + TimeSpan.FromMilliseconds(firstToken.Start * 10);
            var wordEnd = lastSegStart + TimeSpan.FromMilliseconds(lastToken.End * 10);
            var rawId = AssignSpeaker(wordStart, wordEnd, diarization.Segments);
            if (!rawToOrdinal.TryGetValue(rawId, out var ordinalId))
            {
                ordinalId = OrdinalLabel(rawToOrdinal.Count);
                rawToOrdinal[rawId] = ordinalId;
            }
            foreach (var (token, segStart) in group)
            {
                var tokenStart = segStart + TimeSpan.FromMilliseconds(token.Start * 10);
                var tokenEnd = segStart + TimeSpan.FromMilliseconds(token.End * 10);
                words.Add(new TranscriptWord(tokenStart, tokenEnd, token.Text ?? string.Empty, ordinalId));
            }
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
                if (IsWhisperSpecialToken(token.Text))
                    continue;
                list.Add((token, segment.Start));
            }
        }
        return list;
    }

    // Word boundaries follow whisper's BPE convention: a leading " "
    // (decoded "▁") marks a word-initial token. Tokens without a leading
    // space are subword continuations or attached punctuation and must
    // stay with the preceding word for speaker-attribution purposes;
    // otherwise the overlap lookup can split one spoken word ("monoxide"
    // → " mon" + "oxide") across a pyannote boundary.
    private static List<List<(WhisperToken Token, TimeSpan SegmentStart)>> GroupTokensByWord(
        List<(WhisperToken Token, TimeSpan SegmentStart)> flattened)
    {
        var groups = new List<List<(WhisperToken, TimeSpan)>>();
        List<(WhisperToken, TimeSpan)>? current = null;
        foreach (var item in flattened)
        {
            var text = item.Token.Text ?? string.Empty;
            var startsNewWord = current is null || (text.Length > 0 && text[0] == ' ');
            if (startsNewWord)
            {
                current = new List<(WhisperToken, TimeSpan)> { item };
                groups.Add(current);
            }
            else
            {
                current!.Add(item);
            }
        }
        return groups;
    }

    // whisper.cpp emits non-text tokens (begin-of-segment, timestamps,
    // end-of-transcript, language tags, etc.) through `whisper_token_to_str`
    // as bracketed placeholders like `[_BEG_]`, `[_TT_832]`, or `[_EOT_]`.
    // They must not surface in the per-word transcript; otherwise they show
    // up verbatim inside the speaker-labeled markdown output.
    private static bool IsWhisperSpecialToken(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.AsSpan().Trim();
        return trimmed.Length >= 3
            && trimmed[0] == '['
            && trimmed[1] == '_'
            && trimmed[^1] == ']';
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
