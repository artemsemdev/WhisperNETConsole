# ADR-024: Adopt a Local Speaker Labeling Pipeline

**Status:** Accepted

**Date:** 2026-04-10

**Related ADRs:** ADR-004, ADR-006, ADR-019, ADR-020, ADR-021, ADR-022

**References:**

- Bain, M. et al., "WhisperX: Time-Accurate Speech Transcription of Long-Form Audio" (2023). Inspired the general pipeline shape (ASR → word timing → diarization → speaker assignment), though this ADR uses Whisper.net's native word timestamps instead of a separate forced-alignment model. https://github.com/m-bain/whisperX
- Bredin, H. et al., "pyannote.audio: neural building blocks for speaker diarization" (2020). The diarization toolkit used in the sidecar. pyannote pretrained models are licensed under CC BY-NC-SA 4.0, which permits non-commercial use. https://github.com/pyannote/pyannote-audio

**Context:** VoxFlow's current pipeline produces filtered transcript segments from local audio using `Whisper.net`, but it does not identify who spoke each segment. For interview, meeting, and call-review workflows, this limits the usefulness of the transcript. The product direction is to remain local-first and privacy-first, so cloud diarization is not an acceptable default architecture. The target scope is all languages supported by the existing transcription pipeline in the Desktop, CLI, and MCP hosts.

Speaker labeling does not depend on the language of the transcript. pyannote diarization operates on acoustic features (voice embeddings), not on text. Whisper.net 1.9.0 already provides word-level timestamps via `WhisperToken[]` in `SegmentData`, which eliminates the need for a separate forced-alignment model (e.g., wav2vec2). The current pipeline discards these tokens during filtering — this ADR changes that by preserving them for speaker assignment.

High-quality speaker labeling requires more than assigning a single speaker to each Whisper segment. Whisper segment boundaries are optimized for transcription, not turn-taking. If speaker labels are assigned only at the segment level, mixed turns and overlap around boundaries produce visibly wrong results. To achieve materially better quality, VoxFlow needs an enrichment pipeline that combines transcription, word-level timing, diarization, and a review surface.

**Process overview:**

Current pipeline (without speaker labeling):

```
┌──────────────┐   ┌──────────┐   ┌──────────────┐   ┌───────────────┐   ┌────────────┐
│  Audio File  │──>│  ffmpeg  │──>│  Whisper.net │──>│ Transcription │──>│   Output   │
│  .m4a / .wav │   │  Convert │   │  Inference   │   │ Filter        │   │  .txt ...  │
│              │   │  to WAV  │   │  (raw segs)  │   │ (accept segs) │   │            │
└──────────────┘   └──────────┘   └──────────────┘   └───────────────┘   └────────────┘
                   child process       .NET                .NET               .NET
```

New pipeline with speaker labeling (additions marked with `★`):

```
┌──────────────┐   ┌──────────┐   ┌──────────────────┐   ┌───────────────┐
│  Audio File  │──>│  ffmpeg  │──>│  Whisper.net     │──>│ Transcription │──┐
│  .m4a / .wav │   │  Convert │   │  Inference       │   │ Filter        │  │
│              │   │  to WAV  │   │  (raw segs       │   │ ★ preserve    │  │
└──────────────┘   └─────┬────┘   │ + WhisperToken[])│   │   word tokens │  │
                         │        └──────────────────┘   └───────────────┘  │
                         │                                                  │
                    normalized                            accepted segments │
                    WAV file                              + word timestamps │
                         │                                                  │
                         │  ★ DIARIZATION (optional)                        │
                         │  ┌────────────────────────────────────────┐      │
                         │  │       Python Sidecar (local)           │      │
                         │  │                                        │      │
                         ├─>│  pyannote speaker diarization          │      │
                         │  │  - extract voice embeddings from WAV   │      │
                         │  │  - cluster into speakers automatically │      │
                         │  │  - return speaker time segments        │      │
                         │  │                                        │      │
                         │  │  Response: who spoke when              │      │
                         │  │  (no text processing, language-agnostic│      │
                         │  └───────────────────┬────────────────────┘      │
                         │                      │ JSON                      │
                         │                      ▼                           │
                         │  ┌────────────────────────────────────────┐      │
                         │  │  ★ Speaker Merge (.NET Core)           │<─────┘
                         │  │                                        │
                         │  │  - match word tokens to speaker times  │
                         │  │  - assign speaker to each word         │
                         │  │  - group into speaker turns            │
                         │  │  - build TranscriptDocument            │
                         │  └───────────────────┬────────────────────┘
                         │                      │
                         │                      ▼
                         │  ┌────────────────────────────────────────┐
                         │  │  ★ Output                              │
                         │  │                                        │
                         │  │  existing formats     .voxflow.json    │
                         │  │  .txt .srt .vtt       (speaker roster  │
                         │  │  .json .md            + words + turns  │
                         │  │  (unchanged)          + review data)   │
                         │  └───────────────────┬────────────────────┘
                                                │
                                                ▼
                            ┌────────────────────────────────────────┐
                            │  ★ Desktop Review UI                   │
                            │                                        │
                            │  Speaker A (03:24) ──> "Интервьюер"    │
                            │  Speaker B (12:01) ──> "Кандидат"      │
                            │  Speaker C (01:15) ──> "HR"            │
                            │                                        │
                            │  [Rename]   [Copy]   [Export]          │
                            └────────────────────────────────────────┘
```

Data flow through the enrichment stage:

```
From Whisper.net                     From Python Sidecar
(word timestamps)                    (speaker segments)

WhisperToken[]                       Sidecar Request (JSON)
┌────────────────────────┐          ┌──────────────────────────────┐
│  "Hello" 1.20 - 1.50   │          │  { "version": 1,             │
│  "how"   1.50 - 1.90   │          │    "wavPath": "/tmp/out.wav" │
│  "are"   1.90 - 2.10   │          │  }                           │
│  "you"   2.10 - 2.40   │          └──────────────┬───────────────┘
│  "I'm"   3.10 - 3.40   │                         │
│  "fine"  3.40 - 3.80   │                         ▼
│  ...                   │          Sidecar Response (JSON)
└───────────┬────────────┘          ┌──────────────────────────────┐
            │                       │  { "version": 1,             │
            │                       │    "status": "ok",           │
            │                       │    "speakers": [             │
            │                       │      {"id": "A",             │
            │                       │       "duration": 94.2},     │
            │                       │      {"id": "B",             │
            │                       │       "duration": 201.5}     │
            │                       │    ],                        │
            │                       │    "segments": [             │
            │                       │      {"speaker": "A",        │
            │                       │       "start": 1.2,          │
            │                       │       "end": 3.1},           │
            │                       │      {"speaker": "B",        │
            │                       │       "start": 3.1,          │
            │                       │       "end": 4.8},           │
            │                       │      ...                     │
            │                       │    ]                         │
            │                       │  }                           │
            │                       └──────────────┬───────────────┘
            │                                      │
            └──────────────┬───────────────────────┘
                           │
                    .NET Speaker Merge
                    (match words to speaker times)
                           │
                           ▼
            TranscriptDocument (.NET)
            ┌──────────────────────────────────────┐
            │  speakers:                           │
            │    A -> "Speaker A"                  │
            │    B -> "Speaker B"                  │
            │                                      │
            │  words (with speakers assigned):     │
            │    "Hello" 1.20-1.50  speaker: A     │
            │    "how"   1.50-1.90  speaker: A     │
            │    "are"   1.90-2.10  speaker: A     │
            │    "you"   2.10-2.40  speaker: A     │
            │    "I'm"   3.10-3.40  speaker: B     │
            │    "fine"  3.40-3.80  speaker: B     │
            │                                      │
            │  turns (derived):                    │
            │    [A] 00:01.2 - 00:03.1             │
            │        "Hello, how are you…"         │
            │    [B] 00:03.1 - 00:04.8             │
            │        "I'm fine, thanks…"           │
            └──────────────────────────────────────┘
```

Runtime boundaries — everything runs locally on the user's machine:

```
┌───────────────────────────────────────────────────────────────┐
│                     User's Machine (local)                    │
│                                                               │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │                    .NET Process                         │  │
│  │  Whisper.net (ASR + word tokens)                        │  │
│  │  VoxFlow.Core (orchestration, speaker merge, output)    │  │
│  │  Host: Desktop / CLI / MCP                              │  │
│  └────────────┬────────────────────────┬───────────────────┘  │
│               │ spawn                  │ spawn                │
│               ▼                        ▼                      │
│  ┌────────────────────┐   ┌────────────────────────────┐      │
│  │  ffmpeg process    │   │  Python sidecar process    │      │
│  │  (audio convert)   │   │  (pyannote diarization     │      │
│  │                    │   │   only, no text processing)│      │
│  │  WAV in -> WAV out │   │                            │      │
│  │                    │   │  WAV in -> speaker         │      │
│  │                    │   │  time segments (JSON)      │      │
│  └────────────────────┘   └────────────────────────────┘      │
│                                                               │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │  Local Model Cache   ~/Library/Caches/VoxFlow/          │  │
│  │    whisper models (.bin)                                │  │
│  │    pyannote models (~300 MB)                            │  │
│  └─────────────────────────────────────────────────────────┘  │
│                                                               │
│  No network calls after models are cached                     │
└───────────────────────────────────────────────────────────────┘
```

**Decision:** Adopt a local post-ASR speaker-labeling architecture with four stages:

1. `Whisper.net` remains the system of record for transcription, language selection, and **word-level timestamps**. The existing `WhisperToken[]` data in `SegmentData` is preserved through filtering instead of being discarded.
2. A local Python sidecar performs **diarization only** using `pyannote/speaker-diarization-community-1`. It receives the normalized WAV file and returns speaker time segments (who spoke when). No text processing or word alignment happens in the sidecar — it is purely acoustic.
3. VoxFlow merges Whisper word tokens with diarization speaker segments in .NET, assigns a speaker to each word by time overlap, and regroups words into speaker-labeled transcript turns.
4. Desktop adds a lightweight review workflow that lets the user rename auto-detected speakers (`Speaker A`, `Speaker B`, `Speaker C`, ...) and inspect the enriched transcript before export or copy.

Word-level timestamps from Whisper.net replace the need for a separate forced-alignment model (e.g., wav2vec2). This eliminates ~1 GB of additional model dependencies and removes text/language processing from the sidecar entirely.

The diarization pipeline is an enrichment stage layered on top of the existing accepted-segment pipeline. It does not replace the current filtering pipeline and does not change the current non-speaker transcript behavior unless the feature is enabled.

**Architectural rules:**

- Keep `Whisper.net` as the ASR engine and the source of word-level timestamps (`WhisperToken[]`).
- Treat speaker labeling as a post-processing enrichment stage after accepted transcript segments have been produced.
- Use local diarization only. Do not require cloud APIs for the default path.
- Keep the sidecar focused on diarization only — no text processing, no word alignment, no language-specific models. The sidecar is a pure acoustic processor.
- Keep merge and speaker-assignment logic in .NET so it is testable, host-agnostic, and independent from the Python runtime.
- Persist a structured transcript artifact that can carry words, speakers, turns, and review metadata without overloading the current plain-text outputs.

**Implementation shape:**

- Preserve `WhisperToken[]` from `SegmentData` through the transcription filter. Currently tokens are discarded (`Array.Empty<WhisperToken>()`); the filter must pass them through so word-level timestamps are available for speaker assignment.
- Add a `speakerLabeling` configuration section to `TranscriptionOptions` with automatic speaker count detection. Speaker labeling is available for all languages supported by the transcription pipeline.
- Introduce a structured transcript document model separate from `FilteredSegment`, with speaker-aware segment and word types.
- Add a Core orchestration interface such as `ISpeakerEnrichmentService` that accepts the normalized WAV path and produces diarization speaker segments.
- Call a Python sidecar process from Core using a stable JSON contract. The sidecar receives only the WAV path and returns speaker time segments — no transcript data crosses the process boundary.
- In .NET, merge word tokens (from Whisper.net) with speaker time segments (from sidecar) by time overlap to assign a speaker to each word.
- Keep existing user-facing exports (`txt`, `md`, `srt`, `vtt`, `json`) and add a structured companion artifact for speaker-aware state, such as `*.voxflow.json`.
- Extend Desktop completion flow to load the structured artifact and present rename/review actions without requiring a full waveform editor in the first release.

**Sidecar lifecycle:**

- The Python sidecar is launched per-request as a child process, consistent with the ffmpeg precedent established in ADR-004. This keeps the lifecycle simple: spawn, exchange JSON over stdin/stdout, wait for exit.
- pyannote model loading is expensive (several seconds). For single-file workflows the per-request cost is acceptable. For batch workflows the cumulative cost may become significant. If profiling confirms this, a future iteration may introduce a persistent daemon mode with a keep-alive protocol — but per-request is the starting point.
- The Python runtime must be resolved before Phase 1 begins. Options in preference order: (1) a self-contained binary built with PyInstaller, bundled with the Desktop installer and CLI distribution; (2) a managed virtualenv created during first-run setup; (3) system Python with dependency installation. Option 1 eliminates user-facing Python management entirely and is strongly preferred. A follow-up spike should validate PyInstaller packaging of pyannote and its dependencies (PyTorch, torchaudio). Without wav2vec2, the sidecar footprint is reduced by ~1 GB compared to a full WhisperX-style stack.
- pyannote model files (~300 MB) must be cached locally. First-run acquisition should be explicit and cancellable. The cache location should follow platform conventions (`~/Library/Caches/VoxFlow/` on macOS). Offline operation must work once models are cached.
- `IValidationService` must include a preflight check for sidecar availability (binary exists, model files present). Failure should produce a clear diagnostic, not a runtime crash.

**Sidecar JSON contract:**

- The request/response contract is the integration seam between two runtimes. It must be versioned and documented.
- The contract schema should be defined in `docs/contracts/sidecar-diarization-v1.json` as a JSON Schema file. Both the .NET serializer and the Python deserializer must be validated against shared fixture files.
- The contract is intentionally minimal: no transcript text, no language code, no segments cross the process boundary. The sidecar works purely with audio.
- The request envelope includes: `version` (integer, starting at 1), `wavPath` (absolute path to normalized WAV).
- The response envelope includes: `version`, `status` ("ok" | "error"), `error` (string, present only on failure), `speakers` (array of `{id, totalDuration}`), `segments` (array of `{speaker, start, end}` — time intervals per speaker). The number of speakers is determined automatically by the diarization model and may vary per audio file.
- On contract mismatch (version disagreement or malformed JSON), the .NET side must treat it as a recoverable enrichment failure, not a pipeline crash.

**Structured transcript model:**

- The document model should use a speaker roster at the document level with per-word speaker references:
  - `TranscriptDocument`: top-level container with `speakers` (list of `SpeakerInfo { id, displayName }`), `words` (list of `TranscriptWord { start, end, text, speakerId }`), and derived `turns` (list of `SpeakerTurn { speakerId, startTime, endTime, words }`).
  - Words originate from `WhisperToken[]` (text + timing from Whisper.net). Speaker IDs are assigned by matching each word's time range against the diarization speaker segments. The merge algorithm assigns each word to the speaker whose segment has the largest time overlap with that word.
  - The number of speakers is determined automatically by the diarization model — no manual speaker count is required from the user. Speaker identities are assigned ordinal labels in order of first appearance: `Speaker A`, `Speaker B`, `Speaker C`, etc. User renames update the `displayName` in the roster without changing the `speakerId` references.
  - Turns are computed by grouping consecutive words with the same `speakerId`. They are a derived view, not a primary storage unit.
- This model is separate from `FilteredSegment`, which remains the unit of the transcription-only pipeline. The enrichment stage consumes accepted segments (with preserved `WhisperToken[]`) plus diarization speaker segments and produces a `TranscriptDocument`.
- The model should be designed and reviewed as a standalone spike before Phase 1 implementation begins, validated against the Desktop review UI wireframes and the CLI/MCP output requirements.

**Progress reporting:**

- Add a new `ProgressStage` variant (e.g., `SpeakerLabeling`) to the existing `ProgressStage` enum from ADR-020. Diarization can take multiple minutes; without progress feedback the Desktop UI will appear frozen.
- The sidecar contract may include newline-delimited JSON progress lines on stderr (e.g., embedding extraction progress, clustering phase) while the final result is written to stdout.

**Failure modes:**

- Diarization is an enrichment, not a prerequisite. If the enrichment fails for any reason, the pipeline must produce the original non-speaker transcript with a warning, not a hard error.
- Specific failure cases and their handling:
  - Sidecar binary not found or not executable → validation-time failure with diagnostic message; feature disabled.
  - pyannote model not downloaded → validation-time failure with download instructions; feature disabled.
  - Sidecar process crashes or returns non-zero exit → enrichment skipped; warning attached to result: "Speaker labeling was not available for this file."
  - Sidecar returns malformed JSON → enrichment skipped; warning attached.
  - Sidecar times out → process killed via cancellation; enrichment skipped; warning attached.
  - Audio contains only one speaker or many overlapping speakers → not an error; labels are produced as detected, with speaker count metadata surfaced to the user.
- The `TranscribeFileResult` should carry an optional `enrichmentWarnings` list so hosts can surface these to the user without conflating them with transcription errors.

**Automatic speaker count detection:**

- The diarization model determines the number of speakers automatically — no user input or hardcoded speaker count is required. pyannote's clustering pipeline supports unconstrained speaker detection out of the box.
- Detected speakers are assigned ordinal labels in order of first appearance in the audio: `Speaker A`, `Speaker B`, `Speaker C`, etc. The labeling sequence uses the English alphabet (A–Z), which supports up to 26 speakers — more than sufficient for practical audio scenarios.
- After diarization, the pipeline should surface metadata about the detected speaker distribution so the user can assess quality:
  - Number of detected speakers.
  - Per-speaker total speech duration (already present in the sidecar response envelope).
  - If only one speaker is detected, attach an informational note: "Only one speaker detected." The enriched transcript is still produced — this is not an error.
- An optional `maxSpeakers` configuration parameter may be added in a future iteration to let users constrain detection when they know the expected count, but it is not required for the initial release.

**Artifact lifecycle for `.voxflow.json`:**

- The `.voxflow.json` file is an internal artifact, not a user-facing export format. It carries the full `TranscriptDocument` including speaker roster, word-level data, and review metadata (renames).
- It is written to the same directory as the primary output file, alongside the existing format output.
- Desktop loads it on the completion screen to populate the review UI. CLI and MCP can read it for structured speaker-aware output.
- Cleanup: when a user starts a new transcription for the same input file, the previous `.voxflow.json` is overwritten. Batch summary should note which files have companion artifacts.
- The schema must be documented in `docs/contracts/voxflow-transcript-v1.json`. If the schema evolves, the version field in the file allows forward-compatible reading.

**Initial product scope:**

- All languages supported by the existing transcription pipeline
- Automatic speaker count detection with ordinal labels (`Speaker A`, `Speaker B`, `Speaker C`, ...)
- Local execution only
- Review and rename in Desktop
- Structured output available to CLI and MCP consumers

**Not in initial scope:**

- Arbitrary speaker identification by real names
- Cross-session speaker recognition
- Cloud diarization fallback
- Full word-level manual editing UI
- Speaker count limits or manual speaker count override

**Alternatives considered:**

| Alternative | Why rejected |
|------------|-------------|
| Whisper-only segment labeling | Not accurate enough at turn boundaries because Whisper segments are not diarization turns. |
| Channel-based labeling only | Useful for dual-channel calls, but not sufficient for the common case of mono meeting and interview audio. |
| Replace the .NET ASR pipeline with WhisperX end to end | Would require a larger platform shift and would duplicate working host integration already built around `Whisper.net`. Remains a valid future option if pyannote licensing becomes a constraint. |
| Cloud diarization via hosted APIs | Conflicts with VoxFlow's local-first and privacy-first product principles. |
| Embed pyannote directly inside the MAUI/Desktop process | Increases packaging and runtime complexity inside the app process and makes isolation, diagnostics, and upgrades harder than a sidecar boundary. |
| Defer review UI and expose raw labels only | Leaves users with no practical way to correct inevitable diarization mistakes, which weakens trust in the feature. |
| Separate word alignment model (wav2vec2) in sidecar | Whisper.net 1.9.0 already provides word-level timestamps via `WhisperToken[]`. Adding wav2vec2 would increase sidecar size by ~1 GB, add language-specific model management, and duplicate timing data that Whisper already produces. |

**Trade-offs accepted:**

- VoxFlow will gain a Python sidecar dependency for the speaker-labeling path, which increases packaging, setup, and support complexity.
- `pyannote` model acquisition introduces model-distribution and local-environment concerns that do not exist in the current Whisper-only path.
- Speaker labeling will remain probabilistic and will require user review for high-stakes workflows.
- The structured transcript model adds another output surface that must remain stable across Desktop, CLI, and MCP.
- Automatic speaker count detection is more flexible than a fixed two-speaker constraint, but diarization quality degrades in noisy audio or when many speakers have short turns. The review UI is the mitigation: users can verify and correct labels.
- Per-request sidecar spawning trades latency (model reload per file) for lifecycle simplicity. Batch workflows may expose this cost; a daemon mode is a future optimization, not a Phase 1 requirement.
- pyannote pretrained models use CC BY-NC-SA 4.0, which permits non-commercial use. This is acceptable for VoxFlow's current non-commercial scope. If the project becomes commercial, a separate license agreement with pyannote/CNRS would be required.
- Whisper.net word-level timestamps are "good enough" for speaker assignment but may be less precise than dedicated forced-alignment models in edge cases (overlapping speech, very short words). The review UI mitigates this — users can verify results.

**Consequences:**

- Core remains the orchestration center, and host behavior stays consistent across Desktop, CLI, and MCP.
- Desktop evolves from a transcript-preview screen into a transcript-review screen for speaker-aware workflows.
- CLI and MCP gain access to richer transcript artifacts without needing to duplicate merge logic.
- Future enhancements such as uncertainty highlighting, speaker-specific exports, and evidence extraction can build on the same structured transcript document.

**Testing strategy:**

- **Unit tests** for the .NET merge and speaker-assignment logic using JSON fixture files that represent sidecar responses. No Python runtime required. These cover the core business rules: word-to-speaker mapping, turn grouping, ordinal label assignment, and rename propagation. Fixtures should include single-speaker, two-speaker, and multi-speaker (3+) scenarios.
- **Contract tests** that validate .NET serialization and Python deserialization produce identical structures from shared fixture files stored in `tests/fixtures/sidecar/`. Both sides must pass against the same fixtures to ensure the contract holds.
- **Integration tests** that exercise the full sidecar path with a real Python process and a short WAV fixture (~5 seconds of two-speaker audio). Gated behind a test category (e.g., `[Category("RequiresPython")]`) so they do not run in CI environments without the Python sidecar installed.
- **Desktop component tests** for the review UI: rename actions, speaker label rendering, structured artifact loading. Follow the existing headless Razor rendering pattern.

**Design artifacts required before Phase 1:**

The following design artifacts must be produced and reviewed before Phase 1 implementation begins. These are architectural seams that will be expensive to change after code is written against them.

1. **Sidecar contract spec** — JSON Schema for request/response envelopes, versioning protocol, error envelope, progress reporting mechanism. Location: `docs/contracts/sidecar-diarization-v1.json`.
2. **Structured transcript model** — `TranscriptDocument` type design with speaker roster, word-level references (sourced from `WhisperToken[]`), turn derivation rules, and rename metadata. Validated against Desktop review UI wireframes and CLI/MCP output requirements.
3. **Python distribution decision** — Spike to validate PyInstaller packaging of pyannote + PyTorch into a self-contained binary. If PyInstaller is not viable, document the fallback strategy (managed virtualenv or system Python) with user-facing setup instructions.

**Implementation notes:**

1. Phase 0: Produce the three design artifacts listed above. Validate PyInstaller packaging. Preserve `WhisperToken[]` through `TranscriptionFilter` (prerequisite for Phase 1).
2. Phase 1: Add Core speaker-enrichment interfaces, sidecar contract, word-to-speaker merge logic, and structured artifact generation behind configuration.
3. Phase 2: Add Desktop review UI for rename and inspection of speaker-labeled segments.
4. Phase 3: Extend CLI and MCP outputs to expose speaker-aware transcript data explicitly.
5. Phase 4: Evaluate optional speaker count constraints, cross-session speaker recognition, and per-language quality benchmarks after packaging is stable.
