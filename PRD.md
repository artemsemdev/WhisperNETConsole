# Product Requirements Document

## Product Name

Audio Transcription Utility

## Purpose

Provide a local C# console application that:

- reads a local `.m4a` file
- converts it to a filtered `.wav` file with `ffmpeg`
- transcribes the audio with a local Whisper model
- writes timestamped transcript lines to a local text file

The product must stay local-only and must not depend on cloud transcription APIs.

## Product Goals

- Keep the external input/output contract stable
- Make all runtime behavior configuration-driven
- Validate prerequisites before long-running work starts
- Show visible runtime progress during transcription
- Allow graceful cancellation of long-running work
- Reduce hallucinated transcript output caused by silence and noise
- Keep the system testable and maintainable

## Non-Goals

- Real-time transcription
- Speaker diarization
- Translation
- Web or desktop UI
- Cloud-hosted inference
- Batch folder processing

## External Contract

### Input

- local `.m4a` source file

### Intermediate Output

- local `.wav` file

### Final Output

- local `result.txt`

### Output Format

Each transcript line must remain:

```text
{start}->{end}: {text}
```

Example:

```text
00:00:01.2000000->00:00:03.8000000: Hello, this is a test.
```

## Runtime Configuration

The application must load runtime settings from:

- `appsettings.json`, or
- a path provided through `TRANSCRIPTION_SETTINGS_PATH`

The following settings must be configurable:

- input, WAV, result, and model paths
- Whisper model type
- `ffmpeg` executable path
- WAV output format
- ffmpeg audio-filter chain
- supported languages
- filtering and scoring thresholds
- anti-hallucination transcription settings
- startup validation behavior
- console progress behavior

## Functional Requirements

### 1. Startup Validation

Before transcription begins, the application must run a configurable preflight stage.

The stage must report:

- `PASSED`
- `PASSED WITH WARNINGS`
- `FAILED`

Checks may include:

- input file presence
- output directory existence
- output directory writability
- `ffmpeg` availability and version
- model type validity
- model directory existence and writability
- model reuse or download readiness
- Whisper runtime loadability
- configured language-code support

If the final startup outcome is `FAILED`, transcription must not start.

### 2. Audio Conversion

The application must convert the input `.m4a` file to a `.wav` file before transcription.

The WAV output must be:

- mono
- `16000 Hz`
- WAV container

The conversion stage must support a configurable `ffmpeg` audio-filter chain. The current default chain is:

```text
afftdn=nf=-25
silenceremove=stop_periods=-1:stop_threshold=-50dB:stop_duration=1
```

This is intended to reduce noise and remove long silent stretches before transcription.

### 3. Model Management

The application must use a local Whisper model file.

Behavior:

- reuse the configured model when it already exists and loads correctly
- download the configured model if it is missing
- re-download the configured model if it exists but is empty or unloadable

The implementation currently targets `Whisper.net 1.9.0`.

### 4. Supported Languages

The application must transcribe only the configured supported languages.

Business defaults previously included:

- English
- Russian
- German
- Ukrainian

The current runtime configuration may narrow that list. At the moment, the default checked-in configuration is:

- English

### 5. Language Selection

Language handling must follow the configured supported-language list.

Behavior:

- if exactly one language is configured, the application must use that language directly and skip candidate comparison
- if multiple languages are configured, the application must run one candidate pass per configured language
- each candidate must be filtered and scored
- the system must choose the best supported candidate using duration-weighted segment probability

Ambiguity handling must be configurable:

- reject ambiguous candidates, or
- continue with the best candidate and print a warning

### 6. Anti-Hallucination Controls

The transcription pipeline must expose decoder settings that help reduce junk output on silence or low-information audio.

Current configurable controls include:

- `useNoContext`
- `noSpeechThreshold`
- `logProbThreshold`
- `entropyThreshold`

The default checked-in settings are tuned to reduce repetitive and non-speech hallucinations.

### 7. Transcript Filtering

The system must skip unusable segments.

Current filtering categories include:

- empty text
- configured non-speech markers
- low-probability segments
- low-information long segments
- suspicious non-speech placeholders
- bracketed non-speech stage directions such as `[door opening]`
- repeated short duplicate loops that often appear during silence hallucinations

### 8. Progress Reporting

The application must show clear runtime progress during transcription.

The progress UI must show:

- overall percentage complete
- overall percentage left
- current language candidate
- elapsed time
- current activity text

The UI must support:

- colored ANSI output in interactive terminals
- readable fallback output when stdout is redirected

### 9. Cancellation

The application must support graceful cancellation of long-running work.

Behavior:

- user interruption such as `Ctrl+C` must stop the current run without corrupting output state
- internal async operations must accept and pass through cancellation tokens where supported
- in-progress external process work should be stopped when cancellation is requested
- canceled runs must exit clearly instead of hanging during download, conversion, transcription, or output writing

### 10. Result Writing

The application must write accepted transcript segments to the configured result file using UTF-8 and the existing timestamped line format.

If the run is rejected because of unsupported or ambiguous language, the application must not silently produce misleading transcript output.

## Reliability Requirements

- fail early on invalid configuration
- fail early when required tooling is unavailable
- fail clearly on `ffmpeg` conversion errors
- fail clearly on model download or load errors
- stop promptly and cleanly when cancellation is requested
- print actionable diagnostics for each major stage
- avoid unsafe native teardown paths that can trigger macOS runtime instability

## User Experience Requirements

- show clear preflight results before work begins
- make long-running transcription visibly active
- log model reuse vs. model download
- log applied audio filters during WAV conversion
- log candidate scores when multi-language mode is used
- log skipped-segment reasons for diagnostics

## Engineering Requirements

- production code must stay organized by responsibility
- all business rules must be driven by configuration instead of inline magic values
- the app must remain a console application with no CLI contract changes
- the external I/O contract must remain backward-compatible

## Testing Requirements

### Unit Tests

The solution must include unit coverage for:

- configuration loading and validation
- transcript filtering
- WAV loading
- output formatting
- language decision rules
- startup validation reporting

### End-to-End Tests

The solution must include process-level tests for:

- clean failure on startup-validation failure
- successful startup and entry into the transcription flow

The current end-to-end tests use:

- a generated temporary settings file
- a fake `ffmpeg` executable
- a generated WAV fixture

## Success Criteria

- the application starts with a clear startup-validation report
- the user sees a live progress indicator during transcription
- audio cleanup is applied during WAV generation through configured `ffmpeg` filters
- single-language runs no longer mis-route through unnecessary language selection
- obvious silence and stage-direction hallucinations are reduced
- output format remains unchanged
- unit and end-to-end tests pass
