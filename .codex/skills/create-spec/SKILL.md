---
name: create-spec
description: Create handoff-ready PRD spec-sheet Markdown files for future repo upgrades. Use when the user asks to draft, create, serialize, or prepare a robust implementation spec, PRD, feature plan, architecture plan, or agent handoff document under Specs/ with project context and git specifics.
---
# Create Spec

Create a self-contained Markdown spec that a future agent can use without reconstructing the whole conversation.

## Workflow

1. Read the repo guidance first:
   - `AGENTS.md`
   - `SKILLS.md`
   - relevant existing `Specs/*.md` files, if any
2. Inspect `git status --short --branch` before editing.
3. Create or update one focused file under `Specs/`.
4. Name the file with a stable lowercase slug, for example `Specs/G-00x-unit-device-id-namespacing.md`.
5. Do not implement the feature unless the user explicitly asks. The output of this skill is the spec.
6. Update `CHANGELOG.md` and `SKILLS.md` only when creating the spec changes repo process or captures a reusable project learning.

## Required Spec Sections

Include these sections, in this order unless the user asks otherwise:

```markdown
# <Spec Nr.><Spec Title>

## Summary
## Problem
## Goals
## Non-Goals
## Current Behavior
## References
## Proposed Architecture
## FRs
## NFRs
## Validation And Testing
## Plan
## Acceptance Criteria
## Open Questions
## Git Specifics
```

## Writing Rules

- Make the spec serialized and handoff-ready: include enough context, exact file paths, expected commands, assumptions, and constraints.
- Keep the proposal precise, not essay-like.
- Separate behavior requirements from implementation steps.
- Mark unresolved decisions in `Open Questions` instead of hiding assumptions.
- Include relevant branch, commit, cleanup, and push guidance in `Git Specifics`.
- Preserve current project decisions unless the spec explicitly proposes changing them.

## Validation

Before finishing:

- Confirm the file is visible to Git and not ignored, if it is ignored, inform.
- Check that every required section exists.
- Report changed files and whether any validation/build command was unnecessary.
