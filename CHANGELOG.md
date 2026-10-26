# Changelog

All notable project and generator changes should be recorded here.

## 2026-10-26

### Added

- The user confirmed successful live FBD generation after the G-019 power-flow ordering correction. Both V21 Debug builds and all thirteen offline suites against both outputs also pass. The wider SCL/live timing, aliasing and repeat-generation matrix remains separately documented.
- G-019 Unit mode plan shared by FBD/SCL: five main actuator/PID families receive cyclic OFF, and the four valve/motor families receive both Service FEATURE writes. Both groups execute after Devices/before Operations; empty groups are omitted. Devices retain next-call timing; Service flags overwrite manual/HMI edits cyclically without restoring prior values. Separate styled SCL OFF assignments are the documented chain-presentation fallback.
- Unit CFG now uses a direct Plant InOut alias to cfgData.<SafeUnit>; device CFG pins remain Input. Verified old Input members move intact to InOut, with strict alias/collision/ownership checks. A retained upgrade plan audits callers and records cyclic field ownership; unknown/uninspectable callers stop for reconciliation. Type folders, migration approvals, instance-DB preservation and compile/export guards remain in force.
- Offline inventory/namespace/interface fixtures, independent multi-scan SCL/FBD oracle, copied-Input counterexample, protected upgrade/caller checks and Siemens FBD schema validation. Live Portal aliasing, instance DB consistency and runtime behavior require a disposable-project trial.

### Fixed

- G-019 FBD mode graphs now serialize gates before their dependent coils and share positive outputs only across consecutive assignments. The next native Service import rejected interleaved branches with a power-flow ordering error at coil 44. Service pairs retain declaration order with separate gate branches; Off retains its contiguous shared fan-out. Ordering/branch regressions and the existing schema, wiring and scan checks pass in all thirteen suites against both rebuilt V21 outputs. Native import after this correction remains unverified.
- G-019 MODE Service FBD wiring now uses separate operand occurrences for each gate input, including shared TRUE literals. The subsequent TIA trial accepted MODE Off but rejected the Service IdentCon fan-out as a power rail. Boolean gate outputs retain signal fan-out; assignment values/order, inversion and SCL are unchanged. New point-to-point operand regressions, both V21 builds and all thirteen suites pass against both outputs; full live import retry remains pending.
- G-019 FBD mode assignments now drive positive coils through one shared single-input AND gate per source. TIA rejected direct IdentCon-to-Coil.in wiring during Unit import, despite generic XSD acceptance. The gate preserves source fan-out, inversion and scan semantics and matches the supplied FBD reference. Native connection-shape regressions now supplement schema and scan tests; both V21 builds and all thirteen suites pass against both outputs. A live import retry remains pending.
- G-019 mode-contract validation now reads inline Struct fields directly under their Member, matching the actual Siemens typeValveCfg export. It also retains support for Sections-wrapped fields and referenced types, while rejecting ambiguous/missing/non-Bool paths. This fixes the reported FBD preflight failure on CFG.FEATURE.IsAutoModeDefault. A minimal export-shaped regression and read-only replay of the failed run's actual valve contracts pass; both V21 builds and all thirteen offline suites pass against both outputs. Live generation retry remains pending.

## 2026-10-23

### Added

- The user confirmed successful G-018 generation after testing. Both V21 Debug configurations also compile with the current Hydrogenator 410 project definition. The confirmation is a reported live result; detailed Portal formatting/export, migration and repeat-generation cases remain documented separately.
- G-018: generated non-Phase SCL uses indented regions, aligned multiline arguments, preserved comments plus role-specific explanations, Unit Operations nesting and per-phase Operation separators. Closed Boolean invocation enables proven TRUE render directly; real/unknown/ENO gates, meaningful parameter values, call/output order, FBD shadows and Phase bytes remain protected. Prior block ownership records authorize style upgrades only after their original export fingerprint matches.
- Unit UDT folder planning now covers nested lookup, backup, preflight, recheck, import, compile, export and ownership publication in both languages. New type records retain folder/content fingerprints. Unsupported existing-root moves stop with a retained folder plan/backups; no delete/recreate fallback or implicit legacy/manual adoption. Focused goldens and device/helper/folder policy regressions supplement the eleven existing suites; live Portal placement/editor/separator behavior remains separately unverified.
- Failed modular block imports/compiles now retain API consistency and compile/modification timestamps, a fresh target/companion lookup, and a best-effort XML export in the ignored run folder. Export refusal is recorded without replacing the original failure. FBD XML capture uses Openness; no manual source-menu export is required. This improves diagnosis of the unresolved zero-error FBD consistency failure without bypassing publication checks.
- User-directed presentation updates: operation interlocks always use SCL in either selected mode, with Function comments, General Interlocks and per-phase regions. Unchanged generator-owned FBD interlocks may follow this explicitly requested conversion; manual edits remain protected. Device FBD titles no longer receive ETU group comments, and exact former headers are removed without deleting other comments. Console choices omit default/experimental/phase annotations; the IO prompt reads Select signal IOs generation style.
- User-directed phase-template correction: phase FBs retain the original SCL source in both FBD and SCL modes. Preserve complete IF/ELSIF/CASE structure, Action/Next comments, disabled step transition and empty transfer/status branches. Removed the flattened executable phase model from generation; ownership checks/capture use actual SCL phase language. Independent reference-body and runtime round-trip fixtures replace phase-lowering tests. This supersedes G-017's earlier FBD-phase requirement and phase renderer fixes below; other generated blocks keep language selection.
- Automatic execution error reports for both app versions, covering startup, PLC and HMI generation. Each failure saves timestamps, settings, full nested exception/stack trace and a bounded recent-console transcript in ignored GenerationErrors; an unwritable folder falls back to the user's temporary directory. The console shows the report path, failure returns exit status 1, and successful runs create no logs. Offline tests cover capture, Unicode, preservation of console behavior, repeated failures and fallback without Portal execution; V21 x64/AnyCPU builds pass.
- G-017 runtime integration in both apps: general frozen FBD/SCL selection, Unit control/configuration interfaces, Operation forwarding, seven device groups, supported body renderers and real FBD phase instructions. Plant remains FBD; vendor definitions remain external. Supersedes the preparation-only checkpoints below.
- Per-unit configuration UDTs and nested cfgData fields retain offline values, metadata and manual fields additively. Exact reviewed legacy-migration approval is required; old flat fields remain. No online-value capture, automatic external-consumer rewrites or download is added.
- Whole-block ownership records enable protected managed regeneration. Missing records, manual edits, language/staging changes and incompatible folders/contracts stop before replacement. Standalone Tp/Rp retain global HMI.RESET by user decision. Both language interlock starters now use cyclic FALSE assignments.
- Finalized batch preflight, private backups/plans/diagnostics, dependency rechecks and import/compile gates stop later callers after failure. GeneratedModels is ignored to keep private project exports out of Git. SCL uses tokenized XML bodies to preserve existing interface metadata.
- Offline phase oracle/FBD interpreter, ownership, migration approval, integrated rendering and actual dependency-scanner fixtures. V21 Debug x64/AnyCPU builds and ten offline suites pass; V20 APIs and live PLC import/runtime/migration validation remain unavailable/untested.

### Fixed

- The user confirmed successful FBD generation after adding the PLC-software compile fallback; SCL generation had already succeeded. Both V21 configurations build and all eleven offline suites pass. This verifies the reported trial, while production migration, runtime timing and V20 API validation remain separate gaps.
- Zero-error but inconsistent modular blocks now receive one PLC-software dependency compile after the two block-only passes. The latest FBD diagnostics confirmed that fresh lookups and native export still reject fbTK003 even though block compilation reports up-to-date; the user reports successful project-level compilation. The fallback retains all software compiler diagnostics and requires actual block consistency before ownership export/Plant import. Tests cover recovery, no unnecessary fallback, compiler failures and persistent inconsistency; successful live recovery remains to be verified.
- Unit FB publication now completes its planned global instance DBs before requiring final consistency/export and importing Plant. The latest FBD run stopped before creating TK003's instance DB, leaving its prepared Plant call unpublished. Definition compile errors still stop immediately; companion instances preserve backed-up offline values, are prepared once, and must compile before ownership capture/callers. Fake-backend tests verify Unit/DB/export/Plant order and failure gating; live resolution remains unverified.
- Interactive execution failures now wait for a key after printing the error-log path, preventing an immediate console-window close. Redirected execution and offline tests do not pause; failures still return status 1.
- Compilation now checks PlcBlock.IsConsistent before ownership export and dependent imports. One extra pass is allowed when a zero-error compile updated FBD calls but left the block inconsistent; persistent inconsistency or actual compile errors stop with retained diagnostics. This addresses the latest fbTK003 post-compile export failure; offline gate tests cover bounded retries and preserved failures, while live retry remains unverified.
- FBD interlock Function headings now use an empty NetworkSource, matching the repository's blank-network template. TIA rejected the previous empty FlgNet/Parts graph after all restored SCL phases had imported successfully. The Function title and executable precondition networks are retained. Both V21 builds and targeted phase/interlock/routing suites pass; live retry remains pending.
- FBD phase NOT expressions now push inversion to direct Boolean operands using De Morgan transformations, comparison inversion and a Boolean gate for standalone negated coil/call/enable inputs. TIA rejected a negation on an OR-to-AND wire in the TRANSFER_READY guard. Regression checks forbid gate-to-gate inversion; both V21 builds and independent 4096-scan phase checks pass. Live import retry remains pending.
- FBD phase rendering no longer emits an unsupported Type template parameter on MOVE. The latest execution log showed Siemens rejecting the first phase import despite general FlgNet XSD validation passing. MOVE retains output cardinality and typed operand connections. An instruction-specific regression and the 4096-scan phase parity checks pass against both rebuilt V21 outputs; live import retry remains pending.
- G-017 library-pin preflight now recognizes generic String input call markers against bounded String declarations. The retained fbMonAn export declares UNIT as String[10], which the former exact text comparison incorrectly rejected. Explicit bound mismatches, String/WString differences and Output/InOut checks remain strict; mismatch diagnostics include generated and actual types. Targeted offline fixtures cover valid/invalid contracts; live import is still separate validation.
- G-017 backup exports no longer nest run IDs below the long project/CPU hash directory. The normal bin/Debug TEMP backup path exceeded Windows MAX_PATH; run artifacts now use GeneratedModels/Runs/<run-id>, while existing ownership-ledger paths remain unchanged. Preflight checks the longest export/ledger paths before mutation and gives a shorter-working-directory diagnostic when necessary. Offline regressions cover the reported location, artifact lengths, unique runs and legacy ledger compatibility; V21 x64/AnyCPU builds and targeted suites pass. Live export retry remains unverified.
- Managed constructor reentry retains local CFG operands instead of rewriting them to legacy globals. Fresh/reopened operand updates keep scope, wire identity, negation and output fanout; fresh open-pin indexes and legacy unqualified constant/global setters now remain consistent on repeated declarations.
- Shared grouping checks include enclosing-instance/field hazards and independent intervening networks. Conditional phase writes preserve no-write behavior through set/reset coils; template-body edits are rejected before rendering. User Project.cs definitions are preserved separately.

## 2026-10-22

### Added

- G-017 development: add supported FB/Boolean network models, readable and tokenized SCL body renderers, protected device grouping and configuration UDT/DB proposal serialization. Offline fixtures cover all seven device families, monitors/helpers, typed CFG bindings and interface schemas. Global runtime activation, complete phase conversion and migration application remain in development.
- G-017 planning checkpoint (not enabled in runtime generation): explicit control/reference/configuration ownership models, metadata-preserving configuration snapshots, dependency ordering and device grouping hazard checks. Added offline planning fixtures and a contract/artifact audit of retained trial XML. Complete FBD/SCL backends, runtime adapters and migration protections remain unimplemented; the existing selector and generator behavior are unchanged. Standalone Tp/Rp global resets are retained by user instruction.
- G-017 follow-up preparation: verified the supplied current library exports, confirming Input CFG on all seven families and Bool HSS on fbControlSfc. Added a parameterized Boolean-input reconciler, an offline Unit/Operation/Plant control-routing pass and read-only configuration migration inventory with consumer/backup/online-value checks. These preparation passes are not activated in runtime generation; full backend/managed-content integration remains pending. Existing G-016 behavior is retained through its compatibility entry point.
- G-016: every generated unit, operation and phase `fbControlSfc` call now connects Input `HSS : Bool` directly to global `F_Safe.HSS`, without inversion. Existing operation selectors are resolved explicitly as well as new calls; cached model paths retain the connection.
- Focused HSS reconciliation adds missing connections, corrects simple wrong/negated operands, preserves other consumers of shared operands, and rejects ambiguous drivers/topology with block/network/instance context. Correct wiring is unchanged and does not dirty the block. No hardware IO or fallback DB/member is created. The target must already contain the updated `fbControlSfc` and `F_Safe.HSS : Bool`; controller import errors retain the original exception and include this prerequisite.
- Added `Tests/Verify-SfcHss.ps1`: executable shared-call configuration tests cover unit/operation/multiple phases, fresh and qualified v4/v5 exports, both staging modes/interlock languages, exact reopen/idempotence, caches/dirty state, shared/manual preservation, wrong call identities and unsupported topology. Live Siemens import/compile remains separate validation.

### Fixed

- Fixed adding devices to an already generated V21 unit: all seven device constructors now discover the unit alarm/warning OR blocks using the unit network's actual FlgNet namespace. Hard-coded v4 lookups missed exported v5 wiring and added another driver to `fbUnit.ALARM` for each new device, causing duplicate-connection import failures. Offline regressions cover all families across two edit/add/export/reopen passes in both modes and v4/v5, retaining existing status wires and manual comments.
- G-015: `AddPidControl` no longer registers unused `<name>_SP` / `<name>_OUT` hardware outputs in either staging mode. Its `fbPIDCtrl` call, Static instance description, configuration, reset/alarm and HMI behavior remain unchanged; existing Project.cs calls need no edits.
- Deferred unit staging interfaces and Plant bindings to inventory preparation so fresh PID-only units do not reference nonexistent hardware UDTs/dbIO members. Units with real IO retain the same staging names and bindings. Existing interfaces, bindings, old PID-only artifacts and manual content are not swept or deleted; regenerated UDTs for participating units reflect their current inventory.
- Extended the offline device-staging suite for fresh/cached PID calls, PID-only and mixed units, configuration/comments, legitimate similarly named outputs, XML schema validation and existing-artifact preservation. V21 Debug x64/AnyCPU builds and all four offline suites pass; V20 APIs are absent and no live Portal run was performed.

## 2026-10-21

### Added

- G-014: independent startup FBD/SCL selection for operation interlock FBs in both apps, with FBD as default and a frozen pre-construction setting. Shared pure SCL rendering finalizes ordered distinct phases at upload; general and per-phase declarations/regions use padded `01..50` suffixes and cyclic FALSE starter assignments.
- Batch interlock preflight detects CPU-global/requested name conflicts, folder/language/type mismatches and incompatible existing SCL interfaces. Matching SCL blocks are reused unchanged; conversion/merge/deletion is not automatic. New SCL FBs use Siemens external sources with explicit target folders, compilation diagnostics and retained failure evidence; repeated upload enumeration does not regenerate them.
- Added complete SCL source snapshots and offline planning/preservation/failure/call-contract fixtures in `Tests/Verify-OperationInterlocks.ps1`. V21 Debug x64/AnyCPU builds, new fixtures and existing staging XML checks pass. V20 build is blocked by absent installed APIs; the device-comment regression suite is now restored and passes alongside the newer checks. Live source generation/interface/EN-ENO verification remains pending; the generator was not run.

### Fixed

- Consolidated the current tested generator work with updated remote main, which already contained the merged G-012/G-013 and author-identity branches. Preserved the latest local BV130/A2 Project.cs separately, retained enabled-QB reconciliation from main alongside the newer disabled-input repair, and resolved duplicated description setup introduced by the merge. All four offline regression suites are part of the consolidated build workflow.

- Restored device instance-description behavior from local commit `6dee8e1` (`g-012-device-instance-comments`) into the active V21-fix branch. That implementation was absent from this branch's older main baseline, so rebuilding had brought back network descriptions. All seven device Add paths now update direct Static instance comments, including configured monitor/interlock instances; constructors no longer write these descriptions as network comments. Existing network text is preserved. Recovered description/schema/preservation tests run alongside the newer staging-input tests; current SCL, namespace and A2 IO fixes are retained. Restored the companion motor staging single-connection guard required by the original topology regression.

- Fixed A2's undefined staged digital quality inputs: additional `fbMonDi` monitors now honor `qualityBit=false`, and marker-tag planning receives the same flag. Valve feedback generation now independently honors `mon_opn` and `mon_cls`, instead of referencing both sides when either monitor is enabled. Reruns repair exact stale disabled-input operands with neutral Bool constants while preserving UIds, wires, negations and unrelated references. Valve feedback checks use the existing network's namespace to avoid duplicating enabled logic after export. New offline tests cover actual Add methods, both staging modes, monitor counts, feedback/quality combinations and qualified v4/v5 reruns. Live recompilation remains pending; the pasted log's 90 warnings were not individually included.

- SCL operation interlocks now accept hyphenated phase/operation names. A2's `PH_Re-Inert`, `PH_H2-replace` and `PH_N2-replace` were rejected by an overly restrictive identifier validator. Hyphenated Static members are quoted consistently in declarations and local assignments while model/interface/region names remain intact; simple-name output is unchanged. Full-source offline fixtures cover quoting, exact raw interface names and rejection of unsafe source characters.

- Existing FBD network parsing now reads the FlgNet namespace from each network instead of assuming v4. The old assumption could miss existing calls/Parts and crash at XmlCall.AddFirst on a rerun. Network helpers share the source namespace; additions to newer networks retain it, while legacy v4 generation stays unchanged. Offline v4/v5 round-trip fixtures cover call reuse, manual XML preservation, added calls/parameters/accesses/wires and reference/value edits. Missing editable containers now produce a diagnostic before adding a call. The user's exact unit export/live rerun still needs confirmation; the separate no-merge boundary for new SCL phase members remains unchanged.

- Fresh FB templates in V21 builds now emit engineering version V21 and an empty global Namespace identifier. Adding a phase reached an interlock template with an unsupported V17 header; the operation template had the same issue. Existing exported blocks and V20 template loading remain unchanged. Extended offline checks cover unit, operation, phase, and interlock FB template metadata while preserving all other XML content.

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
