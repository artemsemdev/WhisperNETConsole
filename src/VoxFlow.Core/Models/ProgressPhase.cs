namespace VoxFlow.Core.Models;

/// <summary>
/// The three coarse phases a transcription run goes through. Used by both
/// the CLI progress bar and the Desktop three-ring progress tracker so the
/// two hosts display the same banded view of a <see cref="ProgressStage"/>
/// stream.
/// </summary>
public enum ProgressPhase
{
    Transcription,
    Diarization,
    Merge
}
