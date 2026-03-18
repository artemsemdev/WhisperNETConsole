#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;

/// <summary>
/// Runs candidate transcriptions and selects the best supported language.
/// </summary>
internal static class LanguageSelectionService
{
    /// <summary>
    /// Transcribes the same audio for each configured language and selects the highest-scoring result.
    /// </summary>
    public static async Task<CandidateTranscriptionResult> SelectBestCandidateAsync(
        WhisperFactory whisperFactory,
        float[] audioSamples,
        TranscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var candidates = new List<CandidateTranscriptionResult>(options.SupportedLanguages.Count);
        var progressService = new ConsoleProgressService(options.ConsoleProgress);
        progressService.StartTranscription(options.SupportedLanguages.Count);

        // Reuse a single processor across candidate passes to avoid native teardown instability.
        var processor = CreateProcessor(whisperFactory, options, progressService);

        if (options.SupportedLanguages.Count == 1)
        {
            var language = options.SupportedLanguages[0];
            progressService.StartLanguage(0, language.DisplayName);

            // The single-language path avoids extra passes and skips ambiguity handling entirely.
            var singleCandidate = await TranscribeCandidateAsync(
                    processor,
                    audioSamples,
                    language,
                    options,
                    cancellationToken)
                .ConfigureAwait(false);

            progressService.CompleteLanguage(
                $"Finished {language.DisplayName}: accepted segments {singleCandidate.AcceptedSegments.Count}");

            Console.WriteLine(
                $"Single-language mode: forced {language.DisplayName} ({language.Code}), score={singleCandidate.Score:F3}, duration={singleCandidate.AcceptedSpeechDuration.TotalSeconds:F1}s, acceptedSegments={singleCandidate.AcceptedSegments.Count}");

            foreach (var skippedSegment in singleCandidate.SkippedSegments)
            {
                Console.WriteLine(
                    $"Skipped segment [{language.Code}] {skippedSegment.Start}->{skippedSegment.End}: {skippedSegment.Reason}");
            }

            progressService.CompleteTranscription(
                $"Selected {singleCandidate.Language.DisplayName} with score {singleCandidate.Score:0.000}");

            return singleCandidate;
        }

        for (var index = 0; index < options.SupportedLanguages.Count; index++)
        {
            var language = options.SupportedLanguages[index];
            progressService.StartLanguage(index, language.DisplayName);

            // Each configured language gets a full pass through the same audio so the
            // final choice is based on scored output instead of a static preference.
            var candidate = await TranscribeCandidateAsync(
                    processor,
                    audioSamples,
                    language,
                    options,
                    cancellationToken)
                .ConfigureAwait(false);

            candidates.Add(candidate);
            progressService.CompleteLanguage(
                $"Finished {language.DisplayName}: score {candidate.Score:0.000}, accepted segments {candidate.AcceptedSegments.Count}");

            Console.WriteLine(
                $"Candidate {language.Code}: score={candidate.Score:F3}, duration={candidate.AcceptedSpeechDuration.TotalSeconds:F1}s, acceptedSegments={candidate.AcceptedSegments.Count}");

            foreach (var skippedSegment in candidate.SkippedSegments)
            {
                Console.WriteLine(
                    $"Skipped segment [{language.Code}] {skippedSegment.Start}->{skippedSegment.End}: {skippedSegment.Reason}");
            }
        }

        var decision = DecideWinningCandidate(candidates, options);
        if (!string.IsNullOrWhiteSpace(decision.WarningMessage))
        {
            Console.WriteLine(decision.WarningMessage);
        }

        progressService.CompleteTranscription(
            $"Selected {decision.WinningCandidate.Language.DisplayName} with score {decision.WinningCandidate.Score:0.000}");

        return decision.WinningCandidate;
    }

    /// <summary>
    /// Creates a Whisper processor configured to reduce hallucinations and report progress.
    /// </summary>
    private static WhisperProcessor CreateProcessor(
        WhisperFactory whisperFactory,
        TranscriptionOptions options,
        ConsoleProgressService progressService)
    {
        var builder = whisperFactory.CreateBuilder()
            .WithLanguage(options.SupportedLanguages[0].Code)
            .WithProbabilities()
            .WithNoSpeechThreshold(options.NoSpeechThreshold)
            .WithLogProbThreshold(options.LogProbThreshold)
            .WithEntropyThreshold(options.EntropyThreshold)
            .WithProgressHandler(progress => progressService.UpdateLanguageProgress(progress));

        if (options.UseNoContext)
        {
            builder = builder.WithNoContext();
        }

        return builder.Build();
    }

    /// <summary>
    /// Runs a single language-specific transcription pass and filters the resulting segments.
    /// </summary>
    private static async Task<CandidateTranscriptionResult> TranscribeCandidateAsync(
        WhisperProcessor processor,
        float[] audioSamples,
        SupportedLanguage language,
        TranscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        processor.ChangeLanguage(language.Code);
        var segments = new List<SegmentData>();

        // The Whisper processor streams segments incrementally, so cancellation can
        // stop a long-running candidate pass without waiting for the full transcript.
        await foreach (var segment in processor.ProcessAsync(audioSamples)
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            segments.Add(segment);
        }

        var filteringResult = TranscriptionFilter.FilterSegments(language, segments, options);
        var acceptedSpeechDuration = TimeSpan.FromSeconds(
            filteringResult.AcceptedSegments.Sum(segment => (segment.End - segment.Start).TotalSeconds));

        var weightedScore = CalculateWeightedScore(filteringResult.AcceptedSegments);

        return new CandidateTranscriptionResult(
            language,
            weightedScore,
            acceptedSpeechDuration,
            filteringResult.AcceptedSegments,
            filteringResult.SkippedSegments);
    }

    /// <summary>
    /// Applies the business rules that decide whether a candidate can be accepted.
    /// </summary>
    internal static LanguageSelectionDecision DecideWinningCandidate(
        IReadOnlyList<CandidateTranscriptionResult> candidates,
        TranscriptionOptions options)
    {
        var rankedCandidates = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Language.Priority)
            .ToList();

        var winningCandidate = rankedCandidates.FirstOrDefault();
        if (winningCandidate is null || winningCandidate.AcceptedSegments.Count == 0)
        {
            throw new UnsupportedLanguageException(CreateUnsupportedLanguageMessage(options));
        }

        if (winningCandidate.AcceptedSpeechDuration < options.MinAcceptedSpeechDuration)
        {
            throw new UnsupportedLanguageException(CreateUnsupportedLanguageMessage(options));
        }

        if (winningCandidate.Score < options.MinWinningCandidateProbability)
        {
            throw new UnsupportedLanguageException(CreateUnsupportedLanguageMessage(options));
        }

        var runnerUp = rankedCandidates.Skip(1).FirstOrDefault();
        if (runnerUp is null)
        {
            return new LanguageSelectionDecision(winningCandidate, null);
        }

        // Nearly equal scores are treated separately so configuration order can act
        // as a deterministic tie-breaker without also triggering ambiguity warnings.
        var scoreDifference = winningCandidate.Score - runnerUp.Score;
        var isAmbiguous = Math.Abs(scoreDifference) > options.TieBreakerEpsilon &&
                          scoreDifference < options.MinWinningMargin;

        if (!isAmbiguous)
        {
            return new LanguageSelectionDecision(winningCandidate, null);
        }

        if (options.RejectAmbiguousLanguageCandidates)
        {
            throw new UnsupportedLanguageException(CreateUnsupportedLanguageMessage(options, ambiguous: true));
        }

        var warningMessage =
            $"Ambiguous language scores detected. Proceeding with best candidate " +
            $"{winningCandidate.Language.DisplayName} ({winningCandidate.Score:0.000}) " +
            $"over {runnerUp.Language.DisplayName} ({runnerUp.Score:0.000}).";

        return new LanguageSelectionDecision(winningCandidate, warningMessage);
    }

    /// <summary>
    /// Computes a duration-weighted probability score for accepted segments.
    /// </summary>
    private static float CalculateWeightedScore(IReadOnlyList<FilteredSegment> acceptedSegments)
    {
        if (acceptedSegments.Count == 0)
        {
            return 0f;
        }

        double weightedProbabilitySum = 0;
        double durationSum = 0;

        foreach (var segment in acceptedSegments)
        {
            // Clamp very short durations to a small positive value so zero-length
            // timing artifacts do not erase otherwise useful probability data.
            var durationSeconds = Math.Max((segment.End - segment.Start).TotalSeconds, 0.001d);
            weightedProbabilitySum += segment.Probability * durationSeconds;
            durationSum += durationSeconds;
        }

        if (durationSum == 0)
        {
            return 0f;
        }

        return (float)(weightedProbabilitySum / durationSum);
    }

    /// <summary>
    /// Builds the unsupported-language message shown to the caller.
    /// </summary>
    private static string CreateUnsupportedLanguageMessage(TranscriptionOptions options, bool ambiguous = false)
    {
        var prefix = ambiguous ? "Unsupported or ambiguous language detected." : "Unsupported language detected.";
        return $"{prefix} Supported languages are {options.GetSupportedLanguageSummary()}.";
    }
}

/// <summary>
/// Represents the scored result of one language candidate transcription.
/// </summary>
internal sealed record CandidateTranscriptionResult(
    SupportedLanguage Language,
    float Score,
    TimeSpan AcceptedSpeechDuration,
    IReadOnlyList<FilteredSegment> AcceptedSegments,
    IReadOnlyList<SkippedSegment> SkippedSegments);

/// <summary>
/// Represents the outcome of the language-selection decision stage.
/// </summary>
internal sealed record LanguageSelectionDecision(
    CandidateTranscriptionResult WinningCandidate,
    string? WarningMessage);

/// <summary>
/// Represents a controlled failure when audio cannot be accepted as a supported language.
/// </summary>
internal sealed class UnsupportedLanguageException : Exception
{
    public UnsupportedLanguageException(string message)
        : base(message)
    {
    }
}
