# Audio Transcription Utility

Local C# console application that converts a local `.m4a` file to `.wav`, preprocesses audio with `ffmpeg`, transcribes it with a local Whisper model, and writes timestamped transcript lines to a local text file.

The current implementation uses `Whisper.net 1.9.0`.

## What It Does

The application runs these stages:

1. Load settings from `appsettings.json` or `TRANSCRIPTION_SETTINGS_PATH`
2. Run startup validation and print a preflight report
3. Convert the source audio to WAV with `ffmpeg`
4. Apply configured audio-cleanup filters during WAV generation
5. Reuse or download the configured Whisper model
6. Load WAV samples and transcribe them with Whisper
7. Filter junk, noise placeholders, and duplicate hallucination loops
8. Write the final transcript to `result.txt`

If one language is configured, the app uses that language directly. If multiple languages are configured, the app evaluates each configured language candidate and selects the best supported result.

## I/O Contract

Input:

- local `.m4a` file

Intermediate output:

- local `.wav` file

Final output:

- local text file with this format:

```text
{start}->{end}: {text}
```

Example:

```text
00:00:01.2000000->00:00:03.8000000: Hello, this is a test.
```

## Configuration

The repository includes a portable default [appsettings.json](appsettings.json) and a matching [appsettings.example.json](appsettings.example.json).

Before your first real run, update the paths in `appsettings.json` or point the app at another settings file.

To use a different settings file:

```bash
TRANSCRIPTION_SETTINGS_PATH=/absolute/path/to/appsettings.json dotnet run
```

### Core Paths

- `inputFilePath`: source `.m4a` file
- `wavFilePath`: generated WAV file
- `resultFilePath`: generated transcript file
- `modelFilePath`: local Whisper model file
- `ffmpegExecutablePath`: `ffmpeg` executable name or absolute path

### Model Settings

- `modelType`: model type used by `WhisperGgmlDownloader`

Example values include `Base`, `LargeV3`, and `LargeV3Turbo`, depending on the installed Whisper.NET package support.

### WAV Conversion Settings

- `outputSampleRate`
- `outputChannelCount`
- `outputContainerFormat`
- `overwriteWavOutput`
- `audioFilterChain`

`audioFilterChain` is an ordered list of ffmpeg audio filters applied during WAV generation.

Current checked-in defaults:

```json
"audioFilterChain": [
  "afftdn=nf=-25",
  "silenceremove=stop_periods=-1:stop_threshold=-50dB:stop_duration=1"
]
```

This reduces background noise and removes long silent stretches before transcription.

### Language Settings

- `supportedLanguages`
- `code`
- `displayName`

Behavior:

- one configured language: direct forced transcription
- multiple configured languages: per-language candidate scoring and best-candidate selection

The current checked-in settings use English only.

### Filtering and Scoring Settings

- `nonSpeechMarkers`
- `longLowInformationSegmentThresholdSeconds`
- `minTextLengthForLongSegment`
- `minSegmentProbability`
- `minWinningCandidateProbability`
- `minWinningMargin`
- `tieBreakerEpsilon`
- `rejectAmbiguousLanguageCandidates`
- `minAcceptedSpeechDurationSeconds`

### Anti-Hallucination Settings

- `useNoContext`
- `noSpeechThreshold`
- `logProbThreshold`
- `entropyThreshold`
- `suppressBracketedNonSpeechSegments`
- `maxConsecutiveDuplicateSegments`
- `maxDuplicateSegmentTextLength`

These settings are used to reduce:

- non-speech bracketed placeholders like `[door opening]`
- repeated short phrases produced during silence
- low-confidence junk output

### Startup Validation Settings

- `startupValidation.enabled`
- `startupValidation.printDetailedReport`
- `startupValidation.checkInputFile`
- `startupValidation.checkOutputDirectories`
- `startupValidation.checkOutputWriteAccess`
- `startupValidation.checkFfmpegAvailability`
- `startupValidation.checkModelType`
- `startupValidation.checkModelDirectory`
- `startupValidation.checkModelLoadability`
- `startupValidation.checkLanguageSupport`
- `startupValidation.checkWhisperRuntime`

### Console Progress Settings

- `consoleProgress.enabled`
- `consoleProgress.useColors`
- `consoleProgress.progressBarWidth`
- `consoleProgress.refreshIntervalMilliseconds`

## Runtime Output

Before transcription starts, the app prints a startup-validation report with a final outcome:

- `PASSED`
- `PASSED WITH WARNINGS`
- `FAILED`

If startup validation fails, transcription does not start.

During transcription, the app shows a progress bar with:

- overall percent done
- overall percent left
- current language candidate
- elapsed time
- current activity

The app also logs:

- input file detection
- ffmpeg availability
- WAV conversion start/success/failure
- applied ffmpeg audio filters
- model reuse or download
- candidate scores in multi-language mode
- skipped-segment reasons
- final output file path

## Project Layout

- [Program.cs](Program.cs): application entry point
- [Configuration/](Configuration): settings loading and validation
- [Audio/](Audio): `ffmpeg` conversion and WAV loading
- [Processing/](Processing): transcript filtering
- [Services/](Services): model loading, language selection, progress UI, output writing, and startup validation
- [tests/WisperTestApp.UnitTests/](tests/WisperTestApp.UnitTests): unit tests
- [tests/WisperTestApp.EndToEndTests/](tests/WisperTestApp.EndToEndTests): end-to-end tests
- [PRD.md](PRD.md): product requirements document

## Build And Run

### Prerequisites

- .NET 9 SDK
- `ffmpeg` installed and reachable through `ffmpegExecutablePath`
- file-system access to the configured input and output paths

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run
```

The default checked-in configuration is intentionally a template. Update `artifacts/input.m4a` and any other paths you need before running against real audio.

## Tests

Run unit tests:

```bash
dotnet test tests/WisperTestApp.UnitTests/WisperTestApp.UnitTests.csproj
```

Run end-to-end tests:

```bash
dotnet test tests/WisperTestApp.EndToEndTests/WisperTestApp.EndToEndTests.csproj
```

Run both:

```bash
dotnet test tests/WisperTestApp.UnitTests/WisperTestApp.UnitTests.csproj
dotnet test tests/WisperTestApp.EndToEndTests/WisperTestApp.EndToEndTests.csproj
```

The end-to-end tests do not use your real local audio files or require a checked-in Whisper model. They generate temporary settings, create a fake `ffmpeg` executable, and use generated WAV fixtures.

## Notes

- The checked-in config currently uses English in single-language mode.
- If you configure a single language, the app will use that language directly and skip candidate comparison.
- If transcription quality is still not sufficient, changing the model type to a larger model is usually more impactful than only adjusting thresholds.
