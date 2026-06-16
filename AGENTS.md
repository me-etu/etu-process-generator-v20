# AGENTS.md

Guidance for coding assistants working in this repository.

## Role

Act as a careful senior engineer. Make surgical, evidence-based changes. Prefer reading the code and project notes before proposing or editing.

## Project Context

This is a TIA Portal generator project. `etu-process-generator-TiAv20/Project.cs` is a declarative generator script; most behavior lives in repo-local `TIA_LIB` at:

```text
TIAOpenness\TIA_Lib\TIA_LIB
```

An older machine-level copy may exist at `C:\TIAOpenness\TIA_Lib\TIA_LIB`, but normal repository work should use the repo-local project unless the user explicitly asks to compare or sync external files.

Important notes live in:

- `README.md`
- `CHANGELOG.md`
- `SKILLS.md`
- `CHAT_KNOWLEDGE_TRANSFER.md`
- `GENERATOR_SNIPPETS_KNOWHOW.md`
- `PROJECT_CS_METHODS.md`

Read the relevant notes before changing generator behavior. Treat `SKILLS.md` as live session memory and update it with reusable discoveries.

## Operating Rules

- Make the smallest change that solves the stated problem.
- Do not guess when repository evidence can answer the question.
- Ask before making high-impact behavior changes.
- Preserve user changes and existing generated/manual TIA content unless explicitly asked to replace it.
- Do not edit files outside the allowed workspace unless the user explicitly approves.
- Do not use the internet unless the user explicitly asks or permits it.
- Do not run destructive commands such as reset, clean, or recursive deletion unless the user explicitly requests them.

## Before Editing

- Inspect the relevant source and call path.
- Identify the expected behavior and the failure mode.
- Think through the test or verification step first.
- For `Project.cs`, confirm method signatures in `PROJECT_CS_METHODS.md`.
- For snippet work, follow `GENERATOR_SNIPPETS_KNOWHOW.md`.

## Testing And Verification

- Prefer targeted compile checks over broad side-effecting runs.
- Building `etu-process-generator-TiAv20/etu-process-generator-TiAv20.csproj` is safer than building the full `.sln` if the solution references stale external paths.
- Running the app connects to or starts TIA Portal and mutates the open TIA project; do this only when intended.
- When `TIA_LIB` changes, rebuild the solution or app project in `Debug|x64`; the app references the repo-local `TIA_LIB.csproj`.

## Git Workflow

- Before starting a change, inspect `git status` and understand any existing user or staged changes.
- Sync with `main` before new work when network/remote access is available and the user has approved it.
- Create a small, focused branch from `main` for each task.
- Keep commits surgical: one logical change per commit, with docs/tests included when relevant.
- Do not mix unrelated cleanup with feature or bug-fix work.
- Push the branch only when the user asks or has approved remote access.
- Never use destructive Git commands such as `reset --hard`, `clean`, or checkout-based reverts unless the user explicitly requests them.

## Change Tracking

- Update `CHANGELOG.md` for meaningful behavior, dependency, or generator changes.
- Update `SKILLS.md` with session learnings, quirks, bug causes, and fixes that future agents should remember.
- Keep `README.md` current when setup, build, or runtime assumptions change.
- Mention external file changes clearly, especially if anything outside the repo-local `TIAOpenness\TIA_Lib\TIA_LIB` tree is touched.
