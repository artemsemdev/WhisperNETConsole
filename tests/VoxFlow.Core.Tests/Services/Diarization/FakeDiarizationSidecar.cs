using System;
using System.Threading;
using System.Threading.Tasks;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Tests.Services.Diarization;

/// <summary>
/// Test double for <see cref="IDiarizationSidecar"/>. Each test supplies a
/// delegate that produces the result (or throws) for a single DiarizeAsync
/// invocation. Tracks the call count so tests can assert non-invocation.
/// </summary>
internal sealed class FakeDiarizationSidecar : IDiarizationSidecar
{
    private readonly Func<DiarizationRequest, IProgress<SpeakerLabelingProgress>?, CancellationToken, Task<DiarizationResult>> _handler;

    public FakeDiarizationSidecar(
        Func<DiarizationRequest, IProgress<SpeakerLabelingProgress>?, CancellationToken, Task<DiarizationResult>> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public int CallCount { get; private set; }

    public Task<DiarizationResult> DiarizeAsync(
        DiarizationRequest request,
        IProgress<SpeakerLabelingProgress>? progress,
        CancellationToken cancellationToken)
    {
        CallCount++;
        return _handler(request, progress, cancellationToken);
    }

    public static FakeDiarizationSidecar ThrowsIfCalled()
        => new((_, _, _) => throw new InvalidOperationException("FakeDiarizationSidecar should not have been called."));
}
