using System;
using System.Linq;
using System.Reflection;
using VoxFlow.Core.Services;
using Whisper.net;
using Xunit;

namespace VoxFlow.Core.Tests.Services;

public sealed class LanguageSelectionServiceTests
{
    [Fact]
    public void ConfigureBuilderForTranscription_EnablesTokenTimestamps()
    {
        // Per-token timestamps are required for SpeakerMergeService to
        // attribute words correctly when a single whisper segment spans a
        // speaker boundary. Without WithTokenTimestamps(), whisper.net reports
        // every token at the segment's coarse bounds (Start=0, End=segment
        // end), so the overlap-based diarization lookup picks one speaker for
        // the entire segment. That's the exact cause of the stacked turn
        // merges observed in markdown output (e.g. the host's short intro
        // getting glued onto the guest's long monologue).
        var builder = CreateStubBuilder();

        LanguageSelectionService.ConfigureBuilderForTranscription(
            builder,
            languageCode: "en",
            noSpeechThreshold: 0.6f,
            logProbThreshold: -1.0f,
            entropyThreshold: 2.4f,
            useNoContext: false);

        Assert.True((bool?)ReadOption(builder, "UseTokenTimestamps"));
    }

    [Fact]
    public void ConfigureBuilderForTranscription_AppliesCoreThresholdsAndProbabilities()
    {
        // Regression guard: the extracted helper must preserve the existing
        // builder chain (language, probabilities, and the three quality
        // thresholds) so that nothing is dropped when WithTokenTimestamps is
        // added alongside them.
        var builder = CreateStubBuilder();

        LanguageSelectionService.ConfigureBuilderForTranscription(
            builder,
            languageCode: "ru",
            noSpeechThreshold: 0.5f,
            logProbThreshold: -0.8f,
            entropyThreshold: 2.1f,
            useNoContext: true);

        Assert.Equal("ru", (string?)ReadOption(builder, "Language"));
        Assert.True((bool?)ReadOption(builder, "ComputeProbabilities"));
        Assert.Equal(0.5f, (float?)ReadOption(builder, "NoSpeechThreshold"));
        Assert.Equal(-0.8f, (float?)ReadOption(builder, "LogProbThreshold"));
        Assert.Equal(2.1f, (float?)ReadOption(builder, "EntropyThreshold"));
        Assert.True((bool?)ReadOption(builder, "NoContext"));
    }

    private static WhisperProcessorBuilder CreateStubBuilder()
    {
        // WhisperProcessorBuilder has only an internal ctor that takes a
        // native whisper handle plus a pool; passing null/IntPtr.Zero is safe
        // because we never call Build() -- we only inspect the options the
        // builder accumulates as WithX() methods are invoked.
        var ctor = typeof(WhisperProcessorBuilder)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        return (WhisperProcessorBuilder)ctor.Invoke(new object?[] { IntPtr.Zero, null, null });
    }

    private static object? ReadOption(WhisperProcessorBuilder builder, string propertyName)
    {
        var optsField = typeof(WhisperProcessorBuilder).GetField(
            "whisperProcessorOptions",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var opts = optsField.GetValue(builder);
        return opts!.GetType().GetProperty(propertyName)!.GetValue(opts);
    }
}
