---
name: etu-generator-warmup
description: Efficient warm-up for the etu-process-generator-TiAv20 TIA Portal generator repo. Use when starting a new session, preparing to edit Project.cs or repo-local TIA_LIB, debugging generator behavior, checking repo state before a task, or getting oriented without reading unnecessary files.
---
# ETU Generator Warm-Up

Act as a careful senior engineer. Build only the context needed for the user's task, then stop reading and report readiness.

## Baseline

1. Inspect state:

```powershell
git status --short --branch
```

2. Read `AGENTS.md` only if its instructions are not already present in the conversation.
3. Read `SKILLS.md` for recent live memory. If the file is large, scan headings first and read only relevant sections.
4. Do not use the internet. Do not run the generator app. It attaches to or starts TIA Portal and mutates the open project.
5. Treat repo-local `TIAOpenness/TIA_Lib/TIA_LIB` as source of truth. Do not use `C:/TIAOpenness/TIA_Lib/TIA_LIB` unless explicitly asked.

## Task Routes

For a general warm-up:

- Read `README.md`.
- Inspect `etu-process-generator-TiAv20/Project.cs`.
- Inspect project references only when needed:
  - `etu-process-generator-TiAv20/etu-process-generator-TiAv20.csproj`
  - `TIAOpenness/TIA_Lib/TIA_LIB/TIA_LIB.csproj`
- Check `TASK.txt`, `Specs/`, and `SimulationFiles/` only if the user's task mentions active unit scope, specs, simulator UI, or handoff work.

For `Project.cs` edits:

- Read relevant `GENERATOR_KNOWLEDGE_BASE.md` sections for the exact `Add...` methods being changed.
- Confirm method signatures in `TIAOpenness/TIA_Lib/TIA_LIB/PlcProject.cs`.
- Check `SimulationFiles/DeviceUi_*.json` only when devices are added, removed, renamed, or regenerated.

For `TIA_LIB` behavior changes:

- Read the direct call path first, usually one or more of:
  - `TIAOpenness/TIA_Lib/TIA_LIB/PlcProject.cs`
  - `TIAOpenness/TIA_Lib/TIA_LIB/SiemensPortal.cs`
  - `TIAOpenness/TIA_Lib/TIA_LIB/Xml/XmlPlant.cs`
  - `TIAOpenness/TIA_Lib/TIA_LIB/Devices/<device>.cs`
  - `TIAOpenness/TIA_Lib/TIA_LIB/Xml/<xml-area>.cs`
- Use `GENERATOR_KNOWLEDGE_BASE.md` as targeted reference, not a mandatory full read.

For spec work:

- Use `create-spec` or `execute-spec` when those skills match the request.
- Read only the named `Specs/*.md` file plus files it explicitly references.

For docs-only questions:

- Read only the docs needed to answer.
- Avoid code reads unless the docs conflict or the user asks for verification.

## Validation

Prefer compile-only validation. The safer project build is:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' `
  'etu-process-generator-TiAv20/etu-process-generator-TiAv20.csproj' `
  /p:Configuration=Debug /p:Platform=x64 /m
```

Do not run the app unless the user explicitly intends TIA Portal side effects.

## Readiness Report

Keep the warm-up report short:

- branch and worktree state
- files read
- active project scope, if inspected
- important discrepancies or risks found
- safe next validation step
- confirm no edits unless edits were requested

Update `SKILLS.md` after meaningful reusable discoveries, behavior changes, or environment quirks.