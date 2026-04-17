using System.ComponentModel;
using VoxFlow.Core.Models;

namespace VoxFlow.Desktop.ViewModels;

public enum PhaseStatus
{
    Idle,
    Running,
    Done,
    Skipped,
    Failed
}

public sealed record PhaseState(
    ProgressPhase Phase,
    PhaseStatus Status,
    double LocalPercent,
    string? SubStatus,
    TimeSpan Elapsed);

/// <summary>
/// Per-phase progress state for the Desktop three-ring tracker. Subscribes
/// to a <see cref="ProgressUpdate"/> stream via <see cref="OnProgress"/> and
/// projects it into three immutable <see cref="PhaseState"/> snapshots.
/// Elapsed time for the currently running phase is computed live off the
/// injected <see cref="TimeProvider"/> so callers always see an up-to-date
/// clock without needing the heartbeat timer to mutate any state.
/// </summary>
public sealed class PhaseProgressTracker : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _heartbeatInterval;
    private readonly object _stateLock = new();
    private readonly PhaseState[] _phases;
    private readonly DateTimeOffset?[] _startedAt;
    private ITimer? _heartbeat;

    public PhaseProgressTracker(
        bool speakerLabelingEnabled,
        TimeProvider? timeProvider = null,
        TimeSpan? heartbeatInterval = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _heartbeatInterval = heartbeatInterval ?? DefaultHeartbeatInterval;
        _phases = new PhaseState[3];
        _startedAt = new DateTimeOffset?[3];
        _phases[(int)ProgressPhase.Transcription] = new PhaseState(
            ProgressPhase.Transcription, PhaseStatus.Idle, 0, null, TimeSpan.Zero);
        _phases[(int)ProgressPhase.Diarization] = new PhaseState(
            ProgressPhase.Diarization,
            speakerLabelingEnabled ? PhaseStatus.Idle : PhaseStatus.Skipped,
            0,
            speakerLabelingEnabled ? null : "skipped",
            TimeSpan.Zero);
        _phases[(int)ProgressPhase.Merge] = new PhaseState(
            ProgressPhase.Merge, PhaseStatus.Idle, 0, null, TimeSpan.Zero);
    }

    public IReadOnlyList<PhaseState> Phases
    {
        get
        {
            lock (_stateLock)
            {
                var now = _timeProvider.GetUtcNow();
                var snap = new PhaseState[_phases.Length];
                for (var i = 0; i < _phases.Length; i++)
                {
                    if (_phases[i].Status == PhaseStatus.Running && _startedAt[i].HasValue)
                    {
                        var live = now - _startedAt[i]!.Value;
                        if (live < TimeSpan.Zero) live = TimeSpan.Zero;
                        snap[i] = _phases[i] with { Elapsed = live };
                    }
                    else
                    {
                        snap[i] = _phases[i];
                    }
                }
                return snap;
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void OnProgress(ProgressUpdate update)
    {
        lock (_stateLock)
        {
            var now = _timeProvider.GetUtcNow();
            var isFailed = update.Stage == ProgressStage.Failed;
            var isComplete = update.Stage == ProgressStage.Complete;

            if (isFailed)
            {
                var target = -1;
                for (var i = 0; i < _phases.Length; i++)
                {
                    if (_phases[i].Status == PhaseStatus.Running)
                    {
                        target = i;
                        break;
                    }
                }
                if (target < 0)
                    target = (int)ProgressPhaseBanding.PhaseOf(update.Stage);

                var elapsed = _startedAt[target] is { } t
                    ? now - t
                    : _phases[target].Elapsed;
                _phases[target] = _phases[target] with
                {
                    Status = PhaseStatus.Failed,
                    SubStatus = update.Message ?? "failed",
                    Elapsed = elapsed,
                };
                DisposeHeartbeatLocked();
            }
            else
            {
                var newPhase = ProgressPhaseBanding.PhaseOf(update.Stage);
                var newPhaseIdx = (int)newPhase;

                // Finalize phases before newPhase that haven't finished yet.
                for (var i = 0; i < newPhaseIdx; i++)
                    FinalizeAsDone(i, now);

                // Update newPhase unless it was pre-marked Skipped.
                if (_phases[newPhaseIdx].Status != PhaseStatus.Skipped)
                {
                    if (_phases[newPhaseIdx].Status == PhaseStatus.Idle)
                        _startedAt[newPhaseIdx] = now;

                    var elapsed = _startedAt[newPhaseIdx] is { } t
                        ? now - t
                        : TimeSpan.Zero;
                    if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;

                    var status = isComplete ? PhaseStatus.Done : PhaseStatus.Running;
                    var localPct = isComplete
                        ? 100.0
                        : ProgressPhaseBanding.LocalPercent(update.Stage, update.PercentComplete);
                    _phases[newPhaseIdx] = _phases[newPhaseIdx] with
                    {
                        Status = status,
                        LocalPercent = localPct,
                        SubStatus = update.Message ?? StageSubLabel(update.Stage),
                        Elapsed = elapsed,
                    };
                }

                if (isComplete)
                {
                    for (var i = 0; i < _phases.Length; i++)
                        FinalizeAsDone(i, now);
                    DisposeHeartbeatLocked();
                }
                else
                {
                    EnsureHeartbeatLocked();
                }
            }
        }
        RaisePhasesChanged();
    }

    private void FinalizeAsDone(int index, DateTimeOffset now)
    {
        var phase = _phases[index];
        if (phase.Status is PhaseStatus.Done or PhaseStatus.Skipped or PhaseStatus.Failed)
            return;

        var elapsed = _startedAt[index] is { } t
            ? now - t
            : TimeSpan.Zero;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        _phases[index] = phase with
        {
            Status = PhaseStatus.Done,
            LocalPercent = 100.0,
            SubStatus = "done",
            Elapsed = elapsed,
        };
    }

    private void EnsureHeartbeatLocked()
    {
        if (_heartbeat is not null) return;
        _heartbeat = _timeProvider.CreateTimer(
            static s => ((PhaseProgressTracker)s!).OnHeartbeat(),
            this,
            _heartbeatInterval,
            _heartbeatInterval);
    }

    private void DisposeHeartbeatLocked()
    {
        _heartbeat?.Dispose();
        _heartbeat = null;
    }

    private void OnHeartbeat()
    {
        try
        {
            RaisePhasesChanged();
        }
        catch
        {
            // Heartbeat is best-effort — never surface exceptions from
            // the timer thread.
        }
    }

    private void RaisePhasesChanged()
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Phases)));

    private static string? StageSubLabel(ProgressStage stage) => stage switch
    {
        ProgressStage.Validating => "validating",
        ProgressStage.Converting => "converting audio",
        ProgressStage.LoadingModel => "loading model",
        ProgressStage.Transcribing => "transcribing",
        ProgressStage.Filtering => "filtering segments",
        ProgressStage.Diarizing => "diarizing",
        ProgressStage.Writing => "writing output",
        ProgressStage.Complete => "done",
        ProgressStage.Failed => "failed",
        _ => null
    };

    public void Dispose()
    {
        lock (_stateLock)
        {
            DisposeHeartbeatLocked();
        }
    }
}
