# Changelog

All notable project and generator changes should be recorded here.

## 2026-07-22

### Added

- Added optional generated DB/UDT signal staging mode with startup selection, unit `hwIN`/`hwOUT` interfaces, `dbIO` artifact planning, and mode-aware device signal references while preserving marker memory as the default.
- Added automatic unit name comments on the `fbUnit.FUNC_TEXT` Bool member for unit wrapper calls.
- Added automatic device network comments from trailing `Project.cs` device-call comments.
- Applied TASKS-UNITS/Vacud Updates/vacud_unit_template_payload.json to Project.cs, adding the remaining 20 Vacud unit sections and 122 generated device calls through the current marker-tag generator path.

## 2026-07-01

### Fixed

- Pruned unsupported multilingual cultures from generated PLC XML before import so templates with languages like es-ES, it-IT, fr-FR, or fr-BE do not fail in projects where those cultures are not enabled.

### Added

- Applied the enabled `Vakuumzentrale` unit workbook rows to `Project.cs` as generator calls for pressure, pump controls, valves, and maintenance switches.
- Added the project-local `etu-generator-warmup` Codex skill for efficient, task-routed session onboarding.
- Added `unit_template.xlsx` as a G-009 primer workbook for unit spec-sheet preparation before generation.
- Updated the G-008/G-009 planning docs and workbook template for optional `IOBindings` with unit-first `dbIO.<UnitName>.IN/OUT` staging paths.
- Simplified `IOBindings` so standard signal identity, direction, type, UDT membership, and dbIO paths are derived rather than manually typed.
- Added the project-local `fill-unit-workbook` skill for extracting Devices and IOBindings rows from mixed unit source evidence.

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
