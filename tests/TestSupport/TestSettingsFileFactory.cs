using System.IO;
using System.Linq;
using System.Text.Json;

internal static class TestSettingsFileFactory
{
    public static string Write(
        string directoryPath,
        string inputFilePath,
        string wavFilePath,
        string resultFilePath,
        string modelFilePath,
        string ffmpegExecutablePath,
        (string Code, string DisplayName)[]? supportedLanguages = null,
        string[]? nonSpeechMarkers = null,
        object? startupValidation = null,
        string modelType = "Base",
        int outputSampleRate = 16000,
        int outputChannelCount = 1,
        string outputContainerFormat = "wav",
        bool overwriteWavOutput = true,
        string[]? audioFilterChain = null,
        int longLowInformationSegmentThresholdSeconds = 30,
        int minTextLengthForLongSegment = 10,
        float minSegmentProbability = 0.35f,
        float minWinningCandidateProbability = 0.45f,
        float minWinningMargin = 0.02f,
        float tieBreakerEpsilon = 0.0001f,
        bool rejectAmbiguousLanguageCandidates = false,
        int minAcceptedSpeechDurationSeconds = 2,
        bool useNoContext = true,
        float noSpeechThreshold = 0.75f,
        float logProbThreshold = -0.8f,
        float entropyThreshold = 2.4f,
        bool suppressBracketedNonSpeechSegments = true,
        int maxConsecutiveDuplicateSegments = 2,
        int maxDuplicateSegmentTextLength = 32)
    {
        supportedLanguages ??=
        [
            ("en", "English"),
            ("ru", "Russian"),
            ("de", "German"),
            ("uk", "Ukrainian")
        ];

        nonSpeechMarkers ??= ["music", "noise", "silence"];
        audioFilterChain ??=
        [
            "afftdn=nf=-25",
            "silenceremove=stop_periods=-1:stop_threshold=-50dB:stop_duration=1"
        ];

        startupValidation ??= new
        {
            enabled = true,
            printDetailedReport = true,
            checkInputFile = true,
            checkOutputDirectories = true,
            checkOutputWriteAccess = true,
            checkFfmpegAvailability = true,
            checkModelType = true,
            checkModelDirectory = true,
            checkModelLoadability = true,
            checkLanguageSupport = false,
            checkWhisperRuntime = false
        };

        var consoleProgress = new
        {
            enabled = true,
            useColors = false,
            progressBarWidth = 20,
            refreshIntervalMilliseconds = 1
        };

        var configuration = new
        {
            transcription = new
            {
                inputFilePath,
                wavFilePath,
                resultFilePath,
                modelFilePath,
                modelType,
                ffmpegExecutablePath,
                outputSampleRate,
                outputChannelCount,
                outputContainerFormat,
                overwriteWavOutput,
                audioFilterChain,
                supportedLanguages = supportedLanguages.Select(language => new
                {
                    code = language.Code,
                    displayName = language.DisplayName
                }),
                nonSpeechMarkers,
                longLowInformationSegmentThresholdSeconds,
                minTextLengthForLongSegment,
                minSegmentProbability,
                minWinningCandidateProbability,
                minWinningMargin,
                tieBreakerEpsilon,
                rejectAmbiguousLanguageCandidates,
                minAcceptedSpeechDurationSeconds,
                useNoContext,
                noSpeechThreshold,
                logProbThreshold,
                entropyThreshold,
                suppressBracketedNonSpeechSegments,
                maxConsecutiveDuplicateSegments,
                maxDuplicateSegmentTextLength,
                startupValidation,
                consoleProgress
            }
        };

        var settingsPath = Path.Combine(directoryPath, "appsettings.json");
        File.WriteAllText(
            settingsPath,
            JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true }));

        return settingsPath;
    }
}
