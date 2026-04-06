using System.IO;
using System.Linq;
using VoxFlow.Core.Configuration;
using Xunit;

namespace VoxFlow.Core.Tests;

public sealed class SupportedInputFormatsTests
{
    [Theory]
    [InlineData(".m4a")]
    [InlineData(".wav")]
    [InlineData(".mp3")]
    [InlineData(".aac")]
    [InlineData(".flac")]
    [InlineData(".ogg")]
    [InlineData(".aif")]
    [InlineData(".aiff")]
    [InlineData(".mp4")]
    public void IsSupported_RecognizesAllDeclaredFormats(string extension)
    {
        Assert.True(SupportedInputFormats.IsSupported($"recording{extension}"));
    }

    [Theory]
    [InlineData(".M4A")]
    [InlineData(".WAV")]
    [InlineData(".Mp3")]
    [InlineData(".FLAC")]
    [InlineData(".OGG")]
    [InlineData(".MP4")]
    public void IsSupported_IsCaseInsensitive(string extension)
    {
        Assert.True(SupportedInputFormats.IsSupported($"recording{extension}"));
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".pdf")]
    [InlineData(".doc")]
    [InlineData(".json")]
    [InlineData(".exe")]
    [InlineData("")]
    public void IsSupported_RejectsUnsupportedFormats(string extension)
    {
        Assert.False(SupportedInputFormats.IsSupported($"file{extension}"));
    }

    [Fact]
    public void IsSupported_HandlesFullPathCorrectly()
    {
        Assert.True(SupportedInputFormats.IsSupported("/users/test/recordings/meeting.mp3"));
        Assert.False(SupportedInputFormats.IsSupported("/users/test/documents/notes.txt"));
    }

    [Fact]
    public void Extensions_ContainsAllExpectedFormats()
    {
        var expected = new[] { ".m4a", ".wav", ".mp3", ".aac", ".flac", ".ogg", ".aif", ".aiff", ".mp4" };
        Assert.Equal(expected.Length, SupportedInputFormats.Extensions.Count);
        foreach (var ext in expected)
        {
            Assert.Contains(ext, SupportedInputFormats.Extensions);
        }
    }

    [Fact]
    public void GlobPatterns_MatchExtensions()
    {
        Assert.Equal(SupportedInputFormats.Extensions.Count, SupportedInputFormats.GlobPatterns.Count);
        for (var i = 0; i < SupportedInputFormats.Extensions.Count; i++)
        {
            Assert.Equal($"*{SupportedInputFormats.Extensions[i]}", SupportedInputFormats.GlobPatterns[i]);
        }
    }

    [Fact]
    public void HtmlAcceptString_ContainsAllExtensionsAndAudioWildcard()
    {
        var accept = SupportedInputFormats.HtmlAcceptString;
        foreach (var ext in SupportedInputFormats.Extensions)
        {
            Assert.Contains(ext, accept);
        }
        Assert.Contains("audio/*", accept);
    }

    [Fact]
    public void GetDisplaySummary_ReturnsUppercaseCommaSeparated()
    {
        var summary = SupportedInputFormats.GetDisplaySummary();
        Assert.Contains("M4A", summary);
        Assert.Contains("WAV", summary);
        Assert.Contains("MP3", summary);
        Assert.Contains("AAC", summary);
        Assert.Contains("FLAC", summary);
        Assert.Contains("OGG", summary);
        Assert.Contains("AIF", summary);
        Assert.Contains("AIFF", summary);
        Assert.Contains("MP4", summary);
    }
}
