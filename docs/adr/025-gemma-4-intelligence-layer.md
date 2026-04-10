# ADR-025: Use Gemma 4 as an Optional Local Intelligence Layer

**Status:** Accepted

**Date:** 2026-04-11

**Related ADRs:** ADR-019, ADR-021, ADR-023, ADR-024

**References:**

- Google AI for Developers, "Gemma 4 model card". The official model card describes Gemma 4 as a family optimized for reasoning, agentic workflows, native function calling, native system prompts, long context, and multimodal input, with audio support called out on the smaller variants. https://ai.google.dev/gemma/docs/core/model_card_4
- Google AI for Developers, "Audio understanding". The official audio guide documents Gemma audio processing details including a maximum clip length of 30 seconds and single-channel audio handling. https://ai.google.dev/gemma/docs/capabilities/audio

---

## Context

VoxFlow produces high-quality local transcripts. That is a solved problem. The unsolved problem is what happens after the transcript exists.

A user who records a client call does not open VoxFlow to read a wall of text. They open it because they need to know what the client objected to, what decisions were made, and who owes whom a deliverable by Friday. Today they have to read the entire transcript, extract that information themselves, and copy it into whatever system they actually work in. The transcript is an intermediate artifact. The user's job is not "read transcript." The user's job is "act on what was said."

This is the product gap. VoxFlow currently stops at the point where the hard engineering problem ends and the hard product problem begins. The transcription pipeline (Whisper.net, ffmpeg, ADR-024 speaker labeling) produces accurate, timestamped, speaker-aware text. But accurate text is not the same as usable knowledge. The distance between a 45-minute transcript and a usable meeting summary is still entirely on the user.

Gemma 4 closes that gap locally. It introduces strong general reasoning, native structured JSON output, native function calling, long context windows, and system prompt support — all deployable on-device. These capabilities map directly to the three product surfaces that turn VoxFlow from a transcription utility into a local audio intelligence workspace:

1. **Ask Your Transcript** — the user asks questions and gets grounded answers from their own recording.
2. **Structured Outputs** — the system extracts meeting summaries, action items, risks, objections, and findings as typed, exportable artifacts.
3. **Built-in Chat** — a persistent, transcript-grounded search and reasoning interface embedded in the product.

Gemma 4 is not a replacement for the transcription pipeline. The official audio documentation limits audio clips to 30 seconds and single-channel processing. Gemma 4 is not the source of truth for long-form ASR, diarization, word timing, turn boundaries, or speaker assignment. Those remain responsibilities of Whisper.net and the ADR-024 speaker labeling pipeline. The architectural boundary is clean: everything below the transcript is the transcription pipeline; everything above the transcript is the intelligence layer.

---

## Decision

Use Gemma 4 as an optional local intelligence layer that consumes VoxFlow transcript artifacts and produces usable knowledge through three product capabilities: transcript Q&A, structured workflow outputs, and built-in transcript chat.

The product thesis is: **VoxFlow sells usable knowledge, not transcripts.** The transcript is the foundation. The intelligence layer is the product.

### Processing order

```
┌──────────────────────────────────────────────────────────────────────┐
│                         VoxFlow Pipeline                             │
│                                                                      │
│  ┌────────────┐   ┌────────────┐   ┌────────────────┐               │
│  │   Audio    │──>│  Whisper   │──>│  Transcription │               │
│  │   File     │   │  .net ASR  │   │  Filter        │               │
│  └────────────┘   └────────────┘   └───────┬────────┘               │
│                                            │                         │
│                                    ┌───────▼────────┐               │
│                                    │  ADR-024       │               │
│                                    │  Speaker       │  (optional)   │
│                                    │  Labeling      │               │
│                                    └───────┬────────┘               │
│                                            │                         │
│                               Transcript Artifact                    │
│                          (text + timestamps + speakers)              │
│                                            │                         │
│  ══════════════════════════════════════════╪════════════════════════  │
│                    INTELLIGENCE LAYER (this ADR)                     │
│                                            │                         │
│                    ┌───────────────────────▼──────────────────────┐  │
│                    │            Gemma 4 (local)                   │  │
│                    │                                              │  │
│                    │  ┌─────────────────────────────────────────┐ │  │
│                    │  │  Ask Your Transcript                    │ │  │
│                    │  │  "What did the client object to?"       │ │  │
│                    │  │  "Summarize only Speaker B"             │ │  │
│                    │  └─────────────────────────────────────────┘ │  │
│                    │                                              │  │
│                    │  ┌─────────────────────────────────────────┐ │  │
│                    │  │  Structured Outputs                     │ │  │
│                    │  │  meeting_summary.json                   │ │  │
│                    │  │  action_items.json                      │ │  │
│                    │  │  decisions_risks.json                   │ │  │
│                    │  │  customer_objections.json               │ │  │
│                    │  │  research_findings.json                 │ │  │
│                    │  └─────────────────────────────────────────┘ │  │
│                    │                                              │  │
│                    │  ┌─────────────────────────────────────────┐ │  │
│                    │  │  Built-in Chat                          │ │  │
│                    │  │  transcript-grounded search             │ │  │
│                    │  │  multi-turn reasoning over content      │ │  │
│                    │  │  speaker-filtered queries               │ │  │
│                    │  └─────────────────────────────────────────┘ │  │
│                    │                                              │  │
│                    └──────────────────────────────────────────────┘  │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

1. VoxFlow transcribes audio using the existing core pipeline (Whisper.net).
2. VoxFlow optionally enriches the transcript with speaker labels (ADR-024).
3. The intelligence layer consumes the transcript artifact — text, timestamps, speaker metadata — and produces answers, structured outputs, or chat responses on demand.

---

## Capability 1: Ask Your Transcript

This is the highest-value product capability because it moves VoxFlow from generating transcripts to generating answers.

**User experience:** After transcription completes, the user sees their transcript and a question input. They type a natural-language question. VoxFlow returns a grounded answer with evidence from the transcript.

**Example interactions:**

| User question | Expected answer shape |
|---|---|
| "What did the client object to?" | Extracted objections with timestamps and speaker attribution |
| "List all decisions made in this meeting" | Numbered list of decisions with who proposed them and when |
| "What action items were assigned?" | Structured list: item, owner, due date (if stated), timestamp |
| "Summarize only Speaker B" | Speaker-filtered summary covering only that participant's contributions |
| "What risks were discussed?" | Risk statements with severity context and who raised them |
| "Did anyone mention the Q3 deadline?" | Direct quote with timestamp, or "not mentioned" |

**Why this matters:** A transcript is a document. An answer is a decision. Users do not pay for documents; they pay for the ability to make decisions faster. "Ask Your Transcript" converts a passive artifact into an interactive knowledge surface. The user stops reading and starts asking.

**Grounding rules:**

- Every answer must be traceable to content in the transcript. If the transcript does not contain the information, the system says so rather than hallucinating.
- Answers should cite evidence: quoted spans, timestamps, speaker labels when available.
- The system must distinguish between what was explicitly stated and what is inferred. Inference is permitted but must be marked as such.

**Input contract:** The intelligence layer receives the full transcript text (or relevant slices for long transcripts), speaker metadata from ADR-024 when available, and the user's question as a natural-language string.

---

## Capability 2: Structured Outputs for Workflows

Gemma 4 supports native structured JSON output. VoxFlow uses this to extract typed, schema-bound workflow artifacts that the user can export, copy, or pipe into downstream systems.

**Why this matters:** A meeting summary in a chat bubble is nice. A meeting summary as a typed JSON object that a Notion integration, a CRM, or an MCP agent can consume is a workflow primitive. Structured outputs turn VoxFlow into a node in the user's automation graph, not a dead end.

**Approved structured output types:**

### Meeting Summary

```json
{
  "title": "Product Review — Sprint 14",
  "date": "2026-04-11",
  "duration_minutes": 42,
  "participants": ["Speaker A", "Speaker B", "Speaker C"],
  "abstract": "Sprint 14 review covering launch readiness, open blockers, and Q3 timeline adjustments.",
  "topics": [
    {
      "title": "Launch readiness",
      "summary": "Feature-complete, pending QA sign-off.",
      "start_timestamp": "00:02:14",
      "speakers": ["Speaker A", "Speaker B"]
    }
  ],
  "decisions": [
    {
      "text": "Push launch to April 18 pending QA results.",
      "proposed_by": "Speaker A",
      "timestamp": "00:12:33"
    }
  ],
  "action_items": [
    {
      "text": "Run full regression suite on staging",
      "owner": "Speaker B",
      "due": "2026-04-14",
      "timestamp": "00:13:01"
    }
  ],
  "open_questions": [
    {
      "text": "Do we need a separate rollback plan for the payment flow?",
      "raised_by": "Speaker C",
      "timestamp": "00:28:44"
    }
  ]
}
```

### Action Items

```json
{
  "items": [
    {
      "text": "Run full regression suite on staging",
      "owner": "Speaker B",
      "due": "2026-04-14",
      "priority": "high",
      "context": "Blocking launch decision",
      "evidence": {
        "quote": "Can you get the regression done by Monday?",
        "speaker": "Speaker A",
        "timestamp": "00:13:01"
      }
    }
  ]
}
```

### Risks and Issues

```json
{
  "items": [
    {
      "text": "Payment flow has no rollback plan",
      "severity": "high",
      "status": "open",
      "raised_by": "Speaker C",
      "blockers": ["No staging environment for payment testing"],
      "mitigation": "Speaker A to check with platform team",
      "evidence": {
        "quote": "If payments break in prod, we have no way back",
        "timestamp": "00:28:44"
      }
    }
  ]
}
```

### Customer Objections

```json
{
  "items": [
    {
      "text": "Pricing is too high for the SMB segment",
      "category": "pricing",
      "sentiment": "strong_concern",
      "raised_by": "Speaker B",
      "context": "Comparing to competitor X at $29/mo",
      "evidence": {
        "quote": "At this price point, my team would just stick with the free tier of X",
        "timestamp": "00:18:22"
      }
    }
  ]
}
```

### Research Findings

```json
{
  "items": [
    {
      "finding": "Users prefer automatic speaker detection over manual count input",
      "confidence": "high",
      "supporting_evidence": [
        {
          "quote": "I never know how many people will be in the room",
          "speaker": "Participant 3",
          "timestamp": "00:14:55"
        }
      ],
      "source_context": "User interview, round 2"
    }
  ]
}
```

**Output rules:**

- Every structured output field that references transcript content must include an `evidence` object with a quote and timestamp.
- Optional fields (owner, due date, severity) are present only when inferable from the transcript. Missing data is omitted, not guessed.
- Schemas are defined as JSON Schema files in `docs/contracts/intelligence/` and versioned independently.
- Outputs are stored separately from the transcript artifact. The transcript is the source of truth; structured outputs are derived views.

---

## Capability 3: Built-in Transcript Chat

The chat is the persistent interaction surface that ties capabilities 1 and 2 together. It is not a generic AI assistant. It is a transcript-grounded search and reasoning interface.

**Why this matters:** "Ask Your Transcript" handles single questions. Structured outputs handle predefined extractions. But real knowledge work is iterative. The user asks "what were the objections?", then follows up with "which ones did we address?", then "draft a response to the pricing concern." The chat gives the user a conversational workspace over their transcript data.

**Chat architecture:**

```
┌───────────────────────────────────────────────────────────────┐
│                    Desktop Chat Panel                          │
│                                                                │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  Quick Actions Bar                                       │  │
│  │  [ Summarize ] [ Actions ] [ Decisions ] [ Risks ]       │  │
│  │  [ Objections ] [ Findings ] [ Speaker B only ]          │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  Chat History (grounded, multi-turn)                     │  │
│  │                                                           │  │
│  │  User: What were the main objections?                     │  │
│  │  VoxFlow: Three objections were raised:                   │  │
│  │    1. Pricing too high for SMB (Speaker B, 18:22)         │  │
│  │    2. Missing SSO integration (Speaker B, 24:10)          │  │
│  │    3. No mobile app (Speaker C, 31:05)                    │  │
│  │                                                           │  │
│  │  User: Which ones did we commit to addressing?            │  │
│  │  VoxFlow: Speaker A committed to SSO in Q3 (24:45).      │  │
│  │    Pricing and mobile were acknowledged but no            │  │
│  │    commitment was made.                                   │  │
│  │                                                           │  │
│  │  User: Export the objections as JSON                      │  │
│  │  VoxFlow: [customer_objections.json exported]             │  │
│  │                                                           │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  [ Ask anything about this transcript...            🔍 ] │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                │
└───────────────────────────────────────────────────────────────┘
```

**Chat scope rules:**

- **Phase 1 scope:** The current transcript only. One recording, one chat context.
- **Phase 2 scope:** Bounded multi-transcript search. The user selects a project or folder, and the chat reasons across multiple transcripts. ("Across all client calls this week, what objections came up more than once?")
- Chat responses must prefer extraction and citation over unsupported inference.
- Chat must support speaker filters when ADR-024 speaker labeling data is available. ("Only show what Speaker B said about pricing.")
- The quick actions bar exposes explicit intents — `Summarize`, `Actions`, `Decisions`, `Risks`, `Objections`, `Findings` — so the user is not forced to formulate every request from scratch.
- Chat history is local, tied to the transcript or project context, and persisted between sessions.
- The chat can trigger structured output generation. If the user asks "give me the action items as JSON," the chat produces a schema-bound structured output, not free-form text.

**Grounding enforcement:**

- The system prompt anchors Gemma to the transcript content. It does not answer questions outside the transcript scope.
- If the user asks something the transcript does not cover, the response is explicit: "This transcript does not contain information about [topic]."
- The chat is not a general-purpose assistant. It does not write emails, generate code, or answer trivia. It reasons over transcript content.

---

## Architectural Rules

1. **Gemma sits above transcript artifacts, not below them.** Transcript generation, timestamps, and speaker assignment remain responsibilities of Whisper.net and the ADR-024 pipeline. The intelligence layer is a consumer of transcript artifacts, not a producer.

2. **Intelligence features are optional.** The default transcription pipeline must work identically whether the intelligence layer is enabled or disabled. No Gemma dependency for core transcription.

3. **Outputs are advisory, not authoritative.** Structured outputs and chat answers are model-generated interpretations. The transcript artifact remains the system of record. Product surfaces must visually distinguish generated insights from raw transcript text.

4. **Grounding is mandatory.** Every intelligence feature must operate on transcript content as its primary input. No open-domain assistant behavior. No hallucinated facts. Answers cite evidence from the transcript.

5. **Graceful degradation.** If the local Gemma runtime or weights are unavailable, VoxFlow functions as before — transcription works, outputs work, the user just does not get intelligence features. No crashes, no error states that block the core flow.

6. **Explicit activation.** Intelligence features are triggered by user action (asking a question, clicking a quick action, requesting an export). They never silently alter the baseline transcript or inject generated content into the transcript artifact.

7. **Raw audio is out of scope for Gemma.** Any future raw-audio Gemma workflow must be explicitly clip-scoped and must respect the documented 30-second audio limit. Gemma does not touch the audio pipeline.

8. **Host-agnostic contracts.** The same intelligence capabilities must be available across Desktop, CLI, and MCP through shared Core interfaces.

---

## Implementation Shape

### Core abstraction

Add an `ITranscriptIntelligenceService` interface in `VoxFlow.Core` with bounded task contracts:

```
ITranscriptIntelligenceService
├── AnswerTranscriptQuestion(transcript, question) -> GroundedAnswer
├── GenerateMeetingSummary(transcript) -> MeetingSummaryOutput
├── ExtractActionItems(transcript) -> ActionItemsOutput
├── ExtractDecisionsAndRisks(transcript) -> DecisionsRisksOutput
├── ExtractCustomerObjections(transcript) -> CustomerObjectionsOutput
├── ExtractResearchFindings(transcript) -> ResearchFindingsOutput
├── GenerateChapters(transcript) -> ChaptersOutput
├── SuggestRedactions(transcript) -> RedactionSuggestionsOutput
├── ChatMessage(transcript, history, message) -> ChatResponse
├── SearchTranscriptFacts(transcript, query) -> FactSearchResult
├── FindSpeakerMentions(transcript, speaker, topic) -> MentionsResult
└── SummarizeSpeaker(transcript, speaker) -> SpeakerSummaryOutput
```

### Configuration

Add an `intelligence` configuration section:

```json
{
  "intelligence": {
    "enabled": false,
    "runtime": "local",
    "model": "gemma-4-e2b",
    "modelPath": null,
    "maxContextTokens": 8192,
    "structuredOutputSchemaVersion": 1
  }
}
```

### Input contract

Every intelligence request packages:

- Transcript text (full or windowed for long transcripts).
- Speaker metadata from ADR-024 when available (speaker roster, per-turn speaker labels).
- Task type (question, summary, extraction, chat).
- User intent (the question or the structured output type).
- Chat history (for multi-turn chat only).

### Output contract

- Structured outputs conform to versioned JSON Schemas in `docs/contracts/intelligence/`.
- Chat responses include: answer text, cited evidence (quotes + timestamps + speakers), confidence signal, and a flag indicating whether the answer is extracted or inferred.
- All outputs are stored separately from the transcript artifact. The `.voxflow.json` file (ADR-024) is not modified by intelligence outputs.

### Execution model

- Intelligence tasks are user-initiated, never automatic.
- Each task produces a result that is stored and exportable independently.
- Desktop renders results in the chat panel and allows export.
- CLI outputs structured JSON to stdout or to a file.
- MCP exposes intelligence tasks as tools that agents can invoke.

---

## Product Guidance by Model Size

| Variant | Best fit | Trade-off |
|---|---|---|
| Gemma 4 E2B / E4B | Desktop quick actions, single-question Q&A, lightweight summaries, note generation | Fast, low memory. May struggle with very long transcripts or complex multi-step reasoning. |
| Larger Gemma 4 variants | Workstation-grade analysis, full meeting summary extraction, cross-transcript synthesis, heavy MCP workflows | Higher latency, higher memory. Better output quality on complex tasks. |

The runtime should select the model variant based on available hardware and task complexity. Desktop defaults to the smallest usable variant. Power users and MCP workflows may opt into larger variants explicitly.

---

## Desktop Product Shape

The completion screen evolves into a **transcript workspace**:

```
┌────────────────────────────────────────────────────────────────────┐
│  VoxFlow — client-call-2026-04-11.m4a                    [Export ▾]│
├────────────────────────────────┬───────────────────────────────────┤
│                                │                                   │
│  TRANSCRIPT                    │  INTELLIGENCE                     │
│                                │                                   │
│  [Speaker A] 00:00:12          │  ┌───────────────────────────┐   │
│  Welcome everyone, let's       │  │ [ Summarize ] [ Actions ] │   │
│  start with the Q3 update.     │  │ [ Decisions ] [ Risks ]   │   │
│                                │  │ [ Objections ] [ Find ]   │   │
│  [Speaker B] 00:00:28          │  └───────────────────────────┘   │
│  Thanks. So the main           │                                   │
│  concern from the client       │  User: What were the pricing     │
│  is pricing. At $89/seat       │  concerns?                        │
│  they said it's a non-         │                                   │
│  starter for their SMB         │  VoxFlow: Speaker B raised one   │
│  customers.                    │  pricing concern at 00:28:        │
│                                │  "At $89/seat they said it's a   │
│  [Speaker A] 00:01:05          │  non-starter for their SMB       │
│  I hear that. What if we       │  customers." Speaker A proposed  │
│  offer a volume discount       │  a volume discount at 01:05.     │
│  at 50+ seats?                 │  No resolution was reached.      │
│                                │                                   │
│  ...                           │  User: _                          │
│                                │                                   │
├────────────────────────────────┴───────────────────────────────────┤
│  Speakers: Speaker A (Interviewer) · Speaker B (Client PM)        │
│  Duration: 42:18 · Words: 6,240 · Intelligence: Gemma 4 E2B      │
└────────────────────────────────────────────────────────────────────┘
```

**UX principles:**

- The transcript panel is the left pane. The intelligence panel is the right pane. Both are visible simultaneously.
- Quick action buttons produce results immediately without typing.
- Chat supports multi-turn follow-up within the intelligence panel.
- Clicking a timestamp citation in an intelligence response scrolls the transcript panel to that location.
- Export supports both the raw transcript and generated intelligence artifacts (JSON, Markdown, plain text).
- The first UX optimizes for fast retrieval of useful answers, not for an open-ended "chat with AI" aesthetic.

---

## CLI and MCP Shape

### CLI

```bash
# Single question
voxflow ask "What decisions were made?" --input transcript.voxflow.json

# Structured extraction
voxflow extract action-items --input transcript.voxflow.json --output actions.json
voxflow extract summary --input transcript.voxflow.json --output summary.json
voxflow extract objections --input transcript.voxflow.json --format json

# Interactive chat
voxflow chat --input transcript.voxflow.json
```

### MCP

Intelligence tasks are exposed as MCP tools:

- `ask_transcript` — Q&A over a transcript artifact
- `extract_summary` — generate meeting summary
- `extract_action_items` — extract action items
- `extract_decisions_risks` — extract decisions and risks
- `extract_objections` — extract customer objections
- `extract_findings` — extract research findings
- `search_transcript` — fact search over transcript content
- `chat_transcript` — multi-turn chat over a transcript

This makes VoxFlow a composable node in agent workflows. An MCP client can transcribe a file, extract action items, and push them to a task tracker — all through the same local pipeline.

---

## Initial Scope

**In scope:**

- Local transcript Q&A (Ask Your Transcript)
- Built-in transcript chat over the current transcript with multi-turn support
- Quick action buttons for common extractions (Summarize, Actions, Decisions, Risks, Objections, Findings)
- Structured JSON outputs for all approved extraction types
- Transcript-grounded search for facts, commitments, and speaker-specific statements
- Speaker-filtered queries and summaries when ADR-024 data is available
- Evidence citation in all responses (quotes, timestamps, speaker labels)
- Desktop split-pane transcript workspace with intelligence panel
- CLI commands for Q&A and structured extraction
- MCP tool exposure for agent workflows
- Chapter and topic generation
- Schema-versioned output contracts

**Not in scope:**

- Replacing Whisper.net for ASR
- Replacing ADR-024 diarization with Gemma
- Fully autonomous editing of transcript text
- Mandatory Gemma dependency for any user
- Cloud-only Gemma flows
- Raw-audio-first UX for long recordings
- Generic ungrounded assistant chat mode
- Cross-transcript search (Phase 2)
- Real-time streaming intelligence during transcription

---

## Alternatives Considered

| Alternative | Why rejected |
|---|---|
| Replace Whisper.net with Gemma 4 for primary transcription | Conflicts with Gemma's documented 30-second audio limit. Weakens the proven long-form ASR pipeline for no product gain. |
| Use Gemma 4 as the diarization engine | Gemma is not positioned as a diarization or word-timing model. ADR-024 with pyannote remains the diarization path. |
| Use Gemma 4 only through remote APIs | Conflicts with local-first and privacy-first positioning. Users chose VoxFlow because their audio never leaves their machine. |
| Do not add a local LLM layer at all | Leaves the most valuable product surface untouched. Users need answers, not transcripts. Competitors who add intelligence will capture this value. |
| Ship structured outputs without chat | Covers predefined extractions but misses the iterative knowledge work pattern. Users need to follow up, drill down, and ask unexpected questions. Chat is the interaction model for that. |
| Ship chat without structured outputs | Covers Q&A but misses workflow integration. A chat answer is ephemeral; a structured JSON artifact is composable. Both are required. |
| Ship a generic open-domain chat that happens to see the transcript | Produces uneven, ungrounded results. Harder to test, harder to trust. A grounded transcript workspace with explicit actions and evidence citation is a stronger product than a generic assistant with a transcript in its context. |
| Use a cloud LLM (GPT-4, Claude) with optional local fallback | Inverts the architecture. VoxFlow's value is local-first. Cloud should be the optional layer, not the default. Gemma 4 keeps the default path fully local. |

---

## Trade-offs Accepted

- **Complexity increase.** The intelligence layer adds a local model runtime, model weight management, prompt engineering, and output evaluation to the product surface. This is significant ongoing investment beyond the transcription pipeline.
- **Non-deterministic outputs.** Structured extractions and chat answers are model-generated and inherently less deterministic than the core transcript. Evaluation, prompt hardening, and schema validation are required to maintain quality.
- **Hardware requirements.** Not all users will have sufficient local compute for Gemma 4, especially larger variants. The product must degrade gracefully — intelligence features are optional, not gating.
- **UX distinction burden.** The product must clearly separate raw transcript truth from model-generated interpretation. Mixing them risks user confusion and trust erosion.
- **Prompt maintenance.** Each structured output type requires a maintained prompt template, output schema, and evaluation suite. The number of supported extraction types directly correlates with maintenance cost.
- **Long transcript handling.** Transcripts that exceed the model's context window require windowing, chunking, or summarization strategies. This adds implementation complexity and may affect answer quality for very long recordings.

---

## Consequences

- **VoxFlow evolves from a transcription utility into a local audio intelligence workspace.** The product story changes from "transcribe locally" to "understand your recordings locally."
- **The core transcription pipeline remains stable and testable** even if intelligence features are disabled. No regression risk to the existing product.
- **Desktop gains the highest-value user-facing feature** it has ever had: the ability to ask questions about your own recordings and get immediate, evidence-backed answers.
- **CLI and MCP become workflow primitives.** Transcript artifacts can be turned into structured work products — summaries, action items, risk registers — that feed into downstream automation.
- **The product has a natural premium tier.** Intelligence features are the obvious candidate for monetization if VoxFlow becomes commercial, without gating the core transcription capability.
- **Future capabilities compound.** Cross-transcript search, project-level synthesis, automated follow-up detection, and longitudinal analysis all build on the same intelligence abstraction without reworking the transcript pipeline.

---

## Release Order

1. **Core intelligence abstraction.** Add `ITranscriptIntelligenceService` to `VoxFlow.Core` with bounded task contracts: `AnswerTranscriptQuestion`, `GenerateMeetingSummary`, `ExtractActionItems`, `ExtractDecisionsAndRisks`. Define output schemas in `docs/contracts/intelligence/`. Integrate local Gemma runtime.
2. **Desktop transcript workspace.** Split-pane UI with transcript on the left, intelligence panel on the right. Quick action buttons. Single-question Q&A.
3. **Built-in chat.** Multi-turn chat over the current transcript. Chat history persistence. Evidence citation in responses.
4. **Structured output expansion.** Add `ExtractCustomerObjections`, `ExtractResearchFindings`, `GenerateChapters`. JSON export from Desktop. CLI extraction commands.
5. **Speaker-aware intelligence.** Speaker-filtered queries and summaries using ADR-024 data. "Summarize only Speaker B." "What did the client say about pricing?"
6. **MCP intelligence tools.** Expose all intelligence tasks as MCP tools for agent workflow integration.
7. **Cross-transcript search (Phase 2).** Bounded multi-transcript chat and search. Project-level synthesis.
