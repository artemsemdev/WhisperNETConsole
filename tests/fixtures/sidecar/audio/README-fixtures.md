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

### `libricss-2spk-10s.wav`

- **Source:** [LibriCSS](https://github.com/chenzhuo1011/libri_css)
  (CC BY 4.0), MVDR 2-stream continuous-speech-separation outputs,
  session `overlap_ratio_0.0_sil0.1_0.5_session0_actual0.0` (the `0S`
  no-overlap session, session 0). LibriCSS MVDR 2-stream splits the
  original multi-speaker recording into two beamformed streams
  (`channel_0`, `channel_1`); each stream carries a disjoint subset of
  the speakers, so both streams must be mixed back together to recover
  a realistic multi-speaker mixture.
- **Window:** 00:01:00 – 00:01:10 of each channel.
- **Format:** 16 kHz / mono / s16le, 10.000 s, ~313 KB.
- **License:** CC BY 4.0 (LibriCSS). Short derivative clip committed
  here solely for local and CI tests; not redistributed as a dataset.
- **Used by:**
  - `PyannoteSidecarClientIntegrationTests.DiarizeAsync_RealSidecar_TwoSpeakerWav_Returns2Speakers`
  - `SidecarScriptContractTests.RunAgainstTwoSpeakerWav_ReturnsOkResponse_WithTwoSpeakers`

### `libricss-3spk-10s.wav`

- **Source:** LibriCSS MVDR 2-stream outputs, session
  `overlap_ratio_20.0_sil0.1_1.0_session0_actual20.8` (the `OV20`
  20%-overlap session, session 0). Same two-stream reconstruction as
  the 2-speaker clip; the higher overlap ratio and denser mix make it
  likely that at least three distinct speakers are active in the
  selected window.
- **Window:** 00:01:00 – 00:01:10 of each channel.
- **Format:** 16 kHz / mono / s16le, 10.000 s, ~313 KB.
- **License:** CC BY 4.0 (LibriCSS).
- **Used by:**
  - `PyannoteSidecarClientIntegrationTests.DiarizeAsync_RealSidecar_ThreeSpeakerWav_ReturnsAtLeast3Speakers`

## Reproduction commands

### Obama (single-speaker)

```
ffmpeg -y \
  -i "artifacts/input/President Obama Speech.m4a" \
  -ss 00:00:30 -t 00:00:10 \
  -ac 1 -ar 16000 -sample_fmt s16 \
  tests/fixtures/sidecar/audio/obama-speech-1spk-10s.wav
```

### LibriCSS (2-speaker, 3-speaker)

Both LibriCSS fixtures are reconstructed by mixing the two MVDR
separation streams of the chosen session back together. Download the
LibriCSS MVDR 2-stream outputs into a cache outside this repository,
then:

```
# 2-speaker (0S session0)
ffmpeg -y \
  -ss 00:01:00 -t 00:00:10 \
  -i /path/to/libricss_mvdr_2stream/overlap_ratio_0.0_sil0.1_0.5_session0_actual0.0_channel_0.wav \
  -ss 00:01:00 -t 00:00:10 \
  -i /path/to/libricss_mvdr_2stream/overlap_ratio_0.0_sil0.1_0.5_session0_actual0.0_channel_1.wav \
  -filter_complex "[0:a][1:a]amix=inputs=2:duration=first:normalize=0" \
  -ac 1 -ar 16000 -sample_fmt s16 \
  tests/fixtures/sidecar/audio/libricss-2spk-10s.wav

# 3-speaker (OV20 session0)
ffmpeg -y \
  -ss 00:01:00 -t 00:00:10 \
  -i /path/to/libricss_mvdr_2stream/overlap_ratio_20.0_sil0.1_1.0_session0_actual20.8_channel_0.wav \
  -ss 00:01:00 -t 00:00:10 \
  -i /path/to/libricss_mvdr_2stream/overlap_ratio_20.0_sil0.1_1.0_session0_actual20.8_channel_1.wav \
  -filter_complex "[0:a][1:a]amix=inputs=2:duration=first:normalize=0" \
  -ac 1 -ar 16000 -sample_fmt s16 \
  tests/fixtures/sidecar/audio/libricss-3spk-10s.wav
```

`amix=...:normalize=0` preserves each stream's amplitude rather than
halving it, which matters for diarization clustering. `duration=first`
trims to the shorter input window.

### Verify

```
ffprobe -v error \
  -show_entries stream=codec_name,sample_rate,channels,sample_fmt \
  -show_entries format=duration \
  -of default=noprint_wrappers=1 \
  tests/fixtures/sidecar/audio/<fixture>.wav
```

Expected for every fixture: `pcm_s16le / 16000 / 1 / s16 / duration=10.000000`.
