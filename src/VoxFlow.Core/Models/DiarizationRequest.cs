namespace VoxFlow.Core.Models;

/// <summary>
/// Input to <c>IDiarizationSidecar.DiarizeAsync</c>: the absolute path to a
/// WAV file the sidecar should diarize.
/// </summary>
public sealed record DiarizationRequest(string WavPath);
