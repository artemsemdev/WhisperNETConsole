# Adding New Scope of Work

How to define, evaluate, and integrate new work into VoxFlow's delivery process.

---

## When to Use This Process

Use this process when introducing work that changes product behavior, adds a new capability, modifies architecture, or affects the external contract. Examples:

- A new transcription feature (e.g., speaker diarization, streaming mode)
- A new product surface or host (e.g., Windows Desktop, HTTP MCP transport)
- A significant change to an existing pipeline stage
- A new integration or dependency

For small bug fixes, documentation corrections, or minor refactors, follow the standard [Contributing](../../CONTRIBUTING.md) workflow instead.

---

## Step 1: Define the Problem

Before proposing a solution, document the problem clearly.

| Question | Purpose |
|---|---|
| What user need or gap does this address? | Ensures the work is outcome-driven, not solution-driven |
| Which target personas are affected? | Anchors scope to real users (see [PRD §3](../product/PRD.md)) |
| What happens if we do nothing? | Establishes urgency and priority |
| Does this conflict with any non-goal? | Prevents scope that contradicts explicit exclusions (see [PRD §7](../product/PRD.md)) |

Write a short problem statement (3-5 sentences) that a team member unfamiliar with the request can understand without additional context.

---

## Step 2: Evaluate Fit

Check alignment with VoxFlow's product principles before investing in design.

### Principle Alignment Check

| Principle | Evaluate |
|---|---|
| **Local-first** | Does this keep all processing on the user's machine? |
| **Privacy-first** | Does this avoid sending audio or transcript data externally? |
| **Configuration-driven** | Can behavior be controlled through `appsettings.json` without code changes? |
| **Failure-transparent** | Will failures produce clear, actionable diagnostics? |
| **Scriptable** | Is the capability reachable from CLI, Desktop, and MCP? |

If any principle is violated, document the trade-off explicitly and get maintainer approval before proceeding.

### Architecture Fit

- Review the [Architecture Overview](../../ARCHITECTURE.md) and [Container View](../architecture/02-container-view.md) to understand where new work fits in the system.
- New transcription logic belongs in `VoxFlow.Core`. Host projects (CLI, Desktop, MCP) should contain only host-specific concerns.
- If the change introduces a new external dependency, document it as a candidate [Architecture Decision Record](../adr/README.md).

---

## Step 3: Scope the Work

Define boundaries before implementation begins.

### Scope Definition Template

```markdown
### Title
[Short descriptive name]

### Problem Statement
[3-5 sentences from Step 1]

### Proposed Solution
[What the change does, at a level sufficient for review]

### In Scope
- [Concrete deliverables and behaviors]

### Out of Scope
- [Explicitly excluded items to prevent scope creep]

### Affected Surfaces
- [ ] VoxFlow.Core
- [ ] VoxFlow.Cli
- [ ] VoxFlow.Desktop
- [ ] VoxFlow.McpServer

### Configuration Changes
[New or modified settings in appsettings.json, if any]

### Dependencies
[New runtime or build dependencies, if any]

### Acceptance Criteria
- [Observable, testable outcomes]
```

### Sizing Guidelines

| Size | Characteristics | Approach |
|---|---|---|
| **Small** | Single component, no new dependencies, no config changes | Single PR, standard review |
| **Medium** | Touches core + one or more hosts, adds configuration | Branch, 1-3 focused PRs, documentation update |
| **Large** | New capability, new dependency, architecture change | Issue discussion first, ADR if needed, phased PRs |

---

## Step 4: Get Alignment

The level of alignment required scales with the size of the change.

| Size | Alignment Required |
|---|---|
| **Small** | Self-review against this checklist, then open a PR |
| **Medium** | Open a GitHub issue with the scope definition, get maintainer feedback before implementation |
| **Large** | Open a GitHub issue, write an ADR draft, discuss approach before writing code |

For medium and large work, do not start implementation until the scope definition is reviewed. Rework after implementation is more expensive than rework at the design stage.

---

## Step 5: Plan Delivery

Break the approved scope into deliverable increments.

### Delivery Principles

1. **Ship incrementally.** Each PR should leave the codebase in a working state. Prefer multiple small PRs over one large PR.
2. **Core first, hosts second.** Implement shared logic in `VoxFlow.Core` before wiring it into CLI, Desktop, or MCP.
3. **Tests with code.** Each PR that changes behavior must include corresponding test updates. Do not defer tests to a follow-up PR.
4. **Docs with code.** Update relevant documentation in the same PR or immediately after. Stale docs are worse than no docs.

### Suggested Delivery Sequence

```
1. ADR (if architectural decision is needed)
2. Core library changes + unit tests
3. Configuration schema changes
4. Host integration (CLI / Desktop / MCP) + host-specific tests
5. Documentation updates (architecture, runbooks, PRD if scope warrants)
6. Smoke test validation (see docs/runbooks/smoke-tests.md)
```

---

## Step 6: Update Project Documentation

After the work is delivered, update the following as applicable:

| Document | When to Update |
|---|---|
| [PRD](../product/PRD.md) | New functional requirement, new user journey, or changed non-goal |
| [Architecture views](../architecture/) | New component, changed container boundaries, new runtime sequence |
| [ADR index](../adr/README.md) | New architectural decision |
| [Developer Setup](../developer/setup.md) | New dependency, new build step, new environment variable |
| [Release Process](release-process.md) | New release checklist item or changed delivery workflow |
| [README.md](../../README.md) | New capability visible to end users |
| [Runbooks](../runbooks/) | New operational procedure or troubleshooting scenario |

---

## Checklist Summary

Use this as a quick reference before and during new scope work.

- [ ] Problem is documented and understood
- [ ] Principle alignment is verified (local-first, privacy-first, config-driven)
- [ ] Scope is defined with clear in/out boundaries and acceptance criteria
- [ ] Alignment is obtained at the appropriate level (PR / issue / ADR)
- [ ] Work is broken into incremental, reviewable deliverables
- [ ] Core logic is implemented before host-specific wiring
- [ ] Tests accompany every behavior change
- [ ] Documentation is updated to reflect the delivered scope
- [ ] Release checklist is reviewed for new validation steps
