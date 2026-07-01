---
name: execute-spec
description: Safely implement a tracked Specs/*.md PRD or handoff spec with careful repo inspection, checklist execution, surgical code changes, required validation, acceptance-criteria reporting, and git hygiene. Use when the user asks to execute, implement, build, or carry out a spec, PRD, feature plan, architecture plan, or agent handoff document.
---
# Execute Spec

Implement the target spec as a contract. Preserve its goals, non-goals, validation steps, and git guidance.

## Intake

1. Read `AGENTS.md`.
2. Read `SKILLS.md`.
3. Read the requested `Specs/*.md` file completely.
4. Inspect `git status --short --branch`.
5. Identify user changes already present and avoid overwriting them.

If the user does not name a spec file, list likely files under `Specs/` and ask which one to execute.

## Spec Gate

Before editing, confirm the spec has:

- `Goals`
- `Non-Goals`
- `Current Behavior`
- `Proposed Architecture`
- `FRs`
- `NFRs`
- `Validation And Testing`
- `Plan`
- `Acceptance Criteria`
- `Open Questions`
- `Git Specifics`

If an open question blocks implementation, ask before proceeding. If it does not block, state the assumption and continue.

## Execution Workflow

1. Summarize the implementation target in a short checklist.
2. Follow the spec's plan in order unless local evidence requires a safer order.
3. Keep edits surgical and aligned with existing code patterns.
4. Preserve every listed non-goal.
5. Do not push, pull, or use network unless the user explicitly approves it.
6. Do not use destructive git commands unless the user explicitly requests them.
7. Update `CHANGELOG.md`, `SKILLS.md`, and docs when required by `AGENTS.md` or the spec.

## Verification

Run the validation commands named by the spec. Prefer targeted build/test commands.

If a validation command cannot run, report:

- command attempted
- reason it could not run
- risk left unverified

## Final Report

Include:

- changed files
- validation commands and results
- acceptance criteria status, one by one: `Pass`, `Fail`, or `Not tested`
- any remaining risks or follow-up questions

Keep the final report concise and evidence-based.
