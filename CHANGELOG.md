# Changelog

All notable project and generator changes should be recorded here.

## 2026-06-16

### Added

- Documented the simulator device UI template workflow for new/generated units, including the future G-008/G-009 integration note.

## 2026-06-15

### Added

- Added `GENERATOR_KNOWLEDGE_BASE.md` as the canonical generator knowledge base covering mental model, DSL methods, snippet workflow, generated tags, troubleshooting, and safe workflows.
- Added `GENERATOR_ISSUES.md` to track generator behavior issues before implementation.

### Changed

- Consolidated `CHAT_KNOWLEDGE_TRANSFER.md`, `GENERATOR_SNIPPETS_KNOWHOW.md`, and `PROJECT_CS_METHODS.md` into `GENERATOR_KNOWLEDGE_BASE.md`; the old split docs are now local/ignored rather than tracked documentation.
- Renamed the app project from `UnifiedSprechstunde15` to `etu-process-generator-TiAv20`.
- Updated the app namespace to `EtuProcessGeneratorTiaV20`.
- Renamed the solution and app project files to `etu-process-generator-TiAv20.sln` and `etu-process-generator-TiAv20\etu-process-generator-TiAv20.csproj`.

## 2026-06-12

### Added

- Added this `CHANGELOG.md` to track project changes before setting up git.
- Added `README.md` with project purpose, runtime dependency notes, generation flow, and build guidance.
- Added current `BL170` generator definition in `UnifiedSprechstunde15/Project.cs`.
- Added `AGENTS.md`, `SKILLS.md`, and `.gitignore` to support future coding-agent sessions and safe version control setup.

### Changed

- Standardized the repository on the local `TIAOpenness\TIA_Lib\TIA_LIB` source tree instead of the stale nested `UnifiedSprechstunde15\TIA_LIB` copy.
- Updated the solution to include `TIA_LIB` and changed the app from a direct `bin\x64\Debug\TIA_LIB.dll` reference to a project reference.
- Repointed `TIA_LIB` package and Siemens DLL hint paths to repo-relative NuGet packages and `$(ProgramFiles)`.
- Reduced active `Project.cs` content to the current `BL170` scope only.
- Replaced Siemens HMI detection in external `TIA_LIB` so `SiemensPortal.cs` finds `HmiSoftware` by recursively scanning `SoftwareContainer` on device items instead of relying on PC/IPC type identifiers.
- Changed external `TIA_LIB` Plant generation to preserve existing Plant networks and existing unit calls. The generator now adds a Plant network only when the current unit call is missing.
- Rebuilt external `TIA_LIB` and copied the updated `TIA_LIB.dll` / `TIA_LIB.pdb` into `UnifiedSprechstunde15\bin\x64\Debug`.

### Fixed

- Fixed Plant import failures caused by stale/invalid old Plant networks being mixed destructively with newly generated units.
- Fixed Unified Comfort panel detection where no IPC/PC runtime exists.
- Corrected the BL170 valve tag from `Q-11` back to `Q11`.
- Prevented `CheckTags()` from deleting manually/multiplexed HMI tags whose HMI tag name does not match the PLC tag path.

### Notes

- `TIA_LIB` source of truth is now inside this repository:
  - `TIAOpenness\TIA_Lib\TIA_LIB\Xml\XmlPlant.cs`
  - `TIAOpenness\TIA_Lib\TIA_LIB\SiemensPortal.cs`
- App project builds successfully with `Debug|x64`; current warnings are non-blocking.
