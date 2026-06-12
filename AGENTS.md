# AGENTS.md

Guidance for coding assistants working in this repository.

## Role

Act as a careful senior engineer. Make surgical, evidence-based changes. Prefer reading the code and project notes before proposing or editing.

## Project Context

This is a TIA Portal generator project. `UnifiedSprechstunde15/Project.cs` is a declarative generator script; most behavior lives in external `TIA_LIB`, usually at:

```text
C:\TIAOpenness\TIA_Lib\TIA_LIB
```

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
- Building `UnifiedSprechstunde15/UnifiedSprechstunde15.csproj` is safer than building the full `.sln` if the solution references stale external paths.
- Running the app connects to or starts TIA Portal and mutates the open TIA project; do this only when intended.
- When `TIA_LIB` changes, rebuild it and copy the updated `TIA_LIB.dll` and `.pdb` into `UnifiedSprechstunde15/bin/x64/Debug`.

## Change Tracking

- Update `CHANGELOG.md` for meaningful behavior, dependency, or generator changes.
- Update `SKILLS.md` with session learnings, quirks, bug causes, and fixes that future agents should remember.
- Keep `README.md` current when setup, build, or runtime assumptions change.
- Mention external file changes clearly, especially anything under `C:\TIAOpenness\TIA_Lib\TIA_LIB`.
