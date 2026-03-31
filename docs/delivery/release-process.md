# Release Process

Current state of VoxFlow release and delivery workflows.

## Current Scope

VoxFlow is currently distributed as source-built local artifacts. The release process consists of:

1. **Build** — `dotnet build` or `dotnet publish` from source
2. **Package** — `./scripts/build-macos.sh` produces a local `.pkg` or `.app` with a SHA-256 checksum
3. **Test** — run the test suite and per-host smoke checks (see [docs/runbooks/smoke-tests.md](../runbooks/smoke-tests.md))

There is no automated CI/CD pipeline, no published release artifacts, and no package registry distribution at this time.

## Release Checklist

Before tagging a release:

- [ ] All tests pass: `dotnet test VoxFlow.sln --no-restore`
- [ ] Per-host smoke tests pass (see [docs/runbooks/smoke-tests.md](../runbooks/smoke-tests.md))
- [ ] Desktop UI automation passes: `./scripts/run-desktop-ui-tests.sh`
- [ ] Architecture documentation is current with implementation
- [ ] `README.md` reflects the current project status
- [ ] No secrets, private recordings, or sensitive transcripts in tracked files

## Versioning

VoxFlow does not currently use a formal versioning scheme beyond the MCP server version string (`1.0.0` in `McpOptions`). Version management is a future consideration when distribution channels require it.

## What Is Not Yet Automated

| Concern | Current State | Notes |
|---------|--------------|-------|
| CI/CD pipeline | Not implemented | Builds and tests run locally |
| Code signing | Not implemented | Local builds are unsigned |
| Notarization | Not implemented | Required for Gatekeeper-compatible distribution |
| DMG/installer generation | Not implemented | `build-macos.sh` produces `.pkg`/`.app` only |
| Release artifact hosting | Not implemented | No distribution channel configured |
| Changelog generation | Not implemented | Decision log in `docs/architecture/06-decision-log.md` tracks architectural changes |

Each of these would be addressed when the project moves toward external distribution.
