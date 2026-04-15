using System;

namespace VoxFlow.Core.Configuration;

/// <summary>
/// Immutable configuration for the local speaker-labeling enrichment pipeline.
/// </summary>
public sealed record SpeakerLabelingOptions(
    bool Enabled,
    int TimeoutSeconds,
    PythonRuntimeMode RuntimeMode,
    string ModelId)
{
    public static readonly SpeakerLabelingOptions Disabled = new(
        Enabled: false,
        TimeoutSeconds: 600,
        RuntimeMode: PythonRuntimeMode.ManagedVenv,
        ModelId: "pyannote/speaker-diarization-community-1");

    public int TimeoutSeconds { get; } = EnsurePositiveTimeout(TimeoutSeconds);
    public string ModelId { get; } = EnsureNonEmptyModelId(ModelId);

    private static int EnsurePositiveTimeout(int value)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException(
                $"Settings value '{nameof(TimeoutSeconds)}' must be greater than zero.");
        }

        return value;
    }

    private static string EnsureNonEmptyModelId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Settings value '{nameof(ModelId)}' is required.");
        }

        return value.Trim();
    }
}
