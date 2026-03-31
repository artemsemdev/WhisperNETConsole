# Performance and Scalability

> How VoxFlow manages resources, where time is spent, and what would change under different load profiles.

## Performance Profile

VoxFlow is a local, single-user tool. Performance is dominated by two operations:

| Operation | Typical Duration | Bound By |
|-----------|-----------------|----------|
| ffmpeg audio conversion | Seconds | I/O + CPU (resampling, filtering) |
| Whisper inference | Minutes per hour of audio | CPU (native Whisper runtime) |

All other stages (configuration loading, validation, WAV parsing, filtering, output writing) complete in milliseconds and are not performance-sensitive.

## Resource Management

### Whisper Model Lifecycle

`ModelService` (`Services/ModelService.cs`) caches a single `WhisperFactory` instance per model path:

- `GetOrCreateFactoryAsync()` checks if the cached factory matches the current `modelFilePath`
- On cache hit: reuses the existing factory (no native re-initialization)
- On model change: disposes the previous factory, creates a new one
- `ModelService` implements `IDisposable` — the host must dispose the service provider to release native memory

This follows ADR-010 (reuse Whisper runtime within a run) and ADR-011 (share factory across batch files).

### Whisper Processor Reuse

`LanguageSelectionService` (`Services/LanguageSelectionService.cs`) creates a single `WhisperProcessor` per transcription and reuses it across language candidate passes via `processor.ChangeLanguage()`. This avoids native teardown/setup costs between candidates.

### ffmpeg Process Management

`AudioConversionService` (`Services/AudioConversionService.cs`) spawns ffmpeg as a child process:

- `using var process = new Process` ensures disposal
- `cancellationToken.Register()` attaches a cancellation callback that calls `process.Kill(entireProcessTree: true)`
- Stdout and stderr are captured in parallel, then awaited after process exit
- On cancellation, `InvalidOperationException` is caught (process may already have exited)

### Audio Memory

`WavAudioLoader` (`Services/WavAudioLoader.cs`) reads the entire WAV file into memory:

```
File.ReadAllBytesAsync() → byte[] → float[] samples
```

This is a full-buffer approach, not streaming. For a typical 1-hour recording at 16kHz mono:

| Data | Size |
|------|------|
| WAV file on disk | ~115 MB |
| `float[]` samples in memory | ~230 MB |
| Whisper model (base) | ~75–140 MB |
| **Total per transcription** | **~400 MB** |

This is acceptable for a local desktop/CLI tool processing one file at a time. Streaming would reduce peak memory but adds complexity for no practical benefit at current file sizes.

### Temp File Lifecycle

Intermediate WAV files are:

1. Created by ffmpeg during audio conversion
2. Read by `WavAudioLoader` for sample extraction
3. Deleted in a `finally` block after transcription completes

Cleanup is best-effort: `IOException` and `UnauthorizedAccessException` are caught and suppressed so a failed delete does not hide the transcription result. The `keepIntermediateFiles` configuration flag preserves WAVs for debugging.

In batch mode, temp files use unique names (`{filename}_{guid}.wav`) in a configurable temp directory to prevent collisions.

## Batch Processing

### Sequential Loop

`BatchTranscriptionService` (`Services/BatchTranscriptionService.cs`) processes files in a sequential `for` loop:

```
Load factory once (ADR-010, ADR-011)
    ↓
for each file:
    try:
        Convert → Load → Infer → Filter → Write
    catch (OperationCanceledException):
        Re-throw (stop the batch)
    catch (Exception):
        Record error; continue or stop (configurable)
    finally:
        Cleanup temp WAV
```

### Error Isolation

Each file has independent error handling. Failures do not propagate to subsequent files unless `stopOnFirstError` is configured. `OperationCanceledException` (Ctrl+C) is always re-thrown immediately.

### Scaling Characteristics

| Dimension | Current behavior | Scaling note |
|-----------|-----------------|--------------|
| Files in batch | Sequential, one at a time | Wall-clock time scales linearly with file count |
| File duration | Single-threaded inference | Processing time proportional to audio duration |
| Model size | Single model loaded once | Larger models (small → large) increase memory and inference time |
| Concurrent users | Not applicable | Single-user local tool |

## Cancellation

Cancellation propagates from the entry point through all async operations:

1. **CLI:** `Console.CancelKeyPress` → `CancellationTokenSource.Cancel()`
2. **Desktop:** `AppViewModel.CancelTranscription()` → `CancellationTokenSource.Cancel()`
3. **Service layer:** `cancellationToken.ThrowIfCancellationRequested()` at operation boundaries
4. **Whisper streaming:** `processor.ProcessAsync(samples).WithCancellation(cancellationToken)`
5. **ffmpeg process:** `cancellationToken.Register()` → `process.Kill(entireProcessTree: true)`

Cancellation is cooperative. Latency between signal and stop depends on where execution is when the token fires — typically at the next segment boundary during Whisper inference.

## Desktop UI Performance

### Progress Reporting Thread Safety

`BlazorProgressHandler` (`Services/BlazorProgressHandler.cs`) bridges background transcription to the UI thread:

```csharp
MainThread.BeginInvokeOnMainThread(() =>
{
    _viewModel.CurrentProgress = value;
    _viewModel.NotifyStateChanged();
});
```

Core transcription runs on a background thread. Progress updates are queued to the MAUI UI thread dispatcher asynchronously. The UI thread is never blocked by transcription work.

### Intel CLI Bridge Overhead

On Intel Mac Catalyst, `DesktopCliTranscriptionService` launches `VoxFlow.Cli` as a child process. This adds:

- Process startup latency (dotnet host initialization)
- Temp config file write (merged Desktop config snapshot)
- Inter-process communication via structured JSON lines on stderr

This overhead is small relative to the transcription itself and avoids the in-process Whisper runtime instability on Intel Mac Catalyst.

## Configuration Knobs

Settings in `TranscriptionOptions` that affect performance characteristics:

| Setting | Effect |
|---------|--------|
| `modelType` | Larger models (small, medium, large) increase memory and inference time but improve accuracy |
| `audioFilterChain` | More ffmpeg filters increase conversion time |
| `outputSampleRate` | Fixed at 16kHz (Whisper's expected rate); not user-tunable for performance |
| `batch.stopOnFirstError` | Stops batch early vs. continuing to process remaining files |
| `batch.keepIntermediateFiles` | Retains WAV files for debugging; increases disk usage |
| `batch.tempDirectory` | Location for intermediate WAVs; defaults to system temp |

There are no explicit timeout, buffer size, or thread pool settings. The system relies on OS defaults and cooperative cancellation.

## Where the Design Would Evolve

These are not current weaknesses — they are boundaries that would shift under different requirements.

| Trigger | Current State | Evolution |
|---------|--------------|-----------|
| Large batch throughput | Sequential processing | Producer-consumer pipeline: overlap ffmpeg conversion with Whisper inference (ADR-011 alternative) |
| Very long recordings (>4 hours) | Full audio buffer in memory | Streaming WAV loader; chunked inference |
| Multiple concurrent users | Single-user local tool | Service host with request queuing; model instance pooling |
| GPU acceleration | CPU-only inference | Whisper.net CUDA/Metal backend; requires native library changes |
| Real-time transcription | Batch/file-based only | Audio stream capture; sliding window inference |

Each would be driven by a concrete requirement, not added speculatively.
