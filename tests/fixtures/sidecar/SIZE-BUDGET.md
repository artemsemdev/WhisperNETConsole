# Sidecar Fixture Size Budget

This directory holds both JSON fixtures (tiny, hand-authored) and
binary audio fixtures (committed as plain binary, **no Git LFS**). The
goal is a self-contained repo where any developer can `git clone` and
immediately run `dotnet test --filter Category=RequiresPython` without
needing an out-of-band dataset fetch.

## Per-file cap

- **400 KB** per WAV.

## Aggregate cap

- **1 MB total** across `audio/*.wav`.

## Why these numbers

A 10-second window of 16 kHz / mono / 16-bit PCM is:

```
10 s × 16000 samples/s × 2 bytes/sample = 320000 bytes ≈ 313 KB
```

plus a ~44-byte WAV header, so **~313 KB per clip** is the natural floor.
The 400 KB per-file cap gives us a little slack for format tweaks and
`-sample_fmt` variants; the 1 MB aggregate cap lets us keep three clips
(the spec in `docs/delivery/local-speaker-labeling/phase-0-foundation.md`
calls for one single-speaker clip plus two LibriCSS clips).

Anything that would push a single file above the per-file cap must
either be shortened (`-t` smaller) or down-sampled; any new clip
introduced later must keep the aggregate under 1 MB or evict an
existing clip.

## Current usage

| File | Size | Status |
|---|---|---|
| `audio/obama-speech-1spk-10s.wav` | ~313 KB | committed |
| `audio/libricss-2spk-10s.wav` | ~313 KB | committed |
| `audio/libricss-3spk-10s.wav` | ~313 KB | committed |

Aggregate committed: **~938 KB / 1 MB** (92%). No room remains under the
cap; any new clip requires trimming or evicting an existing fixture.

## `.gitattributes` — no LFS

The WAV files under `audio/` are **not** tracked by Git LFS. LFS would
add a mandatory round-trip to a remote LFS server on clone, which would
break the "clone-and-test" contract above and would require every CI
runner and developer laptop to authenticate against an LFS endpoint.
Since the per-file cap keeps these blobs small enough to live directly
in the pack file, plain binary storage is the right tradeoff.

If a future fixture legitimately needs to exceed the cap, stop and
reconsider: either trim the window, pick a different source, or split
the clip into multiple smaller ones. Adding LFS should be a conscious
architectural decision, not a workaround for an oversized clip.

The repository-level `.gitattributes` should **not** declare
`*.wav filter=lfs` for paths under `tests/fixtures/sidecar/audio/`.
