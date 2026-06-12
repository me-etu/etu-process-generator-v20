# Changelog

All notable project and generator changes should be recorded here.

## 2026-06-12

### Added

- Added this `CHANGELOG.md` to track project changes before setting up git.
- Added `README.md` with project purpose, runtime dependency notes, generation flow, and build guidance.
- Added current `BL170` generator definition in `UnifiedSprechstunde15/Project.cs`.
- Added `AGENTS.md`, `SKILLS.md`, and `.gitignore` to support future coding-agent sessions and safe version control setup.

### Changed

- Reduced active `Project.cs` content to the current `BL170` scope only.
- Replaced Siemens HMI detection in external `TIA_LIB` so `SiemensPortal.cs` finds `HmiSoftware` by recursively scanning `SoftwareContainer` on device items instead of relying on PC/IPC type identifiers.
- Changed external `TIA_LIB` Plant generation to preserve existing Plant networks and existing unit calls. The generator now adds a Plant network only when the current unit call is missing.
- Rebuilt external `TIA_LIB` and copied the updated `TIA_LIB.dll` / `TIA_LIB.pdb` into `UnifiedSprechstunde15\bin\x64\Debug`.

### Fixed

- Fixed Plant import failures caused by stale/invalid old Plant networks being mixed destructively with newly generated units.
- Fixed Unified Comfort panel detection where no IPC/PC runtime exists.
- Corrected the BL170 valve tag from `Q-11` back to `Q11`.

### Notes

- External source changed outside this workspace:
  - `C:\TIAOpenness\TIA_Lib\TIA_LIB\Xml\XmlPlant.cs`
  - `C:\TIAOpenness\TIA_Lib\TIA_LIB\SiemensPortal.cs`
- App project builds successfully with `Debug|x64`; current warnings are non-blocking.
