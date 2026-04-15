namespace VoxFlow.Core.Models;

/// <summary>
/// Taxonomy of diarization-sidecar failures surfaced via
/// <c>DiarizationSidecarException</c>. Each value corresponds to a
/// user-actionable remedy (e.g., install the runtime, increase the timeout,
/// report a schema regression).
/// </summary>
public enum SidecarFailureReason
{
    RuntimeNotReady,
    ProcessCrashed,
    Timeout,
    MalformedJson,
    SchemaViolation,
    ErrorResponseReturned
}
