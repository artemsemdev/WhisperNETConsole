namespace VoxFlow.Core.Models;

/// <summary>
/// Output of <c>ISpeakerEnrichmentService.EnrichAsync</c>. A null
/// <see cref="Document"/> means enrichment did not produce a speaker-labeled
/// transcript; <see cref="Warnings"/> explains why. <see cref="RuntimeBootstrapped"/>
/// is true when this invocation materialized a fresh managed Python venv.
/// </summary>
public sealed record SpeakerEnrichmentResult(
    TranscriptDocument? Document,
    IReadOnlyList<string> Warnings,
    bool RuntimeBootstrapped)
{
    public static SpeakerEnrichmentResult Empty { get; } =
        new(Document: null, Warnings: Array.Empty<string>(), RuntimeBootstrapped: false);
}
