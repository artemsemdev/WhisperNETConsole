# Architecture Decision Records

VoxFlow maintains architecture decision records (ADRs) as part of the architecture documentation.

## Where ADRs Live

All ADRs are maintained in a single decision log:

**[docs/architecture/06-decision-log.md](../architecture/06-decision-log.md)**

The decision log contains 23 ADR entries (ADR-001 through ADR-023) covering decisions from the initial console architecture through the multi-host Core extraction and Desktop app.

## ADR Format

Each record includes:

- **Status** — Accepted or Superseded (with a pointer to the superseding ADR)
- **Context** — The situation that motivated the decision
- **Decision** — What was decided
- **Alternatives considered** — Options evaluated and why they were rejected
- **Trade-offs accepted** — Consequences acknowledged at decision time

## Index

| ADR | Decision | Status |
|-----|----------|--------|
| 001 | Local-only console architecture | Superseded by 019 |
| 002 | File-based stable contract | Accepted |
| 003 | Configuration-driven behavior | Accepted |
| 004 | ffmpeg as external audio preprocessing | Accepted |
| 005 | Local model management with reuse-first | Accepted |
| 006 | Staged inference with explicit post-processing | Accepted |
| 007 | Language selection with duration-weighted scoring | Accepted |
| 008 | Fail fast before expensive work | Accepted |
| 009 | Cancellation propagation through full pipeline | Accepted |
| 010 | Reuse Whisper runtime within a run | Accepted |
| 011 | Sequential batch processing | Accepted |
| 012 | Configuration-driven batch mode | Accepted |
| 013 | One result file per input file | Accepted |
| 014 | Continue-on-error with batch summary | Accepted |
| 015 | Temp directory for intermediate WAVs | Accepted |
| 016 | MCP server as separate host with InternalsVisibleTo | Superseded by 023 |
| 017 | Stdio-only MCP transport | Accepted |
| 018 | Path policy for MCP tool arguments | Accepted |
| 019 | Extract shared VoxFlow.Core library with DI | Accepted |
| 020 | Use IProgress&lt;T&gt; for host-agnostic progress reporting | Accepted |
| 021 | Blazor Hybrid for macOS desktop UI | Accepted |
| 022 | ViewModel-driven desktop state flow | Accepted |
| 023 | Eliminate InternalsVisibleTo in favor of shared library | Accepted |

For the full record text of each ADR, see the [decision log](../architecture/06-decision-log.md).
