# Changelog

All notable project and generator changes should be recorded here.

## 2026-10-20

### Fixed

- Digital QB inputs/tags and inverted `ERR_EXT` connections now follow `qualityBit` for the base and all extra monitors in both staging modes. Regeneration repairs prior generated QB connections when disabled, preserving unrelated wiring. Added offline coverage for new/exported/cached paths, both flag values, 0/1/2 extra monitors, input inventory/tag flags, exact connections/inversion, and idempotent repair. Live TIA compilation remains unverified.

- Fixed duplicate `FB_ON`/`QCMD_ON` wires when creating `fbMotS` with interlocks in generated DB/UDT mode. The interlock branch and common staging branch both wrote the same pins; the interlock branch now handles marker mode only. Added unique-connection and exact staging-reference checks plus the reported BV130_B10_20 topology. Both V21 Debug outputs build and offline checks pass; live TIA import remains unverified.

- Extended interlock description inheritance to `AddMotor` (`fbMotS`) and `AddMotorControl` (`fbMotAn`): all configured ordinary/safe `fbIntlck7` declarations receive the exact motor source description, including cached and empty-first updates. Expanded the existing interlock test matrix to both motor routes; V21 Debug|x64 build and both offline regression scripts pass. Live TIA verification remains untested.

- G-013: valve and control-valve descriptions now also update every configured ordinary/safe `fbIntlck7` Static declaration, including cached updates after an empty initial description. Analog/digital monitor inheritance remains intact; motor interlocks and unrelated helpers keep independent comments. Expanded offline coverage for counts, both staging modes, existing/cached paths, languages, schema, and comment-only preservation. V21 Debug|x64 build and both offline regression scripts pass; V20 APIs are unavailable and live TIA verification remains untested.

- G-012: trailing device-call descriptions now update direct Static FB-instance comments across the seven analog/digital/valve/motor/PID routes, including monitor instances and cached devices. Stop copying them to FBD network comments while retaining existing network metadata, translations, and nested FUNC_TEXT. Added offline routing/preservation/schema checks; V21 app/solution Debug|x64 builds and staging checks pass. V20 APIs are unavailable; live TIA display/import remains untested.

- Updated the V21 new-DB template fallback to emit engineering version V21 and the required empty global Namespace. After the UDT fixes, generated staging reached dbIO import but failed because its fallback still used V17. Existing exported DBs and V20 fallback XML remain unchanged. Expanded the offline staging regression check to exercise the actual DB loader and SetMember path, including complete DB version/identifier checks and interface-schema validation; both Debug outputs pass.
- Added the required empty global `Namespace` identifier to V21 generated UDTs after the first staging UDT import reported it missing. Corrected V21 staging member/comment namespaces in UDTs, nested dbIO members, and unit interfaces; Siemens' installed interface XSD rejects the former unqualified members. Added `Tests/Verify-V21StagingXml.ps1` for offline schema and identifier checks. Both V21 Debug outputs pass; V20 serialization stays guarded and unchanged. Runtime import remains to be retested.
- V21 generated DB/UDT staging now emits UDT XML with engineering version `V21`, rather than hard-coded `V17`; installed V21 help lists V18–V21 as supported import versions. This is guarded by `TIA_V21` in the V21 library project, preserving the V20 XML version. Both V21 Debug builds and in-memory UDT version checks pass; the user's option-2 exception and runtime retry are still needed to confirm its complete failure path.
- Restored the missing FlgNet/v4 `Parts` and `Wires` containers in `EmptyNetwork.xml`, matching the user-provided original library source. The incomplete template caused `XmlCall` to throw `NullReferenceException` when creating the first unit call. Reproduced the failure offline and verified call/access/wire construction and serialization after the fix; both V21 Debug outputs were rebuilt and checked.
- Restored original `EmptyUnit.xml`, `EmptyOperation.xml`, `EmptyPhase.xml`, and `EmptyNetworkWithOutCall.xml` templates from the user-provided original app output. Both library projects now copy all ten templates. Missing `EmptyUnit.xml` caused generation to stop after creating an empty unit folder.
- Verified V21 `Debug|x64` rebuild and `Debug|AnyCPU` build, template XML parsing, and matching output hashes without running the generator. The external original files were read only.

### Added

- Added the separate `etu-process-generator-TiAv21` app and solution, with distinct identities, preserving the V20 app/solution and current `BV260-B210` definitions, staging modes, and disabled SiVArc generation.
- Added `TIA_LIB.V21.csproj`, sharing existing library source and template lists while isolating V21 output/intermediate directories and referencing installed modular V21 .NET Framework 4.8 APIs. No shared-source API changes were needed.
- Added configurable `TiaV21ApiDirectory`, explicit missing-API build errors, and generated V21 runtime configuration bindings with Siemens Copy Local disabled according to installed V21 help.

### Validation

- V21 `Debug|x64` app rebuild and solution build passed; reference identities, config paths, source parity, artifact isolation, and six template copies checked. Missing V21 API directory fails explicitly.
- V20 regression compilation is blocked by the missing standard V20 installation. V21 runtime loading, attachment, XML imports, staging modes, and HMI behavior remain untested; no generator execution or TIA project conversion was performed.
- Restored existing pinned NuGet packages with user authorization. Restore reports moderate vulnerability advisories for existing BouncyCastle 1.8.9; versions remain unchanged.

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
