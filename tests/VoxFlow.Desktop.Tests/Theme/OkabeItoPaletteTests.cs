using VoxFlow.Core.Models;
using VoxFlow.Desktop.Theme;
using Xunit;

namespace VoxFlow.Desktop.Tests.Theme;

public sealed class OkabeItoPaletteTests
{
    private static SpeakerInfo Speaker(string label) =>
        new(Id: label, DisplayName: label, TotalSpeechDuration: TimeSpan.Zero);

    [Fact]
    public void ColorForSpeaker_FirstEightSpeakers_ReturnsDistinctColors()
    {
        var labels = new[] { "A", "B", "C", "D", "E", "F", "G", "H" };
        var colors = labels.Select(l => OkabeItoPalette.ColorForSpeaker(Speaker(l))).ToArray();

        Assert.Equal(8, colors.Distinct().Count());
        foreach (var c in colors)
        {
            Assert.Matches("^#[0-9A-Fa-f]{6}$", c);
        }
    }

    [Fact]
    public void ColorForSpeaker_NinthSpeaker_WrapsToFirstColor()
    {
        var first = OkabeItoPalette.ColorForSpeaker(Speaker("A"));
        var ninth = OkabeItoPalette.ColorForSpeaker(Speaker("I"));

        Assert.Equal(first, ninth);
    }

    [Fact]
    public void ColorForSpeaker_InvalidLabel_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => OkabeItoPalette.ColorForSpeaker(Speaker("zz")));
        Assert.Throws<ArgumentException>(() => OkabeItoPalette.ColorForSpeaker(Speaker("")));
        Assert.Throws<ArgumentException>(() => OkabeItoPalette.ColorForSpeaker(Speaker("1")));
    }

    [Fact]
    public void ColorForSpeaker_KnownPaletteValues_MatchOkabeIto()
    {
        Assert.Equal("#E69F00", OkabeItoPalette.ColorForSpeaker(Speaker("A")));
        Assert.Equal("#56B4E9", OkabeItoPalette.ColorForSpeaker(Speaker("B")));
        Assert.Equal("#000000", OkabeItoPalette.ColorForSpeaker(Speaker("H")));
    }
}
