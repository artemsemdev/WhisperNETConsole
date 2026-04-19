namespace VoxFlow.Core.Models;

/// <summary>
/// Shared mapping between <see cref="ProgressStage"/> values and the three
/// user-facing phases (<see cref="ProgressPhase"/>). Extracted from the CLI
/// progress handler so the CLI's stacked bars and the Desktop's three rings
/// stay bit-for-bit consistent.
/// </summary>
public static class ProgressPhaseBanding
{
    public static ProgressPhase PhaseOf(ProgressStage stage) => stage switch
    {
        ProgressStage.Diarizing => ProgressPhase.Diarization,
        ProgressStage.Writing or ProgressStage.Complete => ProgressPhase.Merge,
        _ => ProgressPhase.Transcription
    };

    /// <summary>
    /// Remaps an overall 0..100 % into the current phase's local 0..100 %
    /// so each ring shows its own progress instead of the shared overall.
    /// </summary>
    public static double LocalPercent(ProgressStage stage, double overall)
    {
        if (stage == ProgressStage.Failed)
            return Clamp(overall);

        var (start, end) = PhaseOf(stage) switch
        {
            ProgressPhase.Transcription => (0.0, 90.0),
            ProgressPhase.Diarization => (90.0, 95.0),
            ProgressPhase.Merge => (95.0, 100.0),
            _ => (0.0, 100.0)
        };
        var span = end - start;
        if (span <= 0) return 0.0;
        return Clamp((overall - start) / span * 100.0);
    }

    /// <summary>
    /// The overall-percent ceiling for the phase a stage belongs to. Used
    /// by the CLI to synthesize a closing 100 % frame on phase transition
    /// because neither Whisper nor pyannote emits one reliably.
    /// </summary>
    public static double PhaseUpperBound(ProgressStage stage) => PhaseOf(stage) switch
    {
        ProgressPhase.Transcription => 90.0,
        ProgressPhase.Diarization => 95.0,
        ProgressPhase.Merge => 100.0,
        _ => 100.0
    };

    private static double Clamp(double value)
    {
        if (value < 0) return 0;
        if (value > 100) return 100;
        return value;
    }
}
