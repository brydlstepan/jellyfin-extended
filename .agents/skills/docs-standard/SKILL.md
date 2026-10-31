---
name: docs-standard
description: The one documentation layout and format for every project. Use when creating, editing, or reorganising any project docs (README, architecture, ADR, runbook, glossary), or when the user asks to "document" something.
---

# Docs standard

Every project uses the same layout. Create a file only when there is something real to put in it.

```
README.md                  ← root: what it is, how to run it (short)
PLAN.md                    ← root: what we're going to build, until it's built
docs/
├── ARCHITECTURE.md        ← how the parts fit together
├── GLOSSARY.md            ← project terms (owned by the domain-modeling skill)
├── adr/NNNN-slug.md       ← one decision per file
├── runbooks/slug.md       ← one operational task per file
└── agents/                ← config for agent skills (issue-tracker.md, ...)
```

There is no hand-written `CHANGELOG.md`; the `changelog` skill generates it from commits.

## Which doc for what

| You want to record... | Doc | Template |
|---|---|---|
| What this is and how to start it | `README.md` | [README.md](./templates/README.md) |
| The whole project's goal, scope, and first milestone, before it's built (one feature's spec goes to the tracker) | `PLAN.md` | [PLAN.md](./templates/PLAN.md) |
| The parts and how data flows between them | `docs/ARCHITECTURE.md` | [ARCHITECTURE.md](./templates/ARCHITECTURE.md) |
| A decision and why, with the options you rejected | `docs/adr/NNNN-slug.md` | [adr/template.md](./templates/adr/template.md) |
| Steps to do an operational task (restore, deploy, rotate) | `docs/runbooks/slug.md` | [runbooks/template.md](./templates/runbooks/template.md) |
| What a project-specific word means | `docs/GLOSSARY.md` | load the `domain-modeling` skill |

If content fits none of these, put it in the nearest one rather than adding a new kind of doc. Ask before adding a new top-level file to `docs/`.

## Writing rules (all docs)

- Markdown only. One `#` title per file, then `##` sections. Sentence case headings.
- Lead with the answer. First paragraph says what this doc is for, in 1-2 sentences.
- Short sentences. Active voice. Use the terms in `docs/GLOSSARY.md`.
- Commands in fenced blocks with the language set (`bash`, `powershell`). Every command must be copy-paste runnable; put values to replace in `<angle-brackets>`.
- Tables for comparisons and reference lists. Bullets for unordered items. Numbered lists only for steps in order.
- Link to other docs with relative paths. Don't copy content between docs; link to the one source.
- Diagrams: Mermaid in a fenced `mermaid` block, so they diff in git.
- No secrets, internal IPs with credentials, or tokens. Use `<placeholder>` and say where the real value lives (password manager, `.env`).
- Dates as `YYYY-MM-DD`.

## Keeping docs current

- When a change makes a doc wrong, fix the doc in the same commit (`docs` changes ride along with the `feat`/`fix`).
- Keep `PLAN.md` current while the first milestone is being built. Once it ships, move what's still true into `README.md` and `docs/ARCHITECTURE.md`, and delete `PLAN.md`.
- Never edit an accepted ADR's decision. Write a new ADR that supersedes it and set the old one's status to `Superseded by NNNN`.
- A runbook gets a `Last verified: YYYY-MM-DD` line. Update it when someone actually ran the steps.
