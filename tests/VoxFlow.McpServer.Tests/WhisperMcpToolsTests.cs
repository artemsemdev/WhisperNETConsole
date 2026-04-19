#nullable enable
using System.Text.Json;
using Microsoft.Extensions.Options;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;
using VoxFlow.McpServer.Configuration;
using VoxFlow.McpServer.Security;
using VoxFlow.McpServer.Tools;
using Whisper.net;
using Xunit;

namespace VoxFlow.McpServer.Tests;

public sealed class WhisperMcpToolsTests
{
    [Fact]
    public async Task ReadTranscriptAsync_UsesOutputPathPolicy()
    {
        var tempDir = CreateTempDirectory();

        try
        {
            var allowedOutputRoot = Path.Combine(tempDir, "output");
            Directory.CreateDirectory(allowedOutputRoot);
            var transcriptPath = Path.Combine(allowedOutputRoot, "result.txt");
            await File.WriteAllTextAsync(transcriptPath, "hello world");

            var tools = CreateTools(
                new PathPolicy(
                    allowedInputRoots: [Path.Combine(tempDir, "input")],
                    allowedOutputRoots: [allowedOutputRoot]),
                new StubTranscriptReader());

            var response = await tools.ReadTranscriptAsync(transcriptPath);
            using var json = JsonDocument.Parse(response);

            Assert.Equal(transcriptPath, json.RootElement.GetProperty("Path").GetString());
            Assert.Equal("hello world", json.RootElement.GetProperty("Content").GetString());
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Fact]
    public async Task TranscribeFileAsync_ForwardsEnableSpeakersTrue()
    {
        var capture = new CapturingTranscriptionService();
        var tempDir = CreateTempDirectory();

        try
        {
            var inputRoot = Path.Combine(tempDir, "input");
            Directory.CreateDirectory(inputRoot);
            var inputPath = Path.Combine(inputRoot, "demo.m4a");
            await File.WriteAllTextAsync(inputPath, "stub");

            var tools = CreateToolsWithTranscription(
                capture,
                new PathPolicy(
                    allowedInputRoots: [inputRoot],
                    allowedOutputRoots: Array.Empty<string>()));

            await tools.TranscribeFileAsync(inputPath, enableSpeakers: true);

            Assert.NotNull(capture.LastRequest);
            Assert.True(capture.LastRequest!.EnableSpeakers);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Fact]
    public async Task TranscribeFileAsync_ForwardsEnableSpeakersFalse()
    {
        var capture = new CapturingTranscriptionService();
        var tempDir = CreateTempDirectory();

        try
        {
            var inputRoot = Path.Combine(tempDir, "input");
            Directory.CreateDirectory(inputRoot);
            var inputPath = Path.Combine(inputRoot, "demo.m4a");
            await File.WriteAllTextAsync(inputPath, "stub");

            var tools = CreateToolsWithTranscription(
                capture,
                new PathPolicy(
                    allowedInputRoots: [inputRoot],
                    allowedOutputRoots: Array.Empty<string>()));

            await tools.TranscribeFileAsync(inputPath, enableSpeakers: false);

            Assert.NotNull(capture.LastRequest);
            Assert.False(capture.LastRequest!.EnableSpeakers);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Fact]
    public async Task TranscribeFileAsync_OmittedEnableSpeakers_ForwardsNull()
    {
        var capture = new CapturingTranscriptionService();
        var tempDir = CreateTempDirectory();

        try
        {
            var inputRoot = Path.Combine(tempDir, "input");
            Directory.CreateDirectory(inputRoot);
            var inputPath = Path.Combine(inputRoot, "demo.m4a");
            await File.WriteAllTextAsync(inputPath, "stub");

            var tools = CreateToolsWithTranscription(
                capture,
                new PathPolicy(
                    allowedInputRoots: [inputRoot],
                    allowedOutputRoots: Array.Empty<string>()));

            await tools.TranscribeFileAsync(inputPath);

            Assert.NotNull(capture.LastRequest);
            Assert.Null(capture.LastRequest!.EnableSpeakers);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    [Fact]
    public async Task ReadTranscriptAsync_PathOutsideAllowedOutputRoots_ReturnsError()
    {
        var tempDir = CreateTempDirectory();

        try
        {
            var transcriptPath = Path.Combine(tempDir, "result.txt");
            await File.WriteAllTextAsync(transcriptPath, "hello world");

            var tools = CreateTools(
                new PathPolicy(
                    allowedInputRoots: Array.Empty<string>(),
                    allowedOutputRoots: [Path.Combine(tempDir, "allowed-output")]),
                new StubTranscriptReader());

            var response = await tools.ReadTranscriptAsync(transcriptPath);
            using var json = JsonDocument.Parse(response);

            Assert.Contains("Access denied", json.RootElement.GetProperty("error").GetString(), StringComparison.Ordinal);
        }
        finally
        {
            DeleteTempDirectory(tempDir);
        }
    }

    private static WhisperMcpTools CreateTools(IPathPolicy pathPolicy, ITranscriptReader transcriptReader)
    {
        return new WhisperMcpTools(
            new NotUsedTranscriptionService(),
            new NotUsedBatchTranscriptionService(),
            new NotUsedValidationService(),
            new NotUsedModelService(),
            new NotUsedConfigurationService(),
            transcriptReader,
            pathPolicy,
            Options.Create(new McpOptions()));
    }

    private static WhisperMcpTools CreateToolsWithTranscription(
        ITranscriptionService transcriptionService,
        IPathPolicy pathPolicy)
    {
        return new WhisperMcpTools(
            transcriptionService,
            new NotUsedBatchTranscriptionService(),
            new NotUsedValidationService(),
            new NotUsedModelService(),
            new NotUsedConfigurationService(),
            new StubTranscriptReader(),
            pathPolicy,
            Options.Create(new McpOptions()));
    }

    private sealed class CapturingTranscriptionService : ITranscriptionService
    {
        public TranscribeFileRequest? LastRequest { get; private set; }

        public Task<TranscribeFileResult> TranscribeFileAsync(
            TranscribeFileRequest request,
            IProgress<ProgressUpdate>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new TranscribeFileResult(
                Success: true,
                DetectedLanguage: "en",
                ResultFilePath: null,
                AcceptedSegmentCount: 0,
                SkippedSegmentCount: 0,
                Duration: TimeSpan.Zero,
                Warnings: Array.Empty<string>(),
                TranscriptPreview: null));
        }
    }

    private sealed class StubTranscriptReader : ITranscriptReader
    {
        public async Task<TranscriptReadResult> ReadAsync(
            string path,
            int? maxCharacters = null,
            CancellationToken cancellationToken = default)
        {
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            return new TranscriptReadResult(path, content, content.Length, false);
        }
    }

    private sealed class NotUsedTranscriptionService : ITranscriptionService
    {
        public Task<TranscribeFileResult> TranscribeFileAsync(
            TranscribeFileRequest request,
            IProgress<ProgressUpdate>? progress = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NotUsedBatchTranscriptionService : IBatchTranscriptionService
    {
        public Task<BatchTranscribeResult> TranscribeBatchAsync(
            BatchTranscribeRequest request,
            IProgress<ProgressUpdate>? progress = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NotUsedValidationService : IValidationService
    {
        public Task<ValidationResult> ValidateAsync(
            TranscriptionOptions options,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NotUsedModelService : IModelService
    {
        public Task<WhisperFactory> GetOrCreateFactoryAsync(
            TranscriptionOptions options,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ModelInfo InspectModel(TranscriptionOptions options)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class NotUsedConfigurationService : IConfigurationService
    {
        public Task<TranscriptionOptions> LoadAsync(string? configurationPath = null)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<SupportedLanguage> GetSupportedLanguages(string? configurationPath = null)
        {
            throw new NotSupportedException();
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"voxflow-mcp-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
