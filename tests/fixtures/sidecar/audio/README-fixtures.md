# Sidecar Audio Fixtures

Short WAV clips used by the `[Trait("Category", "RequiresPython")]`
integration tests in `VoxFlow.Core.Tests` to exercise the real
`voxflow_diarize.py` sidecar against real audio.

All files are **16 kHz mono PCM s16le**, approximately 10 seconds long.
See `../SIZE-BUDGET.md` for the per-file and aggregate size caps and the
reasoning behind committing these as plain binary (no Git LFS).

## Files

### `obama-speech-1spk-10s.wav`

- **Source:** `artifacts/input/President Obama Speech.m4a` (user-provided
  recording, already present in this repository; not redistributed from
  an external site).
- **Window:** 00:00:30 – 00:00:40 (a clean speech segment past any intro).
- **Format:** 16 kHz / mono / s16le, 10.000 s, ~313 KB.
- **License / usage note:** As-is from the `artifacts/input` directory.
  This WAV is a derivative clip used solely for local and CI tests; it is
  not published as a standalone dataset.
- **Used by:**
  - `PyannoteSidecarClientIntegrationTests.DiarizeAsync_RealSidecar_SingleSpeakerWav_Returns1Speaker`
  - `SidecarScriptContractTests.RunAgainstSingleSpeakerWav_ReturnsOkResponse_WithOneSpeaker`

### `libricss-2spk-10s.wav` — **not yet committed**

Reserved filename for a 2-speaker clip trimmed from the LibriCSS `0L`
session. This file is deliberately left out of the initial P0.8 PR so
that the source window can be auditioned before committing. The
integration tests that reference it remain guarded by
`Skip.IfNot(File.Exists(...))` until the file lands.

- **Planned source:** [LibriCSS](https://github.com/chenzhuo1011/libri_css)
  (CC BY 4.0). Download cached outside the repo.
- **Planned extraction:** ~10 s window from the `0L` session (0%
  overlap, 2-speaker mix), resampled to 16 kHz mono s16le.

### `libricss-3spk-10s.wav` — **not yet committed**

Reserved filename for a 3-speaker clip trimmed from a LibriCSS overlap
session (e.g. `OV20` / `OV30`). Same deferral reasoning as the 2-speaker
clip above.

- **Planned source:** [LibriCSS](https://github.com/chenzhuo1011/libri_css)
  (CC BY 4.0).
- **Planned extraction:** ~10 s window containing at least 3 distinct
  speakers, resampled to 16 kHz mono s16le.

## Reproduction commands

### Obama (single-speaker) — committed

```
ffmpeg -y \
  -i "artifacts/input/President Obama Speech.m4a" \
  -ss 00:00:30 -t 00:00:10 \
  -ac 1 -ar 16000 -sample_fmt s16 \
  tests/fixtures/sidecar/audio/obama-speech-1spk-10s.wav
```

Verify:

```
ffprobe -v error -show_entries stream=codec_name,sample_rate,channels,sample_fmt \
  -show_entries format=duration \
  -of default=noprint_wrappers=1 \
  tests/fixtures/sidecar/audio/obama-speech-1spk-10s.wav
```

Expected: `pcm_s16le / 16000 / 1 / s16 / duration=10.000000`.

### LibriCSS (2-speaker, 3-speaker) — template for follow-up PR

```
# 1. Fetch the LibriCSS source pack into a cache *outside* this repo.
# 2. Identify a clean 10 s window in the target session (0L / OV20).
# 3. Trim + downmix + resample to match the Obama clip:

ffmpeg -y \
  -i /path/to/libricss/<session>/<file>.wav \
  -ss <HH:MM:SS> -t 00:00:10 \
  -ac 1 -ar 16000 -sample_fmt s16 \
  tests/fixtures/sidecar/audio/libricss-2spk-10s.wav
```

Repeat for `libricss-3spk-10s.wav` from an overlap session.
