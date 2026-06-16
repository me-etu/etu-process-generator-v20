# SKILLS.md

Living memory for future coding-agent sessions.

Use this file to record practical learnings that are too specific or operational for the README, but important enough that the next session should not rediscover them.

## How To Use This File

- Read this file before changing generator behavior.
- Add concise notes after meaningful debugging, behavior changes, or environment discoveries.
- Prefer concrete file paths, symptoms, causes, and fixes.
- Keep entries dated newest-first.
- Do not use this as a general chat transcript; capture reusable knowledge only.

## 2026-06-12

### Application Project Rename

- The app project was renamed from `UnifiedSprechstunde15` to `etu-process-generator-TiAv20`.
- Current app project path: `etu-process-generator-TiAv20\etu-process-generator-TiAv20.csproj`.
- Current solution path: `etu-process-generator-TiAv20.sln`.
- The C# app namespace is `EtuProcessGeneratorTiaV20`.
- Historical notes may still mention `UnifiedSprechstunde15` when describing old paths, stack traces, or stale external copies.

### Repo-Local TIA_LIB Layout

Current source of truth:

- `TIAOpenness\TIA_Lib\TIA_LIB`

Findings:

- The old nested copy at `UnifiedSprechstunde15\TIA_LIB` was removed before the app rename.
- `TIAOpenness\TIAOpenness_ETU` appears unused; no current solution/project references point to it.
- The app should reference `TIA_LIB.csproj`, not a copied `TIA_LIB.dll` in `bin\x64\Debug`.
- NuGet packages are restored from `packages.config`; the `packages/` folder stays ignored.

Build quirk:

- Siemens Openness DLLs are referenced through `$(ProgramFiles)\Siemens\Automation\Portal V20\PublicAPI\V20`.
- Running the generator app is side-effecting; use compile checks for repository cleanup validation.

### HMI Tag Cleanup Safety

Problem:

- `Portal.CheckTags()` deleted a manually multiplexed HMI tag named `dbParameters_UnitPara`.
- The tag was unrelated to generated units; it only failed the old HMI tag name vs PLC tag path equality heuristic.

Current fix:

- `TIAOpenness\TIA_Lib\TIA_LIB\SiemensPortal.cs`
- `CheckTags()` now reports HMI/PLC tag mismatches and returns whether any were found.
- Actual HMI tag deletion remains limited to `DeleteRenamedTags()`, which removes names ending `_Renamed`.

### Plant FB Preservation

Problem:

- `Plant` import failed with `Cannot create the 'SW.Blocks.CompileUnit' object with Simatic ML ID '8'`.
- Failed XML showed old `BV160-B110` networks mixed with new `BL170`.

Findings:

- `XmlBlock` exports the existing TIA block first.
- Earlier forced `XmlPlant` to start from `EmptyPlant.xml` with `overwrite = true`; this fixed stale bad XML but deleted all existing Plant networks/calls.
- Desired behavior is additive merge: preserve existing Plant networks and only add missing current unit calls.

Current fix:

- `TIAOpenness\TIA_Lib\TIA_LIB\Xml\XmlPlant.cs`
- `XmlPlant` constructor no longer passes `overwrite = true`.
- `GetUnit` now checks both network title and existing calls via `FindUnitCallNetwork`.
- Existing calls such as `fbBV160-B110` / `BV160-B110` are preserved.
- New calls such as `fbBL170` / `BL170` are added only if missing.

Important:

- After editing repo-local `TIA_LIB`, rebuild the solution or app project so MSBuild copies the project-reference output.

### Unified Panel Detection

Problem:

- Original `SiemensPortal.cs` HMI detection only recognized PC/IPC devices:
  - `System:Device.PC`
  - `IPC_`
- Unified Comfort panels are not IPCs.

Current fix:

- `TIAOpenness\TIA_Lib\TIA_LIB\SiemensPortal.cs`
- Device detection recursively scans each device's `DeviceItems` for `SoftwareContainer`.
- It finds `HmiSoftware` and `PlcSoftware` by capability instead of type identifier.
- It throws explicit errors when HMI software, PLC software, or HMI connection is missing.

### Build And Reference Quirks

- The app project now references `TIAOpenness\TIA_Lib\TIA_LIB\TIA_LIB.csproj`.
- Build `Debug|x64` to match Siemens Openness and `TIA_LIB` AMD64 usage.
- Known build command:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' `
  'etu-process-generator-TiAv20.sln' `
  /t:Restore /p:RestorePackagesConfig=true

& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' `
  'etu-process-generator-TiAv20\etu-process-generator-TiAv20.csproj' `
  /p:Configuration=Debug /p:Platform=x64 /m
```

### Current Project Scope

- Active `Project.cs` is intentionally reduced to unit `BL170`.
- Current active generated components:
  - `QC-B24`
  - `QC-B14`
  - `Bl171-B13`
  - `Bl172-B13`
  - `Q11`
- `uS/cm` is used as the analog unit; display scaling handles the `1/10` detail.

### TIA Side Effects

- Running the app mutates the open TIA project.
- `Upload()` imports generated blocks.
- `GenerateSiVarc()` and `UserInterface()` affect Unified HMI objects.
- Use compile-only checks when the goal is validation without touching TIA.
