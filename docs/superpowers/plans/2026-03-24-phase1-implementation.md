# Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure VoxFlow into a shared core library with DI, rewire CLI and MCP hosts, build a macOS Blazor Hybrid desktop app with first-run UX, and update all documentation.

**Architecture:** Extract all business logic from the CLI entry point into `VoxFlow.Core` (class library with public interfaces, internal implementations). Three thin hosts (CLI, MCP, Desktop) compose services via `AddVoxFlowCore()`. Progress flows through `IProgress<ProgressUpdate>`. Desktop uses contextual flow navigation where the screen IS the app state.

**Tech Stack:** .NET 9, MAUI Blazor Hybrid (net9.0-maccatalyst), Whisper.net 1.9.0, xunit 2.9.2, ModelContextProtocol 1.1.0

**Spec:** `docs/superpowers/specs/2026-03-24-phase1-shared-core-desktop-design.md`

---

## Phase A: Solution Restructure and Core Scaffolding

### Task 1: Create VoxFlow.Core class library project

**Files:**
- Create: `src/VoxFlow.Core/VoxFlow.Core.csproj`

- [ ] **Step 1: Create directory structure**

```bash
mkdir -p src/VoxFlow.Core/Interfaces
mkdir -p src/VoxFlow.Core/Services
mkdir -p src/VoxFlow.Core/Models
mkdir -p src/VoxFlow.Core/Configuration
mkdir -p src/VoxFlow.Core/DependencyInjection
```

- [ ] **Step 2: Create VoxFlow.Core.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>VoxFlow.Core</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Whisper.net" Version="1.9.0" />
    <PackageReference Include="Whisper.net.Runtime" Version="1.9.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="9.0.0" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Add Core to solution**

Run: `dotnet sln VoxFlow.sln add src/VoxFlow.Core/VoxFlow.Core.csproj --solution-folder src`
Expected: Project added to solution under `src` folder.

- [ ] **Step 4: Verify solution builds**

Run: `dotnet build VoxFlow.sln`
Expected: Build succeeded (Core is empty but valid).

- [ ] **Step 5: Commit**

```bash
git add src/VoxFlow.Core/ VoxFlow.sln
git commit -m "feat: scaffold VoxFlow.Core class library project"
```

---

### Task 2: Create Core models (DTOs and enums)

**Files:**
- Create: `src/VoxFlow.Core/Models/ProgressUpdate.cs`
- Create: `src/VoxFlow.Core/Models/AppState.cs`
- Create: `src/VoxFlow.Core/Models/TranscribeFileRequest.cs`
- Create: `src/VoxFlow.Core/Models/TranscribeFileResult.cs`
- Create: `src/VoxFlow.Core/Models/BatchTranscribeRequest.cs`
- Create: `src/VoxFlow.Core/Models/BatchTranscribeResult.cs`
- Create: `src/VoxFlow.Core/Models/ValidationResult.cs`
- Create: `src/VoxFlow.Core/Models/ValidationCheck.cs`
- Create: `src/VoxFlow.Core/Models/ModelInfo.cs`
- Create: `src/VoxFlow.Core/Models/TranscriptReadResult.cs`
- Create: `src/VoxFlow.Core/Models/SupportedLanguage.cs`
- Create: `src/VoxFlow.Core/Models/FilteredSegment.cs`
- Create: `src/VoxFlow.Core/Models/SkippedSegment.cs`
- Create: `src/VoxFlow.Core/Models/LanguageSelectionResult.cs`
- Create: `src/VoxFlow.Core/Models/FileProcessingResult.cs`
- Create: `src/VoxFlow.Core/Models/DiscoveredFile.cs`
- Create: `src/VoxFlow.Core/Models/CandidateFilteringResult.cs`

These are public records/enums that evolve from the current internal types in `Contracts/ApplicationContracts.cs` and the internal records scattered across existing services.

- [ ] **Step 1: Create AppState and ProgressUpdate**

`src/VoxFlow.Core/Models/AppState.cs`:
```csharp
namespace VoxFlow.Core.Models;

public enum AppState
{
    NotReady,
    Ready,
    Running,
    Failed,
    Complete
}
```

`src/VoxFlow.Core/Models/ProgressUpdate.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record ProgressUpdate(
    ProgressStage Stage,
    double PercentComplete,
    TimeSpan Elapsed,
    string? Message = null,
    string? CurrentLanguage = null,
    int? BatchFileIndex = null,
    int? BatchFileTotal = null);

public enum ProgressStage
{
    Validating,
    Converting,
    LoadingModel,
    Transcribing,
    Filtering,
    Writing,
    Complete,
    Failed
}
```

- [ ] **Step 2: Create validation models**

`src/VoxFlow.Core/Models/ValidationCheck.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record ValidationCheck(
    string Name,
    ValidationCheckStatus Status,
    string Details);

public enum ValidationCheckStatus
{
    Passed,
    Warning,
    Failed,
    Skipped
}
```

`src/VoxFlow.Core/Models/ValidationResult.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record ValidationResult(
    string Outcome,
    bool CanStart,
    bool HasWarnings,
    string ResolvedConfigurationPath,
    IReadOnlyList<ValidationCheck> Checks);
```

- [ ] **Step 3: Create transcription request/result models**

`src/VoxFlow.Core/Models/SupportedLanguage.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record SupportedLanguage(
    string Code,
    string DisplayName,
    int Priority = 0);
```

`src/VoxFlow.Core/Models/FilteredSegment.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record FilteredSegment(
    TimeSpan Start,
    TimeSpan End,
    string Text,
    double Probability);
```

`src/VoxFlow.Core/Models/SkippedSegment.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record SkippedSegment(
    TimeSpan Start,
    TimeSpan End,
    string Text,
    double Probability,
    SegmentSkipReason Reason);

public enum SegmentSkipReason
{
    EmptyText,
    NoiseMarker,
    BracketedPlaceholder,
    LowProbability,
    LowInformationLong,
    SuspiciousNonSpeech,
    RepetitiveLoop
}
```

`src/VoxFlow.Core/Models/CandidateFilteringResult.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record CandidateFilteringResult(
    IReadOnlyList<FilteredSegment> Accepted,
    IReadOnlyList<SkippedSegment> Skipped);
```

`src/VoxFlow.Core/Models/LanguageSelectionResult.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record LanguageSelectionResult(
    SupportedLanguage Language,
    double Score,
    TimeSpan AudioDuration,
    IReadOnlyList<FilteredSegment> AcceptedSegments,
    IReadOnlyList<SkippedSegment> SkippedSegments,
    string? Warning = null);
```

`src/VoxFlow.Core/Models/TranscribeFileRequest.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record TranscribeFileRequest(
    string InputPath,
    string? ResultFilePath = null,
    string? ConfigurationPath = null,
    IReadOnlyList<string>? ForceLanguages = null,
    bool OverwriteExistingResult = true);
```

`src/VoxFlow.Core/Models/TranscribeFileResult.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record TranscribeFileResult(
    bool Success,
    string? DetectedLanguage,
    string? ResultFilePath,
    int AcceptedSegmentCount,
    int SkippedSegmentCount,
    TimeSpan Duration,
    IReadOnlyList<string> Warnings,
    string? TranscriptPreview);
```

- [ ] **Step 4: Create batch models**

`src/VoxFlow.Core/Models/BatchTranscribeRequest.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record BatchTranscribeRequest(
    string InputDirectory,
    string OutputDirectory,
    string? FilePattern = null,
    string? SummaryFilePath = null,
    bool StopOnFirstError = false,
    bool KeepIntermediateFiles = false,
    string? ConfigurationPath = null,
    int? MaxFiles = null);
```

`src/VoxFlow.Core/Models/BatchTranscribeResult.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record BatchTranscribeResult(
    int TotalFiles,
    int Succeeded,
    int Failed,
    int Skipped,
    string? SummaryFilePath,
    TimeSpan TotalDuration,
    IReadOnlyList<BatchFileResult> Results);

public sealed record BatchFileResult(
    string InputPath,
    string OutputPath,
    string Status,
    string? ErrorMessage,
    TimeSpan Duration,
    string? DetectedLanguage);
```

`src/VoxFlow.Core/Models/FileProcessingResult.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record FileProcessingResult(
    string InputPath,
    string OutputPath,
    FileProcessingStatus Status,
    string? ErrorMessage,
    TimeSpan Duration,
    string? DetectedLanguage);

public enum FileProcessingStatus
{
    Success,
    Failed,
    Skipped
}
```

`src/VoxFlow.Core/Models/DiscoveredFile.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record DiscoveredFile(
    string InputPath,
    string OutputPath,
    string TempWavPath,
    DiscoveryStatus Status,
    string? SkipReason);

public enum DiscoveryStatus
{
    Ready,
    Skipped
}
```

- [ ] **Step 5: Create remaining models**

`src/VoxFlow.Core/Models/ModelInfo.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record ModelInfo(
    string ModelPath,
    string ModelType,
    bool Exists,
    long? FileSizeBytes,
    bool IsLoadable,
    bool NeedsDownload);
```

`src/VoxFlow.Core/Models/TranscriptReadResult.cs`:
```csharp
namespace VoxFlow.Core.Models;

public sealed record TranscriptReadResult(
    string Path,
    string Content,
    long TotalLength,
    bool WasTruncated);
```

- [ ] **Step 6: Verify build**

Run: `dotnet build src/VoxFlow.Core/VoxFlow.Core.csproj`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add src/VoxFlow.Core/Models/
git commit -m "feat: add Core public model types (DTOs, enums, records)"
```

---

### Task 3: Create Core interfaces

**Files:**
- Create: `src/VoxFlow.Core/Interfaces/ITranscriptionService.cs`
- Create: `src/VoxFlow.Core/Interfaces/IBatchTranscriptionService.cs`
- Create: `src/VoxFlow.Core/Interfaces/IValidationService.cs`
- Create: `src/VoxFlow.Core/Interfaces/IConfigurationService.cs`
- Create: `src/VoxFlow.Core/Interfaces/ITranscriptReader.cs`
- Create: `src/VoxFlow.Core/Interfaces/IAudioConversionService.cs`
- Create: `src/VoxFlow.Core/Interfaces/IModelService.cs`
- Create: `src/VoxFlow.Core/Interfaces/ILanguageSelectionService.cs`
- Create: `src/VoxFlow.Core/Interfaces/ITranscriptionFilter.cs`
- Create: `src/VoxFlow.Core/Interfaces/IOutputWriter.cs`
- Create: `src/VoxFlow.Core/Interfaces/IFileDiscoveryService.cs`
- Create: `src/VoxFlow.Core/Interfaces/IBatchSummaryWriter.cs`
- Create: `src/VoxFlow.Core/Interfaces/IWavAudioLoader.cs`

- [ ] **Step 1: Create host-facing interfaces**

`src/VoxFlow.Core/Interfaces/ITranscriptionService.cs`:
```csharp
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Interfaces;

public interface ITranscriptionService
{
    Task<TranscribeFileResult> TranscribeFileAsync(
        TranscribeFileRequest request,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default);
}
```

`src/VoxFlow.Core/Interfaces/IBatchTranscriptionService.cs`:
```csharp
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Interfaces;

public interface IBatchTranscriptionService
{
    Task<BatchTranscribeResult> TranscribeBatchAsync(
        BatchTranscribeRequest request,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default);
}
```

`src/VoxFlow.Core/Interfaces/IValidationService.cs`:
```csharp
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Interfaces;

public interface IValidationService
{
    Task<ValidationResult> ValidateAsync(
        TranscriptionOptions options,
        CancellationToken cancellationToken = default);
}
```

`src/VoxFlow.Core/Interfaces/IConfigurationService.cs`:
```csharp
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Interfaces;

public interface IConfigurationService
{
    Task<TranscriptionOptions> LoadAsync(string? configurationPath = null);
    IReadOnlyList<SupportedLanguage> GetSupportedLanguages(string? configurationPath = null);
}
```

`src/VoxFlow.Core/Interfaces/ITranscriptReader.cs`:
```csharp
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Interfaces;

public interface ITranscriptReader
{
    Task<TranscriptReadResult> ReadAsync(
        string path,
        int? maxCharacters = null,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Create internal pipeline interfaces**

`src/VoxFlow.Core/Interfaces/IAudioConversionService.cs`:
```csharp
using VoxFlow.Core.Configuration;

namespace VoxFlow.Core.Interfaces;

public interface IAudioConversionService
{
    Task ConvertToWavAsync(
        string inputPath,
        string outputPath,
        TranscriptionOptions options,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateFfmpegAsync(
        TranscriptionOptions options,
        CancellationToken cancellationToken = default);
}
```

`src/VoxFlow.Core/Interfaces/IModelService.cs`:
```csharp
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;
using Whisper.net;

namespace VoxFlow.Core.Interfaces;

public interface IModelService
{
    Task<WhisperFactory> GetOrCreateFactoryAsync(
        TranscriptionOptions options,
        CancellationToken cancellationToken = default);

    ModelInfo InspectModel(TranscriptionOptions options);
}
```

`src/VoxFlow.Core/Interfaces/ILanguageSelectionService.cs`:
```csharp
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;
using Whisper.net;

namespace VoxFlow.Core.Interfaces;

public interface ILanguageSelectionService
{
    Task<LanguageSelectionResult> SelectBestCandidateAsync(
        WhisperFactory factory,
        float[] audioSamples,
        TranscriptionOptions options,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default);
}
```

`src/VoxFlow.Core/Interfaces/ITranscriptionFilter.cs`:
```csharp
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;
using Whisper.net.Ggml;

namespace VoxFlow.Core.Interfaces;

public interface ITranscriptionFilter
{
    CandidateFilteringResult FilterSegments(
        SupportedLanguage language,
        IReadOnlyList<SegmentData> segments,
        TranscriptionOptions options);
}
```

`src/VoxFlow.Core/Interfaces/IOutputWriter.cs`:
```csharp
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Interfaces;

public interface IOutputWriter
{
    Task WriteAsync(
        string outputPath,
        IReadOnlyList<FilteredSegment> segments,
        CancellationToken cancellationToken = default);

    string BuildOutputText(IReadOnlyList<FilteredSegment> segments);
}
```

`src/VoxFlow.Core/Interfaces/IFileDiscoveryService.cs`:
```csharp
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Interfaces;

public interface IFileDiscoveryService
{
    IReadOnlyList<DiscoveredFile> DiscoverInputFiles(BatchOptions batchOptions);
}
```

`src/VoxFlow.Core/Interfaces/IBatchSummaryWriter.cs`:
```csharp
using VoxFlow.Core.Models;

namespace VoxFlow.Core.Interfaces;

public interface IBatchSummaryWriter
{
    Task WriteAsync(
        string summaryPath,
        IReadOnlyList<FileProcessingResult> results,
        CancellationToken cancellationToken = default);
}
```

`src/VoxFlow.Core/Interfaces/IWavAudioLoader.cs`:
```csharp
using VoxFlow.Core.Configuration;

namespace VoxFlow.Core.Interfaces;

public interface IWavAudioLoader
{
    Task<float[]> LoadSamplesAsync(
        string wavPath,
        TranscriptionOptions options,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/VoxFlow.Core/VoxFlow.Core.csproj`
Expected: Build will fail because `TranscriptionOptions`, `BatchOptions`, and `SegmentData` do not exist yet in Core. This is expected — they will be moved in the next tasks.

Note: At this point, `TranscriptionOptions` still lives in the root project. We need to move it to Core before the build will pass. Proceed to Task 4.

- [ ] **Step 4: Commit (even with build errors — interfaces define the contract)**

```bash
git add src/VoxFlow.Core/Interfaces/
git commit -m "feat: add Core public interfaces (host-facing and internal pipeline)"
```

---

### Task 4: Move TranscriptionOptions to Core

**Files:**
- Move: `Configuration/TranscriptionOptions.cs` → `src/VoxFlow.Core/Configuration/TranscriptionOptions.cs`

The existing `TranscriptionOptions` is a sealed class with 45+ properties loaded from `appsettings.json`. It needs to be moved to Core with its namespace changed to `VoxFlow.Core.Configuration` and its visibility changed from `internal` to `public`.

- [ ] **Step 1: Copy TranscriptionOptions.cs to Core**

Copy the file from `Configuration/TranscriptionOptions.cs` to `src/VoxFlow.Core/Configuration/TranscriptionOptions.cs`.

Key changes needed:
- Add `namespace VoxFlow.Core.Configuration;`
- Change class visibility from `internal sealed` to `public sealed`
- Change all nested types (like `BatchOptions`, `StartupValidationOptions`, `ConsoleProgressOptions`, `SupportedLanguageConfig`) to `public`
- Keep all existing logic (Load, LoadFromPath, validation, normalization)

- [ ] **Step 2: Update any internal types used by TranscriptionOptions**

Check for internal types referenced by TranscriptionOptions (e.g., `SupportedLanguage` record, batch config types). These must also become public in Core. Cross-reference with the `SupportedLanguage` record created in Task 2 — the Core model should be used instead.

- [ ] **Step 3: Verify Core builds**

Run: `dotnet build src/VoxFlow.Core/VoxFlow.Core.csproj`
Expected: Build succeeded. All interfaces can now resolve `TranscriptionOptions` and `BatchOptions`.

- [ ] **Step 4: Commit**

```bash
git add src/VoxFlow.Core/Configuration/
git commit -m "feat: move TranscriptionOptions to VoxFlow.Core"
```

---

### Task 5: Move service implementations to Core (static → instance)

**Files:**
- Move + convert: `Services/StartupValidationService.cs` → `src/VoxFlow.Core/Services/ValidationService.cs`
- Move + convert: `Audio/AudioConversionService.cs` → `src/VoxFlow.Core/Services/AudioConversionService.cs`
- Move + convert: `Audio/WavAudioLoader.cs` → `src/VoxFlow.Core/Services/WavAudioLoader.cs`
- Move + convert: `Services/ModelService.cs` → `src/VoxFlow.Core/Services/ModelService.cs`
- Move + convert: `Services/LanguageSelectionService.cs` → `src/VoxFlow.Core/Services/LanguageSelectionService.cs`
- Move + convert: `Processing/TranscriptionFilter.cs` → `src/VoxFlow.Core/Services/TranscriptionFilter.cs`
- Move + convert: `Services/OutputWriter.cs` → `src/VoxFlow.Core/Services/OutputWriter.cs`
- Move + convert: `Services/FileDiscoveryService.cs` → `src/VoxFlow.Core/Services/FileDiscoveryService.cs`
- Move + convert: `Services/BatchSummaryWriter.cs` → `src/VoxFlow.Core/Services/BatchSummaryWriter.cs`

For each service, the conversion pattern is:
1. Change namespace to `VoxFlow.Core.Services`
2. Change from `internal static class` to `internal sealed class` implementing the interface
3. Remove `static` from all methods
4. Replace direct `Console.WriteLine` calls with `IProgress<ProgressUpdate>` where applicable
5. Keep all existing logic — just re-house it

- [ ] **Step 1: Move and convert pure-function services first**

Start with services that have no external dependencies and are pure functions:
- `TranscriptionFilter` → implements `ITranscriptionFilter`
- `OutputWriter` → implements `IOutputWriter`
- `FileDiscoveryService` → implements `IFileDiscoveryService`
- `BatchSummaryWriter` → implements `IBatchSummaryWriter`
- `WavAudioLoader` → implements `IWavAudioLoader`

Pattern for each:
```csharp
namespace VoxFlow.Core.Services;

using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Models;

internal sealed class TranscriptionFilter : ITranscriptionFilter
{
    // All existing static method bodies become instance methods
    // Same logic, just not static
}
```

- [ ] **Step 2: Move and convert services with external dependencies**

These services interact with the file system, ffmpeg, or Whisper.net:
- `ValidationService` (implements `IValidationService`) — probe file system, ffmpeg, Whisper runtime
- `AudioConversionService` (implements `IAudioConversionService`) — spawn ffmpeg process
- `ModelService` (implements `IModelService`) — load/download models, add singleton caching per spec

For `ModelService`, add the `GetOrCreateFactoryAsync` caching pattern from the spec:
```csharp
internal sealed class ModelService : IModelService
{
    private WhisperFactory? _cachedFactory;
    private string? _cachedModelPath;

    public async Task<WhisperFactory> GetOrCreateFactoryAsync(
        TranscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        if (_cachedFactory != null && _cachedModelPath == options.ModelFilePath)
            return _cachedFactory;

        _cachedFactory = await CreateFactoryInternalAsync(options, cancellationToken);
        _cachedModelPath = options.ModelFilePath;
        return _cachedFactory;
    }

    // Existing CreateFactoryAsync logic moves to CreateFactoryInternalAsync
}
```

- [ ] **Step 3: Move and convert LanguageSelectionService**

This service depends on `ITranscriptionFilter` and needs `IProgress<ProgressUpdate>`. Inject both via constructor:
```csharp
internal sealed class LanguageSelectionService : ILanguageSelectionService
{
    private readonly ITranscriptionFilter _filter;
    private readonly IWavAudioLoader _wavLoader;

    public LanguageSelectionService(ITranscriptionFilter filter, IWavAudioLoader wavLoader)
    {
        _filter = filter;
        _wavLoader = wavLoader;
    }

    // Move existing static methods to instance methods
    // Replace TranscriptionFilter.FilterSegments() calls with _filter.FilterSegments()
}
```

- [ ] **Step 4: Create ConfigurationService and TranscriptReader**

`src/VoxFlow.Core/Services/ConfigurationService.cs`:
```csharp
namespace VoxFlow.Core.Services;

using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;

internal sealed class ConfigurationService : IConfigurationService
{
    public Task<TranscriptionOptions> LoadAsync(string? configurationPath = null)
    {
        var options = configurationPath != null
            ? TranscriptionOptions.LoadFromPath(configurationPath)
            : TranscriptionOptions.Load();
        return Task.FromResult(options);
    }

    public IReadOnlyList<SupportedLanguage> GetSupportedLanguages(string? configurationPath = null)
    {
        var options = configurationPath != null
            ? TranscriptionOptions.LoadFromPath(configurationPath)
            : TranscriptionOptions.Load();

        return options.SupportedLanguages
            .Select((lang, i) => new SupportedLanguage(lang.Code, lang.DisplayName, i))
            .ToList();
    }
}
```

`src/VoxFlow.Core/Services/TranscriptReader.cs`:
```csharp
namespace VoxFlow.Core.Services;

using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;

internal sealed class TranscriptReader : ITranscriptReader
{
    public async Task<TranscriptReadResult> ReadAsync(
        string path, int? maxCharacters = null,
        CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(path, cancellationToken);
        var totalLength = content.Length;
        var wasTruncated = false;

        if (maxCharacters.HasValue && content.Length > maxCharacters.Value)
        {
            content = content[..maxCharacters.Value];
            wasTruncated = true;
        }

        return new TranscriptReadResult(path, content, totalLength, wasTruncated);
    }
}
```

- [ ] **Step 5: Verify Core builds**

Run: `dotnet build src/VoxFlow.Core/VoxFlow.Core.csproj`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/VoxFlow.Core/Services/
git commit -m "feat: move all services to Core as instance-based implementations"
```

---

### Task 6: Create orchestrator services (TranscriptionService and BatchTranscriptionService)

**Files:**
- Create: `src/VoxFlow.Core/Services/TranscriptionService.cs`
- Create: `src/VoxFlow.Core/Services/BatchTranscriptionService.cs`

These orchestrate the full pipeline, replacing the logic currently in `Program.RunSingleFileAsync` and `Program.RunBatchAsync`.

- [ ] **Step 1: Create TranscriptionService**

`src/VoxFlow.Core/Services/TranscriptionService.cs`:
```csharp
namespace VoxFlow.Core.Services;

using System.Diagnostics;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;

internal sealed class TranscriptionService : ITranscriptionService
{
    private readonly IConfigurationService _configService;
    private readonly IValidationService _validationService;
    private readonly IAudioConversionService _audioConversion;
    private readonly IModelService _modelService;
    private readonly IWavAudioLoader _wavLoader;
    private readonly ILanguageSelectionService _languageSelection;
    private readonly IOutputWriter _outputWriter;

    public TranscriptionService(
        IConfigurationService configService,
        IValidationService validationService,
        IAudioConversionService audioConversion,
        IModelService modelService,
        IWavAudioLoader wavLoader,
        ILanguageSelectionService languageSelection,
        IOutputWriter outputWriter)
    {
        _configService = configService;
        _validationService = validationService;
        _audioConversion = audioConversion;
        _modelService = modelService;
        _wavLoader = wavLoader;
        _languageSelection = languageSelection;
        _outputWriter = outputWriter;
    }

    public async Task<TranscribeFileResult> TranscribeFileAsync(
        TranscribeFileRequest request,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();

        // 1. Load config
        var options = await _configService.LoadAsync(request.ConfigurationPath);

        var inputPath = request.InputPath;
        var resultPath = request.ResultFilePath ?? options.ResultFilePath;
        var wavPath = options.WavFilePath;

        // 2. Validate
        progress?.Report(new ProgressUpdate(ProgressStage.Validating, 0, stopwatch.Elapsed, "Validating environment..."));

        if (options.StartupValidation.Enabled)
        {
            var validation = await _validationService.ValidateAsync(options, cancellationToken);
            if (!validation.CanStart)
            {
                return new TranscribeFileResult(
                    false, null, null, 0, 0, stopwatch.Elapsed,
                    validation.Checks.Where(c => c.Status == ValidationCheckStatus.Failed).Select(c => c.Details).ToList(),
                    null);
            }
            if (validation.HasWarnings)
            {
                warnings.AddRange(validation.Checks
                    .Where(c => c.Status == ValidationCheckStatus.Warning)
                    .Select(c => c.Details));
            }
        }

        // 3. Convert audio
        progress?.Report(new ProgressUpdate(ProgressStage.Converting, 10, stopwatch.Elapsed, "Converting audio..."));
        await _audioConversion.ConvertToWavAsync(inputPath, wavPath, options, cancellationToken);

        // 4. Load model
        progress?.Report(new ProgressUpdate(ProgressStage.LoadingModel, 20, stopwatch.Elapsed, "Loading model..."));
        var factory = await _modelService.GetOrCreateFactoryAsync(options, cancellationToken);

        // 5. Load WAV samples
        var audioSamples = await _wavLoader.LoadSamplesAsync(wavPath, options, cancellationToken);

        // 6. Transcribe + select language
        progress?.Report(new ProgressUpdate(ProgressStage.Transcribing, 30, stopwatch.Elapsed, "Transcribing..."));
        var selectionResult = await _languageSelection.SelectBestCandidateAsync(
            factory, audioSamples, options, progress, cancellationToken);

        if (selectionResult.Warning != null)
            warnings.Add(selectionResult.Warning);

        // 7. Write output
        progress?.Report(new ProgressUpdate(ProgressStage.Writing, 90, stopwatch.Elapsed, "Writing transcript..."));
        await _outputWriter.WriteAsync(resultPath, selectionResult.AcceptedSegments, cancellationToken);

        stopwatch.Stop();

        // 8. Build preview
        var preview = _outputWriter.BuildOutputText(
            selectionResult.AcceptedSegments.Take(10).ToList());

        progress?.Report(new ProgressUpdate(ProgressStage.Complete, 100, stopwatch.Elapsed, "Complete"));

        return new TranscribeFileResult(
            true,
            $"{selectionResult.Language.DisplayName} ({selectionResult.Language.Code})",
            resultPath,
            selectionResult.AcceptedSegments.Count,
            selectionResult.SkippedSegments.Count,
            stopwatch.Elapsed,
            warnings,
            preview);
    }
}
```

- [ ] **Step 2: Create BatchTranscriptionService**

`src/VoxFlow.Core/Services/BatchTranscriptionService.cs`:
```csharp
namespace VoxFlow.Core.Services;

using System.Diagnostics;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;

internal sealed class BatchTranscriptionService : IBatchTranscriptionService
{
    private readonly IConfigurationService _configService;
    private readonly IValidationService _validationService;
    private readonly IFileDiscoveryService _fileDiscovery;
    private readonly IAudioConversionService _audioConversion;
    private readonly IModelService _modelService;
    private readonly IWavAudioLoader _wavLoader;
    private readonly ILanguageSelectionService _languageSelection;
    private readonly IOutputWriter _outputWriter;
    private readonly IBatchSummaryWriter _summaryWriter;

    public BatchTranscriptionService(
        IConfigurationService configService,
        IValidationService validationService,
        IFileDiscoveryService fileDiscovery,
        IAudioConversionService audioConversion,
        IModelService modelService,
        IWavAudioLoader wavLoader,
        ILanguageSelectionService languageSelection,
        IOutputWriter outputWriter,
        IBatchSummaryWriter summaryWriter)
    {
        _configService = configService;
        _validationService = validationService;
        _fileDiscovery = fileDiscovery;
        _audioConversion = audioConversion;
        _modelService = modelService;
        _wavLoader = wavLoader;
        _languageSelection = languageSelection;
        _outputWriter = outputWriter;
        _summaryWriter = summaryWriter;
    }

    public async Task<BatchTranscribeResult> TranscribeBatchAsync(
        BatchTranscribeRequest request,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var options = await _configService.LoadAsync(request.ConfigurationPath);
        var batchOptions = options.Batch;

        // 1. Validate
        if (options.StartupValidation.Enabled)
        {
            var validation = await _validationService.ValidateAsync(options, cancellationToken);
            if (!validation.CanStart)
            {
                return new BatchTranscribeResult(0, 0, 0, 0, null, totalStopwatch.Elapsed, new List<BatchFileResult>());
            }
        }

        // 2. Create factory once (ADR-010, ADR-011)
        progress?.Report(new ProgressUpdate(ProgressStage.LoadingModel, 5, totalStopwatch.Elapsed, "Loading model..."));
        var factory = await _modelService.GetOrCreateFactoryAsync(options, cancellationToken);

        // 3. Discover files
        var discoveredFiles = _fileDiscovery.DiscoverInputFiles(batchOptions);
        var results = new List<BatchFileResult>(discoveredFiles.Count);

        // 4. Process each file
        for (var i = 0; i < discoveredFiles.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = discoveredFiles[i];
            var pct = (int)(10 + (80.0 * i / discoveredFiles.Count));

            if (file.Status == DiscoveryStatus.Skipped)
            {
                results.Add(new BatchFileResult(
                    file.InputPath, file.OutputPath, "Skipped",
                    file.SkipReason, TimeSpan.Zero, null));
                continue;
            }

            progress?.Report(new ProgressUpdate(
                ProgressStage.Transcribing, pct, totalStopwatch.Elapsed,
                $"[{i + 1}/{discoveredFiles.Count}] {Path.GetFileName(file.InputPath)}"));

            var fileStopwatch = Stopwatch.StartNew();
            try
            {
                await _audioConversion.ConvertToWavAsync(file.InputPath, file.TempWavPath, options, cancellationToken);
                var samples = await _wavLoader.LoadSamplesAsync(file.TempWavPath, options, cancellationToken);
                var selection = await _languageSelection.SelectBestCandidateAsync(factory, samples, options, null, cancellationToken);
                await _outputWriter.WriteAsync(file.OutputPath, selection.AcceptedSegments, cancellationToken);

                fileStopwatch.Stop();
                results.Add(new BatchFileResult(
                    file.InputPath, file.OutputPath, "Success",
                    null, fileStopwatch.Elapsed,
                    $"{selection.Language.DisplayName} ({selection.Language.Code})"));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                fileStopwatch.Stop();
                results.Add(new BatchFileResult(
                    file.InputPath, file.OutputPath, "Failed",
                    ex.Message, fileStopwatch.Elapsed, null));

                if (batchOptions.StopOnFirstError) break;
            }
            finally
            {
                CleanupTempWav(file.TempWavPath, batchOptions.KeepIntermediateFiles);
            }
        }

        // 5. Write summary — convert BatchFileResult → FileProcessingResult for summary writer
        var fileResults = results.Select(r => new FileProcessingResult(
            r.InputPath, r.OutputPath,
            r.Status switch { "Success" => FileProcessingStatus.Success, "Failed" => FileProcessingStatus.Failed, _ => FileProcessingStatus.Skipped },
            r.ErrorMessage, r.Duration, r.DetectedLanguage)).ToList();
        await _summaryWriter.WriteAsync(batchOptions.SummaryFilePath, fileResults, cancellationToken);

        totalStopwatch.Stop();
        var succeeded = results.Count(r => r.Status == "Success");
        var failed = results.Count(r => r.Status == "Failed");
        var skipped = results.Count(r => r.Status == "Skipped");

        progress?.Report(new ProgressUpdate(ProgressStage.Complete, 100, totalStopwatch.Elapsed,
            $"Batch complete: {succeeded} succeeded, {failed} failed, {skipped} skipped"));

        return new BatchTranscribeResult(
            results.Count, succeeded, failed, skipped,
            batchOptions.SummaryFilePath, totalStopwatch.Elapsed, results);
    }

    private static void CleanupTempWav(string wavPath, bool keepIntermediateFiles)
    {
        if (keepIntermediateFiles) return;
        try { if (File.Exists(wavPath)) File.Delete(wavPath); }
        catch { /* best-effort */ }
    }
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/VoxFlow.Core/VoxFlow.Core.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/VoxFlow.Core/Services/TranscriptionService.cs src/VoxFlow.Core/Services/BatchTranscriptionService.cs
git commit -m "feat: add TranscriptionService and BatchTranscriptionService orchestrators"
```

---

### Task 7: Create DI registration extension method

**Files:**
- Create: `src/VoxFlow.Core/DependencyInjection/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Write failing test for DI registration**

Create `tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\VoxFlow.Core\VoxFlow.Core.csproj" />
  </ItemGroup>
</Project>
```

Add to solution: `dotnet sln VoxFlow.sln add tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj --solution-folder tests`

Create `tests/VoxFlow.Core.Tests/DependencyInjectionTests.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using VoxFlow.Core.DependencyInjection;
using VoxFlow.Core.Interfaces;

namespace VoxFlow.Core.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddVoxFlowCore_RegistersAllInterfaces()
    {
        var services = new ServiceCollection();
        services.AddVoxFlowCore();
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IConfigurationService>());
        Assert.NotNull(provider.GetService<IValidationService>());
        Assert.NotNull(provider.GetService<IAudioConversionService>());
        Assert.NotNull(provider.GetService<IModelService>());
        Assert.NotNull(provider.GetService<IWavAudioLoader>());
        Assert.NotNull(provider.GetService<ILanguageSelectionService>());
        Assert.NotNull(provider.GetService<ITranscriptionFilter>());
        Assert.NotNull(provider.GetService<IOutputWriter>());
        Assert.NotNull(provider.GetService<IFileDiscoveryService>());
        Assert.NotNull(provider.GetService<IBatchSummaryWriter>());
        Assert.NotNull(provider.GetService<ITranscriptReader>());
        Assert.NotNull(provider.GetService<ITranscriptionService>());
        Assert.NotNull(provider.GetService<IBatchTranscriptionService>());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/VoxFlow.Core.Tests/ --filter DependencyInjectionTests`
Expected: FAIL — `AddVoxFlowCore` does not exist yet.

- [ ] **Step 3: Implement ServiceCollectionExtensions**

`src/VoxFlow.Core/DependencyInjection/ServiceCollectionExtensions.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Services;

namespace VoxFlow.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVoxFlowCore(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IValidationService, ValidationService>();
        services.AddSingleton<IAudioConversionService, AudioConversionService>();
        services.AddSingleton<IModelService, ModelService>();
        services.AddSingleton<IWavAudioLoader, WavAudioLoader>();
        services.AddSingleton<ILanguageSelectionService, LanguageSelectionService>();
        services.AddSingleton<ITranscriptionFilter, TranscriptionFilter>();
        services.AddSingleton<IOutputWriter, OutputWriter>();
        services.AddSingleton<IFileDiscoveryService, FileDiscoveryService>();
        services.AddSingleton<IBatchSummaryWriter, BatchSummaryWriter>();
        services.AddSingleton<ITranscriptReader, TranscriptReader>();
        services.AddSingleton<ITranscriptionService, TranscriptionService>();
        services.AddSingleton<IBatchTranscriptionService, BatchTranscriptionService>();
        return services;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/VoxFlow.Core.Tests/ --filter DependencyInjectionTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/VoxFlow.Core/DependencyInjection/ tests/VoxFlow.Core.Tests/
git commit -m "feat: add AddVoxFlowCore DI registration with passing test"
```

---

## Phase B: Migrate Existing Tests to Core.Tests

### Task 8: Migrate unit tests to VoxFlow.Core.Tests

**Files:**
- Move: `tests/VoxFlow.UnitTests/*.cs` → `tests/VoxFlow.Core.Tests/`
- Move: `tests/TestSupport/*.cs` → `tests/TestSupport/` (stays in place, update link)
- Modify: `tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj` — add TestSupport link

- [ ] **Step 1: Copy all unit test files to Core.Tests**

Copy these files from `tests/VoxFlow.UnitTests/` to `tests/VoxFlow.Core.Tests/`:
- `TranscriptionOptionsTests.cs`
- `TranscriptionFilterTests.cs`
- `WavAudioLoaderTests.cs`
- `StartupValidationServiceTests.cs`
- `StartupValidationReportTests.cs`
- `LanguageSelectionDecisionTests.cs`
- `OutputWriterTests.cs`
- `FileDiscoveryServiceTests.cs`
- `BatchSummaryWriterTests.cs`
- `BatchConfigurationTests.cs`

- [ ] **Step 2: Update namespaces and usings in each test file**

Each test file needs:
- `using VoxFlow.Core.Services;` (or remove if testing via interfaces)
- `using VoxFlow.Core.Configuration;`
- `using VoxFlow.Core.Models;`
- Update any direct static method calls to go through interface or make services `InternalsVisibleTo` test project

Add to `src/VoxFlow.Core/Properties/AssemblyInfo.cs`:
```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("VoxFlow.Core.Tests")]
```

- [ ] **Step 3: Update Core.Tests csproj to include TestSupport**

Add to `tests/VoxFlow.Core.Tests/VoxFlow.Core.Tests.csproj`:
```xml
<ItemGroup>
    <Compile Include="..\TestSupport\*.cs" Link="TestSupport\%(Filename)%(Extension)" />
</ItemGroup>
```

- [ ] **Step 4: Run all Core tests**

Run: `dotnet test tests/VoxFlow.Core.Tests/`
Expected: All tests pass. Fix any namespace or accessibility issues.

- [ ] **Step 5: Commit**

```bash
git add tests/VoxFlow.Core.Tests/ src/VoxFlow.Core/Properties/
git commit -m "feat: migrate unit tests to VoxFlow.Core.Tests"
```

---

## Phase C: CLI Host Migration

### Task 9: Create VoxFlow.Cli project and rewire CLI

**Files:**
- Create: `src/VoxFlow.Cli/VoxFlow.Cli.csproj`
- Create: `src/VoxFlow.Cli/Program.cs`
- Create: `src/VoxFlow.Cli/CliProgressHandler.cs`
- Create: `src/VoxFlow.Cli/ConsoleValidationReporter.cs`
- Move: `Services/ConsoleProgressService.cs` → `src/VoxFlow.Cli/` (CLI-specific)
- Move: existing `StartupValidationConsoleReporter` → `src/VoxFlow.Cli/ConsoleValidationReporter.cs`

- [ ] **Step 1: Create VoxFlow.Cli.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>VoxFlow.Cli</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\VoxFlow.Core\VoxFlow.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
```

Add to solution: `dotnet sln VoxFlow.sln add src/VoxFlow.Cli/VoxFlow.Cli.csproj --solution-folder src`

- [ ] **Step 2: Create thin CLI Program.cs**

`src/VoxFlow.Cli/Program.cs` — the thin host from the spec. Composes DI, delegates to Core services, maps results to exit codes. Handles Ctrl+C cancellation. See spec Section 4 for full code.

- [ ] **Step 3: Create CliProgressHandler**

`src/VoxFlow.Cli/CliProgressHandler.cs`:
Implements `IProgress<ProgressUpdate>`. Wraps the existing `ConsoleProgressService` logic for ANSI spinner + progress bar rendering.

- [ ] **Step 4: Create ConsoleValidationReporter**

`src/VoxFlow.Cli/ConsoleValidationReporter.cs`:
Moves the existing `StartupValidationConsoleReporter` from root project. Renders validation results with color-coded ANSI output.

- [ ] **Step 5: Copy appsettings.json to CLI project**

Copy the existing `appsettings.json` to `src/VoxFlow.Cli/appsettings.json`.

- [ ] **Step 6: Verify CLI builds and runs**

Run: `dotnet build src/VoxFlow.Cli/VoxFlow.Cli.csproj`
Expected: Build succeeded.

Run: `dotnet run --project src/VoxFlow.Cli/`
Expected: Same behavior as the original CLI — startup validation runs, transcription executes if configured.

- [ ] **Step 7: Migrate CLI end-to-end tests**

Move `tests/VoxFlow.EndToEndTests/` to `tests/VoxFlow.Cli.Tests/`, update project references to point to `src/VoxFlow.Cli/VoxFlow.Cli.csproj`. Update `TestProjectPaths` to find the new CLI executable location.

Run: `dotnet test tests/VoxFlow.Cli.Tests/`
Expected: All E2E tests pass.

- [ ] **Step 8: Commit**

```bash
git add src/VoxFlow.Cli/ tests/VoxFlow.Cli.Tests/
git commit -m "feat: create VoxFlow.Cli thin host with DI, migrate E2E tests"
```

---

## Phase D: MCP Server Migration

### Task 10: Rewire MCP server to use Core interfaces

**Files:**
- Modify: `src/WhisperNET.McpServer/WhisperNET.McpServer.csproj` → rename to `src/VoxFlow.McpServer/VoxFlow.McpServer.csproj`
- Modify: `src/VoxFlow.McpServer/Program.cs` — replace facade registration with `AddVoxFlowCore()`
- Modify: `src/VoxFlow.McpServer/Tools/WhisperMcpTools.cs` — inject Core interfaces
- Delete: `Facades/` directory (all 6 files)
- Delete: `Contracts/ApplicationContracts.cs` (DTOs now in Core)
- Remove: `InternalsVisibleTo("WhisperNET.McpServer")` from AssemblyInfo.cs

- [ ] **Step 1: Rename MCP project**

```bash
mv src/WhisperNET.McpServer src/VoxFlow.McpServer
```

Update `VoxFlow.McpServer.csproj`:
- Change `<RootNamespace>` to `VoxFlow.McpServer`
- Change `<ProjectReference>` from `..\..\VoxFlow.csproj` to `..\VoxFlow.Core\VoxFlow.Core.csproj`
- Add `Microsoft.Extensions.DependencyInjection` package reference

Update solution references.

- [ ] **Step 2: Simplify Program.cs**

Replace facade registrations with:
```csharp
builder.Services.AddVoxFlowCore();
builder.Services.AddSingleton<IPathPolicy>(sp => { /* existing PathPolicy setup */ });
```

Remove all `AddSingleton<IStartupValidationFacade>`, `AddSingleton<ITranscriptionFacade>`, etc.

- [ ] **Step 3: Update WhisperMcpTools to inject Core interfaces**

Replace constructor parameters from facades to Core interfaces:
```csharp
public sealed class WhisperMcpTools(
    ITranscriptionService transcriptionService,
    IBatchTranscriptionService batchService,
    IValidationService validationService,
    IModelService modelService,
    IConfigurationService configService,
    ITranscriptReader transcriptReader,
    IPathPolicy pathPolicy)
```

Update each tool method to call Core interfaces instead of facades.

- [ ] **Step 4: Update MCP prompts and resources**

Update namespace references. Resources like `get_effective_config` now use `IConfigurationService` instead of facades.

- [ ] **Step 5: Move PathPolicy to MCP project if not already there**

`Security/PathPolicy.cs` and `IPathPolicy` stay in the MCP project — they are MCP-specific security boundaries, not Core.

- [ ] **Step 6: Delete old facade and contract files**

```bash
rm -rf Facades/
rm Contracts/ApplicationContracts.cs
```

Remove `InternalsVisibleTo("WhisperNET.McpServer")` from the old AssemblyInfo.cs.

- [ ] **Step 7: Migrate MCP tests**

Move `tests/WhisperNET.McpServer.Tests/` to `tests/VoxFlow.McpServer.Tests/`. Update project references. Remove `ApplicationContractTests.cs` and `FacadeTests.cs` (these types no longer exist). Update `PathPolicyTests` and `McpConfigurationTests` for new namespaces.

Run: `dotnet test tests/VoxFlow.McpServer.Tests/`
Expected: All remaining tests pass.

- [ ] **Step 8: Commit**

```bash
git add src/VoxFlow.McpServer/ tests/VoxFlow.McpServer.Tests/
git rm -r Facades/ Contracts/ src/WhisperNET.McpServer/ tests/WhisperNET.McpServer.Tests/
git commit -m "feat: rewire MCP server to Core interfaces, eliminate facades"
```

---

## Phase E: Clean Up Old Root Project

### Task 11: Remove old VoxFlow.csproj and root source files

**Files:**
- Delete: `VoxFlow.csproj`
- Delete: `Program.cs`
- Delete: `Configuration/TranscriptionOptions.cs` (now in Core)
- Delete: `Services/*.cs` (now in Core)
- Delete: `Audio/*.cs` (now in Core)
- Delete: `Processing/*.cs` (now in Core)
- Delete: `Properties/AssemblyInfo.cs` (new one in Core)
- Delete: `tests/VoxFlow.UnitTests/` (migrated to Core.Tests)
- Delete: `tests/VoxFlow.EndToEndTests/` (migrated to Cli.Tests)
- Update: `VoxFlow.sln` — remove old project references

- [ ] **Step 1: Remove old projects from solution**

```bash
dotnet sln VoxFlow.sln remove VoxFlow.csproj
dotnet sln VoxFlow.sln remove tests/VoxFlow.UnitTests/VoxFlow.UnitTests.csproj
dotnet sln VoxFlow.sln remove tests/VoxFlow.EndToEndTests/VoxFlow.EndToEndTests.csproj
```

- [ ] **Step 2: Delete old source directories**

```bash
rm VoxFlow.csproj Program.cs
rm -rf Configuration/ Services/ Audio/ Processing/ Properties/ Facades/ Contracts/ Security/
rm -rf tests/VoxFlow.UnitTests/ tests/VoxFlow.EndToEndTests/
```

Keep: `appsettings.json` (at root for CLI development convenience), `models/`, `artifacts/`, `docs/`, `tests/TestSupport/`

- [ ] **Step 3: Full solution build**

Run: `dotnet build VoxFlow.sln`
Expected: All projects build successfully.

- [ ] **Step 4: Run all tests**

Run: `dotnet test VoxFlow.sln`
Expected: All tests pass across Core.Tests, Cli.Tests, McpServer.Tests.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "chore: remove old root project, complete solution restructure"
```

---

## Phase F: Desktop App

### Task 12: Create VoxFlow.Desktop MAUI Blazor Hybrid project

**Files:**
- Create: `src/VoxFlow.Desktop/VoxFlow.Desktop.csproj`
- Create: `src/VoxFlow.Desktop/MauiProgram.cs`
- Create: `src/VoxFlow.Desktop/MainPage.xaml`
- Create: `src/VoxFlow.Desktop/MainPage.xaml.cs`
- Create: `src/VoxFlow.Desktop/App.xaml`
- Create: `src/VoxFlow.Desktop/App.xaml.cs`

- [ ] **Step 1: Create MAUI Blazor Hybrid project**

```bash
dotnet new maui-blazor -n VoxFlow.Desktop -o src/VoxFlow.Desktop --framework net9.0
```

Then edit the generated `.csproj` to:
- Set `<TargetFrameworks>net9.0-maccatalyst</TargetFrameworks>` (macOS only for Phase 1)
- Add `<ProjectReference Include="..\VoxFlow.Core\VoxFlow.Core.csproj" />`
- Remove iOS/Android/Windows target frameworks

- [ ] **Step 2: Update MauiProgram.cs**

Replace the generated composition root with:
```csharp
using VoxFlow.Core.DependencyInjection;
using VoxFlow.Desktop.ViewModels;

namespace VoxFlow.Desktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddVoxFlowCore();
        builder.Services.AddSingleton<AppViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();

        return builder.Build();
    }
}
```

- [ ] **Step 3: Add to solution**

Run: `dotnet sln VoxFlow.sln add src/VoxFlow.Desktop/VoxFlow.Desktop.csproj --solution-folder src`

- [ ] **Step 4: Verify build**

Run: `dotnet build src/VoxFlow.Desktop/VoxFlow.Desktop.csproj`
Expected: Build succeeded (may have warnings for missing Blazor components — that's fine).

- [ ] **Step 5: Commit**

```bash
git add src/VoxFlow.Desktop/ VoxFlow.sln
git commit -m "feat: scaffold VoxFlow.Desktop MAUI Blazor Hybrid project"
```

---

### Task 13: Create AppViewModel (state machine)

**Files:**
- Create: `src/VoxFlow.Desktop/ViewModels/AppViewModel.cs`

- [ ] **Step 1: Write failing test for state machine**

Create `tests/VoxFlow.Desktop.Tests/VoxFlow.Desktop.Tests.csproj` and `tests/VoxFlow.Desktop.Tests/AppViewModelTests.cs`:

Test that:
- Initial state after `InitializeAsync` with passing validation → `Ready`
- Initial state after `InitializeAsync` with failing validation → `NotReady`
- `TranscribeFileAsync` transitions through `Running` → `Complete`
- `TranscribeFileAsync` on error transitions to `Failed`
- `RetryAsync` from `Failed` → `Running`
- `RevalidateAsync` from `NotReady` → `Ready` or `NotReady`
- `DownloadModelAsync` calls `IModelService.GetOrCreateFactoryAsync` then re-validates
- `DownloadModelAsync` sets `IsDownloadingModel` true during execution

Use mock/fake Core services for these tests.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/VoxFlow.Desktop.Tests/`
Expected: FAIL — `AppViewModel` does not exist.

- [ ] **Step 3: Implement AppViewModel**

`src/VoxFlow.Desktop/ViewModels/AppViewModel.cs`:
```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;

namespace VoxFlow.Desktop.ViewModels;

public class AppViewModel : INotifyPropertyChanged
{
    private readonly ITranscriptionService _transcriptionService;
    private readonly IValidationService _validationService;
    private readonly IConfigurationService _configService;
    private readonly IModelService _modelService;

    private AppState _currentState = AppState.NotReady;
    private ValidationResult? _validationResult;
    private TranscribeFileResult? _transcriptionResult;
    private ProgressUpdate? _currentProgress;
    private string? _errorMessage;
    private string? _lastFilePath;
    private bool _isDownloadingModel;
    private CancellationTokenSource? _cts;

    public AppViewModel(
        ITranscriptionService transcriptionService,
        IValidationService validationService,
        IConfigurationService configService,
        IModelService modelService)
    {
        _transcriptionService = transcriptionService;
        _validationService = validationService;
        _configService = configService;
        _modelService = modelService;
    }

    public AppState CurrentState
    {
        get => _currentState;
        private set { _currentState = value; OnPropertyChanged(); }
    }

    public ValidationResult? ValidationResult
    {
        get => _validationResult;
        private set { _validationResult = value; OnPropertyChanged(); }
    }

    public TranscribeFileResult? TranscriptionResult
    {
        get => _transcriptionResult;
        private set { _transcriptionResult = value; OnPropertyChanged(); }
    }

    public ProgressUpdate? CurrentProgress
    {
        get => _currentProgress;
        set { _currentProgress = value; OnPropertyChanged(); }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { _errorMessage = value; OnPropertyChanged(); }
    }

    public async Task InitializeAsync()
    {
        var options = await _configService.LoadAsync();
        var result = await _validationService.ValidateAsync(options);
        ValidationResult = result;
        CurrentState = result.CanStart ? AppState.Ready : AppState.NotReady;
    }

    public async Task TranscribeFileAsync(string filePath)
    {
        _lastFilePath = filePath;
        CurrentState = AppState.Running;
        ErrorMessage = null;
        _cts = new CancellationTokenSource();

        var progress = new Progress<ProgressUpdate>(update => CurrentProgress = update);

        try
        {
            var request = new TranscribeFileRequest(filePath);
            TranscriptionResult = await _transcriptionService.TranscribeFileAsync(
                request, progress, _cts.Token);
            CurrentState = TranscriptionResult.Success ? AppState.Complete : AppState.Failed;
            if (!TranscriptionResult.Success)
                ErrorMessage = string.Join("; ", TranscriptionResult.Warnings);
        }
        catch (OperationCanceledException)
        {
            CurrentState = AppState.Ready;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            CurrentState = AppState.Failed;
        }
    }

    public async Task RetryAsync()
    {
        if (_lastFilePath != null)
            await TranscribeFileAsync(_lastFilePath);
    }

    public async Task RevalidateAsync()
    {
        await InitializeAsync();
    }

    public bool IsDownloadingModel
    {
        get => _isDownloadingModel;
        private set { _isDownloadingModel = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Downloads/loads the model and re-validates. Called from NotReady view's "Download Model" button.
    /// </summary>
    public async Task DownloadModelAsync()
    {
        IsDownloadingModel = true;
        var progress = new Progress<ProgressUpdate>(update => CurrentProgress = update);

        try
        {
            var options = await _configService.LoadAsync();
            await _modelService.GetOrCreateFactoryAsync(options);
            // Model is now cached. Re-validate to transition state.
            await RevalidateAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Model download failed: {ex.Message}";
        }
        finally
        {
            IsDownloadingModel = false;
        }
    }

    public void CancelTranscription()
    {
        _cts?.Cancel();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyStateChanged() => OnPropertyChanged(string.Empty);

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/VoxFlow.Desktop.Tests/`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/VoxFlow.Desktop/ViewModels/ tests/VoxFlow.Desktop.Tests/
git commit -m "feat: implement AppViewModel state machine with tests"
```

---

### Task 14: Create dark theme CSS and Blazor layout

**Files:**
- Create: `src/VoxFlow.Desktop/wwwroot/css/app.css`
- Create: `src/VoxFlow.Desktop/Components/App.razor`
- Create: `src/VoxFlow.Desktop/Components/MainLayout.razor`
- Create: `src/VoxFlow.Desktop/Components/_Imports.razor`

- [ ] **Step 1: Create dark theme CSS**

`src/VoxFlow.Desktop/wwwroot/css/app.css` — implement the full dark theme from the spec with CSS custom properties for `--bg-primary`, `--bg-secondary`, `--text-primary`, etc. Include styles for `.drop-zone`, `.progress-bar`, `.status-bar`, `.settings-panel`, `.validation-checklist`, `.transcript-preview`, button variants, and all states.

- [ ] **Step 2: Create App.razor**

Root Blazor component with Router and MainLayout.

- [ ] **Step 3: Create MainLayout.razor**

Dark shell with VoxFlow title, gear icon that toggles settings panel, and state-based content rendering:
```razor
@inherits LayoutComponentBase
@inject AppViewModel ViewModel

<div class="app-shell">
    <header class="app-header">
        <span class="app-title">VoxFlow</span>
        <button class="settings-toggle" @onclick="ToggleSettings">⚙</button>
    </header>

    <main class="app-content">
        @switch (ViewModel.CurrentState)
        {
            case AppState.NotReady:
                <NotReadyView />
                break;
            case AppState.Ready:
                <ReadyView />
                break;
            case AppState.Running:
                <RunningView />
                break;
            case AppState.Failed:
                <FailedView />
                break;
            case AppState.Complete:
                <CompleteView />
                break;
        }
    </main>

    <StatusBar />

    @if (_settingsOpen)
    {
        <SettingsPanel OnClose="ToggleSettings" />
    }
</div>
```

- [ ] **Step 4: Verify build**

Run: `dotnet build src/VoxFlow.Desktop/VoxFlow.Desktop.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/VoxFlow.Desktop/wwwroot/ src/VoxFlow.Desktop/Components/
git commit -m "feat: add dark theme CSS and MainLayout with state-based rendering"
```

---

### Task 15: Create Blazor page components (NotReady, Ready, Running, Failed, Complete)

**Files:**
- Create: `src/VoxFlow.Desktop/Components/Pages/NotReadyView.razor`
- Create: `src/VoxFlow.Desktop/Components/Pages/ReadyView.razor`
- Create: `src/VoxFlow.Desktop/Components/Pages/RunningView.razor`
- Create: `src/VoxFlow.Desktop/Components/Pages/FailedView.razor`
- Create: `src/VoxFlow.Desktop/Components/Pages/CompleteView.razor`

- [ ] **Step 1: Create NotReadyView**

Checklist rendering `ValidationResult.Checks` with status icons (green check, yellow warning, red X, gray dash) and action buttons for common failures (Install ffmpeg link, Download Model button, Retry Validation button).

- [ ] **Step 2: Create ReadyView**

Drop zone hero with drag-and-drop support (HTML5 `ondragover`/`ondrop` events) and Browse Files button that calls `FilePicker.PickAsync()`.

- [ ] **Step 3: Create RunningView**

Progress bar rendering from `ViewModel.CurrentProgress` — shows percentage, elapsed time, current stage, and Cancel button.

- [ ] **Step 4: Create FailedView**

Error display with `ViewModel.ErrorMessage`, Retry button, and Choose Different File button.

- [ ] **Step 5: Create CompleteView**

Transcript preview (scrollable box showing first ~20 lines), stats (language, segments, duration), output path, Open Folder button, Copy Transcript button, and a secondary drop zone for chaining.

- [ ] **Step 6: Verify build and test manually**

Run: `dotnet build src/VoxFlow.Desktop/VoxFlow.Desktop.csproj`
Run: `dotnet run --project src/VoxFlow.Desktop/` (launches the desktop app)
Expected: App launches, shows NotReady or Ready state depending on environment.

- [ ] **Step 7: Commit**

```bash
git add src/VoxFlow.Desktop/Components/Pages/
git commit -m "feat: implement all Blazor page components (NotReady through Complete)"
```

---

### Task 16: Create shared components (DropZone, StatusBar, SettingsPanel)

**Files:**
- Create: `src/VoxFlow.Desktop/Components/Shared/DropZone.razor`
- Create: `src/VoxFlow.Desktop/Components/Shared/StatusBar.razor`
- Create: `src/VoxFlow.Desktop/Components/Shared/SettingsPanel.razor`
- Create: `src/VoxFlow.Desktop/ViewModels/SettingsViewModel.cs`

- [ ] **Step 1: Create DropZone component**

Reusable drag-and-drop zone with visual feedback (border highlight on drag-over), accepts file drops and button click for file picker. Emits `OnFileSelected` event callback.

- [ ] **Step 2: Create StatusBar component**

Bottom bar showing status indicators for ffmpeg, model, and language — green/yellow/red dots with labels.

- [ ] **Step 3: Create SettingsViewModel**

Exposes key settings (ModelType, Language, OutputDirectory, FfmpegPath) with save/load. Writes to `~/Library/Application Support/VoxFlow/appsettings.json`.

- [ ] **Step 4: Create SettingsPanel component**

Slide-over panel with form controls for key settings, Save button, and "Open appsettings.json" link.

- [ ] **Step 5: Verify build**

Run: `dotnet build src/VoxFlow.Desktop/VoxFlow.Desktop.csproj`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/VoxFlow.Desktop/Components/Shared/ src/VoxFlow.Desktop/ViewModels/SettingsViewModel.cs
git commit -m "feat: add DropZone, StatusBar, SettingsPanel components"
```

---

### Task 17: Add JS interop for drag-and-drop and platform integrations

**Files:**
- Create: `src/VoxFlow.Desktop/wwwroot/js/interop.js`
- Create: `src/VoxFlow.Desktop/Platform/MacFilePicker.cs`

- [ ] **Step 1: Create JS interop for drag-and-drop**

`src/VoxFlow.Desktop/wwwroot/js/interop.js`:
Handle HTML5 drag-and-drop events, extract file path from dropped files, and invoke Blazor callback.

- [ ] **Step 2: Create MacFilePicker adapter**

Wraps `FilePicker.PickAsync()` from MAUI Essentials with audio file type filter (`.m4a`).

- [ ] **Step 3: Wire up platform integrations**

Add open-folder action (`Launcher.OpenAsync`), clipboard (`Clipboard.SetTextAsync`) in the CompleteView.

- [ ] **Step 4: Test manually on macOS**

Run: `dotnet run --project src/VoxFlow.Desktop/`
Test: drag-and-drop a `.m4a` file, verify it reaches the transcription flow.

- [ ] **Step 5: Commit**

```bash
git add src/VoxFlow.Desktop/wwwroot/js/ src/VoxFlow.Desktop/Platform/
git commit -m "feat: add JS interop for drag-and-drop and macOS platform integrations"
```

---

## Phase G: Desktop Configuration and Packaging

### Task 18: Implement desktop configuration layering

**Files:**
- Modify: `src/VoxFlow.Core/Services/ConfigurationService.cs`
- Create: `src/VoxFlow.Desktop/Configuration/DesktopConfigurationService.cs`

The `.app` bundle is read-only. The desktop needs a layered config: bundled defaults + user overrides at `~/Library/Application Support/VoxFlow/appsettings.json`.

- [ ] **Step 1: Write failing test for desktop config layering**

Create `tests/VoxFlow.Desktop.Tests/DesktopConfigurationTests.cs`:
```csharp
using VoxFlow.Desktop.Configuration;

namespace VoxFlow.Desktop.Tests;

public class DesktopConfigurationTests
{
    [Fact]
    public async Task LoadAsync_MergesUserOverridesOnBundledDefaults()
    {
        // Create temp bundled defaults file
        // Create temp user override file with changed model type
        // Verify merged config uses user override for changed field
        // Verify merged config uses bundled default for unchanged fields
    }

    [Fact]
    public async Task LoadAsync_CreatesAppSupportDirectoryIfMissing()
    {
        // Verify ~/Library/Application Support/VoxFlow/ is created on first load
    }
}
```

- [ ] **Step 2: Implement DesktopConfigurationService**

`src/VoxFlow.Desktop/Configuration/DesktopConfigurationService.cs`:
```csharp
using System.Text.Json;
using VoxFlow.Core.Configuration;
using VoxFlow.Core.Interfaces;
using VoxFlow.Core.Models;

namespace VoxFlow.Desktop.Configuration;

public sealed class DesktopConfigurationService : IConfigurationService
{
    private static readonly string AppSupportDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoxFlow");

    private static readonly string UserConfigPath =
        Path.Combine(AppSupportDir, "appsettings.json");

    private static readonly string DefaultModelDir =
        Path.Combine(AppSupportDir, "models");

    private static readonly string DefaultOutputDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "VoxFlow");

    public Task<TranscriptionOptions> LoadAsync(string? configurationPath = null)
    {
        // Ensure Application Support directory exists
        Directory.CreateDirectory(AppSupportDir);
        Directory.CreateDirectory(DefaultModelDir);

        // Layer 1: bundled defaults (read-only inside .app)
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        // Layer 2: user overrides (writable)
        // Layer 3: explicit override path (if supplied)

        // Merge JSON layers: start with bundled defaults, overlay user overrides
        var merged = MergeJsonFiles(bundledPath, UserConfigPath, configurationPath);

        // Write merged JSON to a temp file so existing TranscriptionOptions.LoadFromPath works
        var tempPath = Path.Combine(Path.GetTempPath(), $"voxflow-merged-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(tempPath, merged);
            var options = TranscriptionOptions.LoadFromPath(tempPath);
            return Task.FromResult(options);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* best-effort */ }
        }
    }

    private static string MergeJsonFiles(string basePath, string userPath, string? overridePath)
    {
        using var baseDoc = File.Exists(basePath)
            ? JsonDocument.Parse(File.ReadAllText(basePath))
            : JsonDocument.Parse("{}");

        var merged = CloneJsonElement(baseDoc.RootElement);

        if (File.Exists(userPath))
        {
            using var userDoc = JsonDocument.Parse(File.ReadAllText(userPath));
            MergeInto(merged, userDoc.RootElement);
        }

        if (overridePath != null && File.Exists(overridePath))
        {
            using var overrideDoc = JsonDocument.Parse(File.ReadAllText(overridePath));
            MergeInto(merged, overrideDoc.RootElement);
        }

        return JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true });
    }

    private static Dictionary<string, object?> CloneJsonElement(JsonElement element)
    {
        // Deep-clone a JsonElement into a mutable dictionary tree
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.Object
                ? CloneJsonElement(prop.Value)
                : JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
        }
        return dict;
    }

    private static void MergeInto(Dictionary<string, object?> target, JsonElement source)
    {
        foreach (var prop in source.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Object
                && target.TryGetValue(prop.Name, out var existing)
                && existing is Dictionary<string, object?> existingDict)
            {
                MergeInto(existingDict, prop.Value);
            }
            else
            {
                target[prop.Name] = prop.Value.ValueKind == JsonValueKind.Object
                    ? CloneJsonElement(prop.Value)
                    : JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
            }
        }
    }

    public IReadOnlyList<SupportedLanguage> GetSupportedLanguages(string? configurationPath = null)
    {
        var options = LoadAsync(configurationPath).GetAwaiter().GetResult();
        return options.SupportedLanguages
            .Select((lang, i) => new SupportedLanguage(lang.Code, lang.DisplayName, i))
            .ToList();
    }

    /// <summary>
    /// Writes user override settings to the user config file.
    /// Only writes fields that differ from defaults.
    /// </summary>
    public async Task SaveUserOverridesAsync(Dictionary<string, object> overrides)
    {
        Directory.CreateDirectory(AppSupportDir);
        var json = JsonSerializer.Serialize(
            new { transcription = overrides },
            new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(UserConfigPath, json);
    }
}
```

- [ ] **Step 3: Register DesktopConfigurationService in MauiProgram.cs**

The desktop overrides the Core's default `ConfigurationService` with its own:
```csharp
// In MauiProgram.cs, AFTER AddVoxFlowCore():
builder.Services.AddSingleton<IConfigurationService, DesktopConfigurationService>();
```

This replaces the Core registration since the last registration wins in .NET DI.

- [ ] **Step 4: Set desktop-specific default paths**

Ensure the desktop's bundled `appsettings.json` uses:
- `"modelFilePath": "~/Library/Application Support/VoxFlow/models/ggml-base.bin"` (resolved at runtime)
- `"resultFilePath"` defaults to `~/Documents/VoxFlow/result.txt`

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/VoxFlow.Desktop.Tests/`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/VoxFlow.Desktop/Configuration/ tests/VoxFlow.Desktop.Tests/DesktopConfigurationTests.cs
git commit -m "feat: implement desktop configuration layering with user overrides"
```

---

### Task 19: Create BlazorProgressHandler

**Files:**
- Create: `src/VoxFlow.Desktop/Services/BlazorProgressHandler.cs`

- [ ] **Step 1: Implement BlazorProgressHandler**

`src/VoxFlow.Desktop/Services/BlazorProgressHandler.cs`:
```csharp
using VoxFlow.Core.Models;
using VoxFlow.Desktop.ViewModels;

namespace VoxFlow.Desktop.Services;

public sealed class BlazorProgressHandler : IProgress<ProgressUpdate>
{
    private readonly AppViewModel _viewModel;

    public BlazorProgressHandler(AppViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public void Report(ProgressUpdate value)
    {
        // SynchronizationContext from Blazor handles UI thread marshaling
        _viewModel.CurrentProgress = value;
        _viewModel.NotifyStateChanged();
    }
}
```

- [ ] **Step 2: Update AppViewModel to use BlazorProgressHandler**

In `AppViewModel.TranscribeFileAsync`, replace `new Progress<ProgressUpdate>(...)` with:
```csharp
var progress = new BlazorProgressHandler(this);
```

- [ ] **Step 3: Commit**

```bash
git add src/VoxFlow.Desktop/Services/BlazorProgressHandler.cs
git commit -m "feat: add BlazorProgressHandler for UI thread progress updates"
```

---

### Task 20: macOS packaging setup

**Files:**
- Create: `src/VoxFlow.Desktop/Platforms/MacCatalyst/Entitlements.plist`
- Create: `src/VoxFlow.Desktop/Platforms/MacCatalyst/Info.plist`
- Create: `scripts/build-macos.sh`
- Modify: `src/VoxFlow.Desktop/VoxFlow.Desktop.csproj`

- [ ] **Step 1: Configure Hardened Runtime entitlements**

`src/VoxFlow.Desktop/Platforms/MacCatalyst/Entitlements.plist`:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.cs.allow-unsigned-executable-memory</key>
    <true/>
    <key>com.apple.security.cs.allow-jit</key>
    <true/>
</dict>
</plist>
```

These entitlements are needed for Whisper.net native interop under Hardened Runtime.

- [ ] **Step 2: Configure Info.plist**

`src/VoxFlow.Desktop/Platforms/MacCatalyst/Info.plist`:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDisplayName</key>
    <string>VoxFlow</string>
    <key>CFBundleIdentifier</key>
    <string>com.voxflow.app</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>13.0</string>
</dict>
</plist>
```

- [ ] **Step 3: Update .csproj for packaging**

Add to `VoxFlow.Desktop.csproj`:
```xml
<PropertyGroup Condition="'$(TargetFramework)' == 'net9.0-maccatalyst'">
    <CodesignEntitlements>Platforms/MacCatalyst/Entitlements.plist</CodesignEntitlements>
    <EnableCodeSigning>true</EnableCodeSigning>
    <RuntimeIdentifier>maccatalyst-arm64</RuntimeIdentifier>
    <!-- Set to actual Developer ID for production signing -->
    <!-- <CodesignKey>Developer ID Application: Your Name (TEAMID)</CodesignKey> -->
</PropertyGroup>
```

- [ ] **Step 4: Bundle ffmpeg**

Add ffmpeg binary to the project and include in the app bundle:
```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net9.0-maccatalyst'">
    <Content Include="Resources\ffmpeg" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Download ffmpeg static binary for macOS and place in `src/VoxFlow.Desktop/Resources/ffmpeg`. Update the desktop's bundled `appsettings.json` to point `ffmpegExecutablePath` to the bundled location.

- [ ] **Step 5: Create build script**

`scripts/build-macos.sh`:
```bash
#!/bin/bash
set -euo pipefail

# Build the desktop app for macOS
dotnet publish src/VoxFlow.Desktop/VoxFlow.Desktop.csproj \
    -f net9.0-maccatalyst \
    -c Release \
    -r maccatalyst-arm64 \
    -p:CreatePackage=true

echo "Build artifacts in: src/VoxFlow.Desktop/bin/Release/net9.0-maccatalyst/maccatalyst-arm64/publish/"

# Generate SHA-256 checksum
APP_PATH=$(find src/VoxFlow.Desktop/bin/Release -name "*.pkg" -o -name "*.app" | head -1)
if [ -n "$APP_PATH" ]; then
    shasum -a 256 "$APP_PATH" > "${APP_PATH}.sha256"
    echo "Checksum: ${APP_PATH}.sha256"
fi
```

- [ ] **Step 6: Test build**

Run: `bash scripts/build-macos.sh`
Expected: Produces a `.app` bundle. If code signing fails (no Developer ID), the unsigned build should still succeed.

- [ ] **Step 7: Commit**

```bash
git add src/VoxFlow.Desktop/Platforms/ scripts/build-macos.sh
git commit -m "feat: add macOS packaging with Hardened Runtime and build script"
```

---

## Phase H: Documentation Updates

### Task 21: Update PRD.md

**Files:**
- Modify: `docs/product/PRD.md`

- [ ] **Step 1: Update PRD per spec Section 10**

Apply all changes listed in the spec:
- Purpose: add desktop app as a product surface
- Product Goals: add macOS desktop app goal
- Non-Goals: remove "Web or desktop UI", add "Linux/Windows desktop in Phase 1"
- External Contract: add desktop app input surface
- Functional Requirements: add FR13 (Desktop App) and FR14 (First-Run Bootstrap)
- Testing Requirements: add desktop smoke tests
- Engineering Requirements: add shared core library requirement
- MCP Server section: update to reference Core interfaces

- [ ] **Step 2: Commit**

```bash
git add docs/product/PRD.md
git commit -m "docs: update PRD for Phase 1 desktop app and shared core"
```

---

### Task 22: Update architecture documents

**Files:**
- Modify: `docs/architecture/01-system-context.md`
- Modify: `docs/architecture/02-container-view.md`
- Modify: `docs/architecture/03-component-view.md`
- Modify: `docs/architecture/04-runtime-sequences.md`
- Modify: `docs/architecture/05-quality-attributes.md`
- Modify: `docs/architecture/06-decision-log.md`
- Modify: `docs/architecture/07-architecture-review.md`
- Modify: `docs/product/ROADMAP.md`

- [ ] **Step 1: Update 01-system-context.md**

Add VoxFlow Desktop as third container. Update trust boundaries, data flow tables.

- [ ] **Step 2: Update 02-container-view.md**

Add VoxFlow.Core shared library and VoxFlow.Desktop container. Remove "Why Static Services" and "Application Facades" sections. Update dependency diagram.

- [ ] **Step 3: Update 03-component-view.md**

Restructure around Core interfaces. Add Desktop components. Remove facade components.

- [ ] **Step 4: Update 04-runtime-sequences.md**

Add Desktop transcription sequence and first-run/validation sequence. Update CLI and MCP sequences.

- [ ] **Step 5: Update 05-quality-attributes.md**

Update Maintainability (DI), Testability (interfaces), trade-off matrix. Add Desktop quality attributes.

- [ ] **Step 6: Update 06-decision-log.md**

Add ADR-019 through ADR-023. Update ADR-001 and ADR-016 statuses.

- [ ] **Step 7: Update 07-architecture-review.md**

Update executive summary, deliberate simplicity, evolution table.

- [ ] **Step 8: Update ROADMAP.md**

Update Phase 1 status and technology decisions.

- [ ] **Step 9: Commit**

```bash
git add docs/
git commit -m "docs: update all architecture documents for Phase 1 multi-host architecture"
```

---

## Phase I: Final Verification

### Task 23: Full solution build and test pass

**Files:** None (verification only)

- [ ] **Step 1: Full solution build**

Run: `dotnet build VoxFlow.sln`
Expected: Build succeeded. 0 errors, 0 warnings (or only expected platform warnings).

- [ ] **Step 2: Run all tests**

Run: `dotnet test VoxFlow.sln`
Expected: All tests pass across VoxFlow.Core.Tests, VoxFlow.Cli.Tests, VoxFlow.McpServer.Tests, VoxFlow.Desktop.Tests.

- [ ] **Step 3: Manual desktop app smoke test**

Run: `dotnet run --project src/VoxFlow.Desktop/`
Verify:
- App launches showing dark theme
- Validation checklist displays (NotReady or Ready depending on environment)
- Settings panel opens/closes from gear icon
- Status bar shows environment info
- If environment is ready: drop zone accepts file, transcription runs, result displays

- [ ] **Step 4: Manual CLI smoke test**

Run: `dotnet run --project src/VoxFlow.Cli/`
Verify: Same behavior as original CLI.

- [ ] **Step 5: Commit any final fixes**

```bash
git add -A
git commit -m "chore: final verification fixes for Phase 1"
```
