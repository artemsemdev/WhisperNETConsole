using System;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Services.Diarization;

/// <summary>
/// Raised by <see cref="PyannoteSidecarClient"/> when diarization fails in a
/// way the caller can categorize via <see cref="Reason"/> and either retry,
/// surface to the user, or give up.
/// </summary>
public sealed class DiarizationSidecarException : Exception
{
    public DiarizationSidecarException(SidecarFailureReason reason, string message, Exception? inner = null)
        : base(message, inner)
    {
        Reason = reason;
    }

    public SidecarFailureReason Reason { get; }
}
