using System;
using System.Collections.Generic;
using System.Linq;
using VoxFlow.Core.Models;
using Xunit;

namespace VoxFlow.Core.Tests.Models;

public sealed class SpeakerTurnTests
{
    [Fact]
    public void GroupConsecutive_TwoSpeakers_ProducesTwoTurns()
    {
        var words = new[]
        {
            Word(0, 1, "Hello", "A"),
            Word(1, 2, "there", "A"),
            Word(2, 3, "friend", "A"),
            Word(3, 4, "Hi", "B"),
            Word(4, 5, "to", "B"),
            Word(5, 6, "you", "B")
        };

        var turns = SpeakerTurn.GroupConsecutive(words);

        Assert.Equal(2, turns.Count);
        Assert.Equal("A", turns[0].SpeakerId);
        Assert.Equal(TimeSpan.FromSeconds(0), turns[0].StartTime);
        Assert.Equal(TimeSpan.FromSeconds(3), turns[0].EndTime);
        Assert.Equal(3, turns[0].Words.Count);
        Assert.Equal("B", turns[1].SpeakerId);
        Assert.Equal(TimeSpan.FromSeconds(3), turns[1].StartTime);
        Assert.Equal(TimeSpan.FromSeconds(6), turns[1].EndTime);
        Assert.Equal(3, turns[1].Words.Count);
    }

    [Fact]
    public void GroupConsecutive_AlternatingSpeakers_ProducesOneTurnPerWord()
    {
        var words = new[]
        {
            Word(0, 1, "one", "A"),
            Word(1, 2, "two", "B"),
            Word(2, 3, "three", "A"),
            Word(3, 4, "four", "B"),
            Word(4, 5, "five", "A"),
            Word(5, 6, "six", "B")
        };

        var turns = SpeakerTurn.GroupConsecutive(words);

        Assert.Equal(6, turns.Count);
        for (var i = 0; i < turns.Count; i++)
        {
            var expectedSpeaker = i % 2 == 0 ? "A" : "B";
            Assert.Equal(expectedSpeaker, turns[i].SpeakerId);
            Assert.Single(turns[i].Words);
            Assert.Equal(TimeSpan.FromSeconds(i), turns[i].StartTime);
            Assert.Equal(TimeSpan.FromSeconds(i + 1), turns[i].EndTime);
        }
    }

    [Fact]
    public void GroupConsecutive_SingleSpeaker_ProducesOneTurn()
    {
        var words = new[]
        {
            Word(0, 1, "just", "A"),
            Word(1, 2, "one", "A"),
            Word(2, 3, "speaker", "A")
        };

        var turns = SpeakerTurn.GroupConsecutive(words);

        var only = Assert.Single(turns);
        Assert.Equal("A", only.SpeakerId);
        Assert.Equal(TimeSpan.FromSeconds(0), only.StartTime);
        Assert.Equal(TimeSpan.FromSeconds(3), only.EndTime);
        Assert.Equal(3, only.Words.Count);
    }

    [Fact]
    public void GroupConsecutive_EmptyInput_ProducesEmptyList()
    {
        var turns = SpeakerTurn.GroupConsecutive(Array.Empty<TranscriptWord>());

        Assert.Empty(turns);
    }

    private static TranscriptWord Word(double startSec, double endSec, string text, string speakerId)
    {
        return new TranscriptWord(
            TimeSpan.FromSeconds(startSec),
            TimeSpan.FromSeconds(endSec),
            text,
            speakerId);
    }
}
