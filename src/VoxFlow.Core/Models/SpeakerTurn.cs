namespace VoxFlow.Core.Models;

/// <summary>
/// A contiguous run of words spoken by one speaker. Derived from
/// <see cref="TranscriptDocument.Words"/> by grouping consecutive
/// same-speaker words.
/// </summary>
public sealed record SpeakerTurn(
    string SpeakerId,
    TimeSpan StartTime,
    TimeSpan EndTime,
    IReadOnlyList<TranscriptWord> Words)
{
    /// <summary>
    /// Groups a chronologically-ordered word list into speaker turns by
    /// collapsing consecutive words that share the same speaker.
    /// </summary>
    public static IReadOnlyList<SpeakerTurn> GroupConsecutive(IReadOnlyList<TranscriptWord> words)
    {
        ArgumentNullException.ThrowIfNull(words);

        var turns = new List<SpeakerTurn>();
        if (words.Count == 0)
        {
            return turns;
        }

        var currentSpeakerId = words[0].SpeakerId;
        var currentStart = words[0].Start;
        var currentEnd = words[0].End;
        var currentWords = new List<TranscriptWord> { words[0] };

        for (var i = 1; i < words.Count; i++)
        {
            var word = words[i];
            if (word.SpeakerId == currentSpeakerId)
            {
                currentWords.Add(word);
                currentEnd = word.End;
                continue;
            }

            turns.Add(new SpeakerTurn(currentSpeakerId, currentStart, currentEnd, currentWords));
            currentSpeakerId = word.SpeakerId;
            currentStart = word.Start;
            currentEnd = word.End;
            currentWords = new List<TranscriptWord> { word };
        }

        turns.Add(new SpeakerTurn(currentSpeakerId, currentStart, currentEnd, currentWords));
        return turns;
    }
}
