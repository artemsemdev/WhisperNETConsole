using VoxFlow.Core.Configuration;
using Xunit;

namespace VoxFlow.Core.Tests;

public sealed class ResultFormatTests
{
    // -----------------------------------------------------------------------
    // ParseFormat — supported values (case-insensitive)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("txt", ResultFormat.Txt)]
    [InlineData("TXT", ResultFormat.Txt)]
    [InlineData("Txt", ResultFormat.Txt)]
    [InlineData("srt", ResultFormat.Srt)]
    [InlineData("SRT", ResultFormat.Srt)]
    [InlineData("vtt", ResultFormat.Vtt)]
    [InlineData("VTT", ResultFormat.Vtt)]
    [InlineData("json", ResultFormat.Json)]
    [InlineData("JSON", ResultFormat.Json)]
    [InlineData("md", ResultFormat.Md)]
    [InlineData("MD", ResultFormat.Md)]
    public void ParseFormat_SupportedValues_ReturnsCorrectFormat(string input, ResultFormat expected)
    {
        Assert.Equal(expected, ResultFormatExtensions.ParseFormat(input));
    }

    // -----------------------------------------------------------------------
    // ParseFormat — defaults to TXT when missing
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ParseFormat_MissingOrEmpty_DefaultsToTxt(string? input)
    {
        Assert.Equal(ResultFormat.Txt, ResultFormatExtensions.ParseFormat(input));
    }

    // -----------------------------------------------------------------------
    // ParseFormat — rejects unsupported values
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("pdf")]
    [InlineData("docx")]
    [InlineData("csv")]
    [InlineData("xml")]
    [InlineData("html")]
    public void ParseFormat_UnsupportedValues_ThrowsWithActionableMessage(string input)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ResultFormatExtensions.ParseFormat(input));
        Assert.Contains(input, ex.Message);
        Assert.Contains("txt, srt, vtt, json, md", ex.Message);
    }

    // -----------------------------------------------------------------------
    // ParseFormat — handles whitespace
    // -----------------------------------------------------------------------

    [Fact]
    public void ParseFormat_TrimsWhitespace()
    {
        Assert.Equal(ResultFormat.Srt, ResultFormatExtensions.ParseFormat("  srt  "));
    }

    // -----------------------------------------------------------------------
    // TryParseFormat
    // -----------------------------------------------------------------------

    [Fact]
    public void TryParseFormat_ValidValue_ReturnsFormat()
    {
        Assert.Equal(ResultFormat.Json, ResultFormatExtensions.TryParseFormat("json"));
    }

    [Fact]
    public void TryParseFormat_InvalidValue_ReturnsNull()
    {
        Assert.Null(ResultFormatExtensions.TryParseFormat("pdf"));
    }

    [Fact]
    public void TryParseFormat_NullValue_ReturnsNull()
    {
        Assert.Null(ResultFormatExtensions.TryParseFormat(null));
    }

    // -----------------------------------------------------------------------
    // ToFileExtension
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(ResultFormat.Txt, ".txt")]
    [InlineData(ResultFormat.Srt, ".srt")]
    [InlineData(ResultFormat.Vtt, ".vtt")]
    [InlineData(ResultFormat.Json, ".json")]
    [InlineData(ResultFormat.Md, ".md")]
    public void ToFileExtension_ReturnsCorrectExtension(ResultFormat format, string expected)
    {
        Assert.Equal(expected, format.ToFileExtension());
    }

    // -----------------------------------------------------------------------
    // NormalizeOutputPath
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("/output/result.txt", ResultFormat.Srt, "/output/result.srt")]
    [InlineData("/output/result.txt", ResultFormat.Txt, "/output/result.txt")]
    [InlineData("/output/result.txt", ResultFormat.Json, "/output/result.json")]
    [InlineData("/output/result.srt", ResultFormat.Vtt, "/output/result.vtt")]
    [InlineData("/output/result.md", ResultFormat.Md, "/output/result.md")]
    public void NormalizeOutputPath_ChangesExtensionToMatchFormat(string input, ResultFormat format, string expected)
    {
        Assert.Equal(expected, ResultFormatExtensions.NormalizeOutputPath(input, format));
    }

    [Fact]
    public void NormalizeOutputPath_PreservesDirectory()
    {
        var result = ResultFormatExtensions.NormalizeOutputPath("/long/path/to/file.txt", ResultFormat.Vtt);
        Assert.StartsWith("/long/path/to/", result);
        Assert.EndsWith(".vtt", result);
    }

    // -----------------------------------------------------------------------
    // Configuration integration — TranscriptionOptions loads resultFormat
    // -----------------------------------------------------------------------

    [Fact]
    public void TranscriptionOptions_DefaultsToTxt_WhenFieldIsMissing()
    {
        using var directory = new TemporaryDirectory();

        var settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: "/tmp/input.m4a",
            wavFilePath: "/tmp/output.wav",
            resultFilePath: "/tmp/result.txt",
            modelFilePath: "/tmp/model.bin",
            ffmpegExecutablePath: "ffmpeg");

        var options = TranscriptionOptions.LoadFromPath(settingsPath);

        Assert.Equal(ResultFormat.Txt, options.ResultFormat);
    }

    [Theory]
    [InlineData("srt", ResultFormat.Srt)]
    [InlineData("vtt", ResultFormat.Vtt)]
    [InlineData("json", ResultFormat.Json)]
    [InlineData("md", ResultFormat.Md)]
    [InlineData("SRT", ResultFormat.Srt)]
    public void TranscriptionOptions_ParsesConfiguredResultFormat(string configValue, ResultFormat expected)
    {
        using var directory = new TemporaryDirectory();

        var settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: "/tmp/input.m4a",
            wavFilePath: "/tmp/output.wav",
            resultFilePath: "/tmp/result.txt",
            modelFilePath: "/tmp/model.bin",
            ffmpegExecutablePath: "ffmpeg",
            resultFormat: configValue);

        var options = TranscriptionOptions.LoadFromPath(settingsPath);

        Assert.Equal(expected, options.ResultFormat);
    }

    [Fact]
    public void TranscriptionOptions_RejectsUnsupportedResultFormat()
    {
        using var directory = new TemporaryDirectory();

        var settingsPath = TestSettingsFileFactory.Write(
            directory.Path,
            inputFilePath: "/tmp/input.m4a",
            wavFilePath: "/tmp/output.wav",
            resultFilePath: "/tmp/result.txt",
            modelFilePath: "/tmp/model.bin",
            ffmpegExecutablePath: "ffmpeg",
            resultFormat: "pdf");

        var ex = Assert.Throws<InvalidOperationException>(() => TranscriptionOptions.LoadFromPath(settingsPath));
        Assert.Contains("pdf", ex.Message);
        Assert.Contains("txt, srt, vtt, json, md", ex.Message);
    }
}
