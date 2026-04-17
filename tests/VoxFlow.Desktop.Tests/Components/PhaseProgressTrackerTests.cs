using VoxFlow.Core.Models;
using VoxFlow.Desktop.ViewModels;
using Xunit;

namespace VoxFlow.Desktop.Tests.Components;

/// <summary>
/// Minimal <see cref="TimeProvider"/> whose clock is advanced manually by the
/// test. The heartbeat timer is a no-op stub — the tracker's Elapsed is
/// computed live off <see cref="GetUtcNow"/>, so tests only need to advance
/// the clock and read <c>Phases</c>.
/// </summary>
internal sealed class ControllableTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public ControllableTimeProvider(DateTimeOffset start)
    {
        _now = start;
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now += delta;

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period) => new NoopTimer();

    private sealed class NoopTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }
}

public sealed class PhaseProgressTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static ProgressUpdate Frame(ProgressStage stage, double pct, string? msg = null)
        => new(stage, pct, TimeSpan.Zero, Message: msg);

    [Fact]
    public void NewTracker_SpeakerLabelingEnabled_AllThreePhasesIdleAndZero()
    {
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        var phases = tracker.Phases;
        Assert.Equal(3, phases.Count);
        Assert.Equal(ProgressPhase.Transcription, phases[0].Phase);
        Assert.Equal(ProgressPhase.Diarization, phases[1].Phase);
        Assert.Equal(ProgressPhase.Merge, phases[2].Phase);
        Assert.All(phases, p => Assert.Equal(PhaseStatus.Idle, p.Status));
        Assert.All(phases, p => Assert.Equal(0.0, p.LocalPercent));
        Assert.All(phases, p => Assert.Equal(TimeSpan.Zero, p.Elapsed));
    }

    [Fact]
    public void NewTracker_SpeakerLabelingDisabled_DiarizationBornSkipped()
    {
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: false, timeProvider: clock);

        var phases = tracker.Phases;
        Assert.Equal(PhaseStatus.Idle, phases[0].Status);
        Assert.Equal(PhaseStatus.Skipped, phases[1].Status);
        Assert.Equal(PhaseStatus.Idle, phases[2].Status);
    }

    [Fact]
    public void OnProgress_TranscribingFrame_MovesTranscriptionToRunning()
    {
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        tracker.OnProgress(Frame(ProgressStage.Transcribing, 45.0));

        var phases = tracker.Phases;
        Assert.Equal(PhaseStatus.Running, phases[0].Status);
        Assert.Equal(50.0, phases[0].LocalPercent, 3);
        Assert.Equal("transcribing", phases[0].SubStatus);
        Assert.Equal(PhaseStatus.Idle, phases[1].Status);
        Assert.Equal(PhaseStatus.Idle, phases[2].Status);
    }

    [Fact]
    public void OnProgress_ExplicitMessageWinsOverStageLabel()
    {
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        tracker.OnProgress(Frame(ProgressStage.Transcribing, 10.0, msg: "loading model"));

        Assert.Equal("loading model", tracker.Phases[0].SubStatus);
    }

    [Fact]
    public void OnProgress_DiarizingAfterTranscribing_MarksTranscriptionDoneAt100()
    {
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        tracker.OnProgress(Frame(ProgressStage.Transcribing, 45.0));
        clock.Advance(TimeSpan.FromSeconds(22));
        tracker.OnProgress(Frame(ProgressStage.Diarizing, 92.0));

        var phases = tracker.Phases;
        Assert.Equal(PhaseStatus.Done, phases[0].Status);
        Assert.Equal(100.0, phases[0].LocalPercent, 3);
        Assert.Equal(TimeSpan.FromSeconds(22), phases[0].Elapsed);
        Assert.Equal(PhaseStatus.Running, phases[1].Status);
    }

    [Fact]
    public void OnProgress_WritingAfterTranscribing_MarksBothUpstreamPhasesDone()
    {
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: false, timeProvider: clock);

        tracker.OnProgress(Frame(ProgressStage.Transcribing, 45.0));
        clock.Advance(TimeSpan.FromSeconds(10));
        tracker.OnProgress(Frame(ProgressStage.Writing, 96.0));

        var phases = tracker.Phases;
        Assert.Equal(PhaseStatus.Done, phases[0].Status);
        Assert.Equal(PhaseStatus.Skipped, phases[1].Status);
        Assert.Equal(PhaseStatus.Running, phases[2].Status);
    }

    [Fact]
    public void OnProgress_CompleteFrame_MarksAllNonSkippedPhasesDone()
    {
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        tracker.OnProgress(Frame(ProgressStage.Transcribing, 30.0));
        clock.Advance(TimeSpan.FromSeconds(5));
        tracker.OnProgress(Frame(ProgressStage.Complete, 100.0));

        var phases = tracker.Phases;
        Assert.All(phases, p => Assert.Equal(PhaseStatus.Done, p.Status));
        Assert.All(phases, p => Assert.Equal(100.0, p.LocalPercent));
    }

    [Fact]
    public void OnProgress_Failed_MarksRunningPhaseFailed()
    {
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        tracker.OnProgress(Frame(ProgressStage.Transcribing, 30.0));
        clock.Advance(TimeSpan.FromSeconds(3));
        tracker.OnProgress(Frame(ProgressStage.Failed, 30.0, msg: "model load failed"));

        Assert.Equal(PhaseStatus.Failed, tracker.Phases[0].Status);
        Assert.Equal("model load failed", tracker.Phases[0].SubStatus);
    }

    [Fact]
    public void Heartbeat_ElapsedAdvancesWithClock_WhileRunning()
    {
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        tracker.OnProgress(Frame(ProgressStage.Transcribing, 10.0));
        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromSeconds(2), tracker.Phases[0].Elapsed);
    }

    [Fact]
    public void Heartbeat_ElapsedFrozen_AfterTerminalComplete()
    {
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);

        tracker.OnProgress(Frame(ProgressStage.Transcribing, 30.0));
        clock.Advance(TimeSpan.FromSeconds(5));
        tracker.OnProgress(Frame(ProgressStage.Complete, 100.0));
        var frozen = tracker.Phases[0].Elapsed;
        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(frozen, tracker.Phases[0].Elapsed);
    }

    [Fact]
    public void OnProgress_RaisesPropertyChangedForPhases()
    {
        var clock = new ControllableTimeProvider(T0);
        using var tracker = new PhaseProgressTracker(speakerLabelingEnabled: true, timeProvider: clock);
        var changes = new List<string?>();
        tracker.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        tracker.OnProgress(Frame(ProgressStage.Transcribing, 45.0));

        Assert.Contains(nameof(PhaseProgressTracker.Phases), changes);
    }
}
