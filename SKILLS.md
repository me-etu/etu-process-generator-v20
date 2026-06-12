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

### Plant FB Preservation

Problem:

- `Plant` import failed with `Cannot create the 'SW.Blocks.CompileUnit' object with Simatic ML ID '8'`.
- Failed XML showed old `BV160-B110` networks mixed with new `BL170`.

Findings:

- `XmlBlock` exports the existing TIA block first.
- Earlier forced `XmlPlant` to start from `EmptyPlant.xml` with `overwrite = true`; this fixed stale bad XML but deleted all existing Plant networks/calls.
- Desired behavior is additive merge: preserve existing Plant networks and only add missing current unit calls.

Current fix:

- `C:\TIAOpenness\TIA_Lib\TIA_LIB\Xml\XmlPlant.cs`
- `XmlPlant` constructor no longer passes `overwrite = true`.
- `GetUnit` now checks both network title and existing calls via `FindUnitCallNetwork`.
- Existing calls such as `fbBV160-B110` / `BV160-B110` are preserved.
- New calls such as `fbBL170` / `BL170` are added only if missing.

Important:

- After editing external `TIA_LIB`, rebuild `TIA_LIB` and copy `TIA_LIB.dll` plus `TIA_LIB.pdb` into `UnifiedSprechstunde15\bin\x64\Debug`.

### Unified Panel Detection

Problem:

- Original `SiemensPortal.cs` HMI detection only recognized PC/IPC devices:
  - `System:Device.PC`
  - `IPC_`
- Unified Comfort panels are not IPCs.

Current fix:

- `C:\TIAOpenness\TIA_Lib\TIA_LIB\SiemensPortal.cs`
- Device detection recursively scans each device's `DeviceItems` for `SoftwareContainer`.
- It finds `HmiSoftware` and `PlcSoftware` by capability instead of type identifier.
- It throws explicit errors when HMI software, PLC software, or HMI connection is missing.

### Build And Reference Quirks

- The app project references `UnifiedSprechstunde15\bin\x64\Debug\TIA_LIB.dll` directly.
- The `.sln` may still contain a stale external project path for `TIA_LIB.csproj`.
- Building `UnifiedSprechstunde15\UnifiedSprechstunde15.csproj` directly is more reliable than building the full solution.
- Known build command:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' `
  'UnifiedSprechstunde15\UnifiedSprechstunde15.csproj' `
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

