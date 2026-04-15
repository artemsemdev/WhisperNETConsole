using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VoxFlow.Core.Models;
using VoxFlow.Core.Services.Diarization;
using VoxFlow.Core.Services.Python;
using VoxFlow.Core.Tests.Services.Python;
using Xunit;

namespace VoxFlow.Core.Tests.Services.Diarization;

public sealed class PyannoteSidecarClientTests
{
    private const string ScriptPath = "/tmp/voxflow_diarize.py";

    [Fact]
    public async Task DiarizeAsync_HappyPath_ReturnsResultFromStdout()
    {
        var runtime = new FakePythonRuntime();
        var launcher = new FakeProcessLauncher();
        launcher.SetResponse(
            runtime.InterpreterPath,
            exitCode: 0,
            stdOut: """
            {
              "version": 1,
              "status": "ok",
              "speakers": [
                { "id": "A", "totalDuration": 4.5 },
                { "id": "B", "totalDuration": 2.25 }
              ],
              "segments": [
                { "speaker": "A", "start": 0.0, "end": 4.5 },
                { "speaker": "B", "start": 4.5, "end": 6.75 }
              ]
            }
            """);

        var client = new PyannoteSidecarClient(runtime, launcher, ScriptPath, TimeSpan.FromSeconds(5));

        var result = await client.DiarizeAsync(
            new DiarizationRequest("/tmp/input.wav"),
            progress: null,
            CancellationToken.None);

        Assert.Equal(1, result.Version);
        Assert.Equal(2, result.Speakers.Count);
        Assert.Equal("A", result.Speakers[0].Id);
        Assert.Equal(4.5, result.Speakers[0].TotalDuration);
        Assert.Equal(2, result.Segments.Count);
        Assert.Equal("A", result.Segments[0].Speaker);
        Assert.Equal(0.0, result.Segments[0].Start);
        Assert.Equal(4.5, result.Segments[0].End);

        Assert.Single(launcher.Invocations);
        var stdinPayload = Assert.Single(launcher.StdInputs);
        Assert.NotNull(stdinPayload);
        Assert.Contains("\"wavPath\"", stdinPayload);
        Assert.Contains("/tmp/input.wav", stdinPayload);
        Assert.Contains("\"version\"", stdinPayload);
        Assert.Equal(ScriptPath, runtime.StartInfoRequests.Single().ScriptPath);
    }

    [Fact]
    public async Task DiarizeAsync_NonZeroExit_ThrowsWithProcessCrashed()
    {
        var runtime = new FakePythonRuntime();
        var launcher = new FakeProcessLauncher();
        launcher.SetResponse(runtime.InterpreterPath, exitCode: 1, stdOut: string.Empty, stdErr: "segfault");

        var client = new PyannoteSidecarClient(runtime, launcher, ScriptPath, TimeSpan.FromSeconds(5));

        var ex = await Assert.ThrowsAsync<DiarizationSidecarException>(
            () => client.DiarizeAsync(new DiarizationRequest("/tmp/a.wav"), progress: null, CancellationToken.None));

        Assert.Equal(SidecarFailureReason.ProcessCrashed, ex.Reason);
        Assert.Contains("segfault", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiarizeAsync_MalformedJsonOnStdout_ThrowsWithMalformedJson()
    {
        var runtime = new FakePythonRuntime();
        var launcher = new FakeProcessLauncher();
        launcher.SetResponse(runtime.InterpreterPath, exitCode: 0, stdOut: "{ this is not json");

        var client = new PyannoteSidecarClient(runtime, launcher, ScriptPath, TimeSpan.FromSeconds(5));

        var ex = await Assert.ThrowsAsync<DiarizationSidecarException>(
            () => client.DiarizeAsync(new DiarizationRequest("/tmp/a.wav"), progress: null, CancellationToken.None));

        Assert.Equal(SidecarFailureReason.MalformedJson, ex.Reason);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public async Task DiarizeAsync_ResponseWithStatusError_ThrowsWithErrorResponseReturned()
    {
        var runtime = new FakePythonRuntime();
        var launcher = new FakeProcessLauncher();
        launcher.SetResponse(
            runtime.InterpreterPath,
            exitCode: 0,
            stdOut: """
            {
              "version": 1,
              "status": "error",
              "error": "wav file not found: /tmp/a.wav",
              "speakers": [],
              "segments": []
            }
            """);

        var client = new PyannoteSidecarClient(runtime, launcher, ScriptPath, TimeSpan.FromSeconds(5));

        var ex = await Assert.ThrowsAsync<DiarizationSidecarException>(
            () => client.DiarizeAsync(new DiarizationRequest("/tmp/a.wav"), progress: null, CancellationToken.None));

        Assert.Equal(SidecarFailureReason.ErrorResponseReturned, ex.Reason);
        Assert.Contains("wav file not found", ex.Message);
    }

    [Fact]
    public async Task DiarizeAsync_Timeout_ThrowsTimeout()
    {
        var runtime = new FakePythonRuntime();
        var launcher = new FakeProcessLauncher();
        launcher.SetNeverReturns(runtime.InterpreterPath);

        var client = new PyannoteSidecarClient(
            runtime, launcher, ScriptPath, timeout: TimeSpan.FromMilliseconds(100));

        var ex = await Assert.ThrowsAsync<DiarizationSidecarException>(
            () => client.DiarizeAsync(new DiarizationRequest("/tmp/a.wav"), progress: null, CancellationToken.None));

        Assert.Equal(SidecarFailureReason.Timeout, ex.Reason);
    }

    [Fact]
    public async Task DiarizeAsync_ExternalCancellation_ThrowsOperationCanceled()
    {
        var runtime = new FakePythonRuntime();
        var launcher = new FakeProcessLauncher();
        launcher.SetNeverReturns(runtime.InterpreterPath);

        var client = new PyannoteSidecarClient(
            runtime, launcher, ScriptPath, timeout: TimeSpan.FromSeconds(30));

        using var cts = new CancellationTokenSource();
        var task = client.DiarizeAsync(new DiarizationRequest("/tmp/a.wav"), progress: null, cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task DiarizeAsync_ProgressOnStderr_ForwardsToReporter()
    {
        var runtime = new FakePythonRuntime();
        var launcher = new FakeProcessLauncher();
        launcher.SetResponse(
            runtime.InterpreterPath,
            exitCode: 0,
            stdOut: """{"version":1,"status":"ok","speakers":[{"id":"A","totalDuration":1.0}],"segments":[{"speaker":"A","start":0.0,"end":1.0}]}""",
            stdErr: """
            {"stage":"loading_model"}
            {"stage":"inferring","fraction":0.5}
            """);

        var reports = new List<SpeakerLabelingProgress>();
        var progress = new ListProgress<SpeakerLabelingProgress>(reports);

        var client = new PyannoteSidecarClient(runtime, launcher, ScriptPath, TimeSpan.FromSeconds(5));
        await client.DiarizeAsync(new DiarizationRequest("/tmp/a.wav"), progress, CancellationToken.None);

        Assert.Equal(2, reports.Count);
        Assert.Equal("loading_model", reports[0].Stage);
        Assert.Null(reports[0].Fraction);
        Assert.Equal("inferring", reports[1].Stage);
        Assert.Equal(0.5, reports[1].Fraction);
    }

    [Fact]
    public async Task DiarizeAsync_SchemaViolation_ThrowsWithSchemaViolation()
    {
        var runtime = new FakePythonRuntime();
        var launcher = new FakeProcessLauncher();
        // Valid JSON, but missing required "speakers" field → schema violation.
        launcher.SetResponse(
            runtime.InterpreterPath,
            exitCode: 0,
            stdOut: """{"version":1,"status":"ok","segments":[]}""");

        var client = new PyannoteSidecarClient(runtime, launcher, ScriptPath, TimeSpan.FromSeconds(5));

        var ex = await Assert.ThrowsAsync<DiarizationSidecarException>(
            () => client.DiarizeAsync(new DiarizationRequest("/tmp/a.wav"), progress: null, CancellationToken.None));

        Assert.Equal(SidecarFailureReason.SchemaViolation, ex.Reason);
    }

    [Fact]
    public async Task DiarizeAsync_RuntimeNotReady_ThrowsWithRuntimeNotReady()
    {
        var runtime = new FakePythonRuntime
        {
            NextStatus = PythonRuntimeStatus.NotReady("venv missing, call CreateVenv first")
        };
        var launcher = new FakeProcessLauncher();

        var client = new PyannoteSidecarClient(runtime, launcher, ScriptPath, TimeSpan.FromSeconds(5));

        var ex = await Assert.ThrowsAsync<DiarizationSidecarException>(
            () => client.DiarizeAsync(new DiarizationRequest("/tmp/a.wav"), progress: null, CancellationToken.None));

        Assert.Equal(SidecarFailureReason.RuntimeNotReady, ex.Reason);
        Assert.Contains("venv missing", ex.Message);
        Assert.Empty(launcher.Invocations);
    }

    private sealed class ListProgress<T> : IProgress<T>
    {
        private readonly List<T> _list;
        public ListProgress(List<T> list) { _list = list; }
        public void Report(T value) => _list.Add(value);
    }
}
