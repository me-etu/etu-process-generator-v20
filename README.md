# ETU process generator — TIA Portal V20 and V21

TIA Portal generator project for creating PLC function blocks, PLC tags, and Unified HMI-related objects from a declarative C# project definition.

G-017 is connected to generation in both apps. See the [implementation and validation audit](Docs/G-017-implementation-audit.md) for contracts, migration safeguards and offline results. V21 builds and offline checks pass; live PLC import, runtime equivalence and legacy migration still need a disposable-project trial.

## Purpose

Each version has its own declarative `Project.cs`: `etu-process-generator-TiAv20/Project.cs` and `etu-process-generator-TiAv21/Project.cs`. They describe the plant model through helper calls inherited from `TIA_LIB.PlcProject`, such as:

- `AddDigital(...)`
- `AddAnalog(...)`
- `AddValve(...)`
- `AddValveControl(...)`
- `AddMotor(...)`
- `AddMotorControl(...)`
- `AddPH(...)`
- `AddTp(...)`
- `AddRp(...)`


The active units are defined independently in each version's `Project.cs`; inspect the selected version before generating. SiVArc generation remains commented out.

## Important Runtime Dependency

Most generator behavior lives in the repo-local `TIA_LIB` source tree:

```text
TIAOpenness\TIA_Lib\TIA_LIB
```

The V20 app references `TIA_LIB.csproj`; the V21 app references `TIA_LIB.V21.csproj` beside it. Both library projects share the same implementation and template files. Keep their explicit Compile/Content/None item lists aligned when adding or removing files. The old machine-level copy can still exist for comparison, but normal builds should not depend on:

```text
C:\TIAOpenness\TIA_Lib\TIA_LIB
```

Do not hand-link `TIA_LIB.dll` from `etu-process-generator-TiAv20\bin\x64\Debug`; Visual Studio/MSBuild should copy the referenced project output automatically.

## Current Generation Flow

Both apps first ask `Select language for code generation:`. Enter selects FBD; `2` selects SCL. The independent hardware staging prompt follows. Set `PlcProject.Language` (`TIA_LIB.Generation.GenerationLanguage`) before constructing `Project` when calling programmatically. The old `InterlockLanguage` API is a compatibility alias for the general choice; conflicting explicit choices are rejected. Language and migration approval are frozen at construction.

The selected language covers generated Unit, Operation and standalone Tp/Rp bodies. **Phases and operation interlocks remain SCL in both modes**, by user decision. Phases retain the original template: complete IF/ELSIF and CASE structure, S10/S20 regions, Action/Next comments, disabled step transition, transfer placeholders and status branches. Interlocks retain Function (with explanatory comments), General Interlocks and per-phase regions. Plant stays FBD and retains unrelated/manual networks. Vendor/library definitions are dependencies, not rewritten. Changed/manual bodies require explicit reconciliation.

Every Unit receives five Input Bool parameters, in both languages and staging modes:

| Unit input | Plant operand |
| --- | --- |
| iReset | HMI.RESET |
| iReset_Delayed | HMI.RESET_DELAYED |
| iMode_Off | HMI.MODE_OFF |
| iMode_Service | HMI.MODE_SERV |
| iHSS | F_Safe.HSS |

Unit controllers use local iReset/iHSS. Devices use iReset and applicable PROT uses iHSS. Operations receive HSS/Delayed_Reset from the Unit; selectors use HSS, and their phase controllers use HSS/Delayed_Reset. The selector's previously unassigned Reset stays unassigned. The checked fbUnit/fbControlSfc definitions have no mode inputs, so no vendor mode pins are invented. These existing global Bool members and the updated library must be present; no HMI/F_Safe fallback DB is created. Standalone fbTPs/fbRPs retain HMI.RESET by user decision, with their external callers unchanged. Monitor RESET_CYCL remains separate.

Configuration is grouped under `cfgData.<SafeUnit>` of type `udtCfg_<SafeUnit>`, supplied to Unit Input CFG. Calls use local `CFG.<exact-instance-name>`, retaining punctuation and every configured monitor leaf. All seven target device CFG contracts were verified as Input. Offline values, metadata and manual fields are retained additively. A fresh empty Unit has controls but no CFG/UDT; a PID-only Unit has controls/PID CFG but no hardware IO.

Device logic groups as Digital_Sensors, Analog_Sensors, Digital_Valves, Analog_Valves, Motors_Direct, Motors_Control and PID_Controllers. SCL nests original network titles inside Devices/category regions. FBD retains device network titles without generated group comments; former exact ETU group headers are removed while other comments remain. Monitor/interlock helpers stay with their device, and descriptions remain on Static instances. A schedule that would reverse a read/write dependency is rejected.

G-018 formats generated non-Phase SCL with four-space region/IF indentation, aligned multiline arguments, single blank separators and explanatory comments. Units group contiguous operation calls under Operations; Operation bodies insert an empty 17-asterisk separator before each phase group. Device regions also show their selected-language Static description once. Existing network comments take precedence over generated purpose comments. Closed Boolean enable trees proven TRUE omit only their SCL invocation wrapper; variable, false, comparison and ENO-dependent conditions remain. Argument expressions, helper flags, output assignments, FBD shadows and Phase templates retain their semantics/content.

Actual generated Unit UDTs target PLC data types/<SafeUnit>, in either language. Types are imported, compiled and exported through that folder, and new ownership records include its path and the exported-content fingerprint. Existing root types stop before replacement: installed V21 PlcType/type-group metadata exposes no verified identity-preserving move operation, and older v1 records contain no content fingerprint. Review the retained type-folder-plan.xml and backups for explicit reconciliation; automatic delete/recreate or adoption after a manual move is disabled. Fresh nested creation and verified nested reruns are supported. No types are invented for empty/PID-only hardware inventories, and unrelated legacy types are untouched. See [G-018 validation](Docs/G-018-implementation-validation.md) for offline evidence and remaining Portal checks.

Marker-memory staging remains the default. Generated DB/UDT staging prepares `hwIN_<SafeUnit>`, `hwOUT_<SafeUnit>` and `dbIO` only for actual hardware inventory. `AddPidControl` contributes no hardware IO.

`CreateTags()` prepares the finalized model, validates library pins/names/dependencies, and exports backups before marker-table changes. `Upload()` rechecks artifacts and dependency snapshots, then imports/compiles prerequisites before callers. Failure stops subsequent imports and retains diagnostics. SCL uses tokenized StructuredText in SimaticML, preserving enclosing interface metadata; the older G-014 external-source backend remains compatibility code.

### Execution error reports

Both apps automatically save execution failures in `GenerationErrors` under the working directory (normally the selected app's `bin/Debug` or `bin/x64/Debug`). Each dated `.log` contains the full exception/inner exceptions/stack trace, UTC timestamps, language/staging settings and the last 40,000 characters of console output. The console prints the full report path; send that file when reporting a generation error. Interactive runs wait for a key before exiting after failure, so the error remains visible. Redirected runs skip the pause. Failure returns status 1.

Successful runs create no report. If the normal log folder cannot be written, the logger tries `%TEMP%\EtuGeneratorErrors` and prints that location instead. `GenerationErrors/` is ignored by Git. Existing detailed import diagnostics and backups under `GeneratedModels` are retained separately. Logs are local diagnostic files and may contain project names/paths and console content.

### Regeneration and migration

Keep the generated `GeneratedModels` directory beside the output used to run the app. It contains private backups, plans, diagnostics and ownership records scoped to saved project path/PLC identity. It is ignored by Git. Run from the same output directory for subsequent generation, and keep the same language/staging mode. Ownership records are written only after successful import, compilation and export.

Matching managed blocks can be regenerated and extended. Changed/manual bodies, missing ownership, a language/staging change, wrong folders and incompatible contracts stop for reconciliation. Existing pre-G-017 FBs are not silently adopted or converted. The user's interlock-language amendment permits conversion of unchanged, recorded generator-owned FBD interlocks to SCL; manual edits still stop it. SCL interlock starters assign FALSE cyclically to all general/per-phase preconditions; these are starter logic, not initialization values.

Legacy flat configuration requires review of `configuration-plan.xml` and `configuration-approval.template.xml` in the retained run directory. After verifying backups, all external consumers and separate online-value protection, supply the completed approval with `--configuration-migration-approval <reviewed-approval.xml>`. It must match the exact project/CPU, original cfgData snapshot and every old/new/type mapping; the default template cannot authorize a migration. Unknown consumers stop it. Old flat members remain, and their values can diverge from nested configuration; no automatic dual-write synchronization is added. This approval does not waive FB ownership/manual-content guards.

No online actual values are captured and no PLC download is performed by this migration. First validate generation and repeat generation in a disposable project copy; the generator still modifies the open TIA project, creates tags and HMI objects, and retains the existing post-upload Project.cs flow.

### Offline verification

Build the V21 app in Debug x64 and AnyCPU, then run all `Tests/Verify-*.ps1` suites listed in the [audit](Docs/G-017-implementation-audit.md). Scripts default to the x64 DLL; repeat with `-LibraryPath ./etu-process-generator-TiAv21/bin/Debug/TIA_LIB.dll` for AnyCPU. These tests bypass live Portal constructors. They verify model/rendering/schema/routing/preservation behavior, not PLC execution.

## Recent Generator Behavior Notes

- `AddDigital(..., qualityBit: true)` creates the QB input and connects it, inverted, to `ERR_EXT` on the main monitor and all configured extra monitors. With `false`, it creates no QB input/tag and neutralizes exact stale generated QB references during regeneration while preserving their wire topology and unrelated wiring. Existing PLC tags are not deleted by this correction.

- Unified HMI detection in `SiemensPortal.cs` is capability-based: it recursively scans device items for `HmiSoftware` and no longer requires `System:Device.PC` or `IPC_`.
- Plant generation now preserves existing `Plant` FB networks and unit calls. It adds a new unit-call network only when the current unit is not already called anywhere in `Plant`.
- Trailing device descriptions in `Project.cs` are written to the direct Static instance declaration comments, including configured monitor and interlock instances. New device descriptions are not written as network comments. Existing network text is preserved. The implementation and `Tests/Verify-DeviceInstanceComments.ps1` were recovered from local G-012/G-013 commit `6dee8e1`, which was absent from the active V21-fix branch's older main baseline.
- `Plant` preservation is intentional so legacy/manual networks like `BV160-B110` remain intact while adding new units such as `BL170`.

Trailing `//` descriptions on `AddAnalog`, `AddDigital`, `AddValve`, `AddValveControl`, `AddMotor`, `AddMotorControl`, and `AddPidControl` calls are written to the called FB instance's direct `Static` declaration comment. For example, `// Level radar sensor BV130 B13` describes instance `LC-B13` of `fbMonAn`. Analog/digital monitor `_InstanceN` declarations receive the same description. For `AddValve`, `AddValveControl`, `AddMotor` (`fbMotS`), and `AddMotorControl` (`fbMotAn`), every configured ordinary and safe `_InterlockN` declaration (`fbIntlck7`) also receives the exact parent description. Out-of-range helpers and unrelated manual instances retain their own comments. Cached calls use the original configured target counts; changing counts does not reconcile topology. This works for new, existing, and cached devices in both staging modes, using the selected app's caller source file.

Empty or unavailable source descriptions preserve existing comments. Nonempty text replaces only the supported target language (en-US, otherwise de-DE, otherwise the first supported culture); other translations and nested comments, including the unit wrapper's `FUNC_TEXT`, remain intact. Source descriptions no longer populate FBD network comments. Existing network comments are preserved. Instance descriptions are independent of network formatting; selectable SCL operation interlocks are described above.

Run `./Tests/Verify-DeviceInstanceComments.ps1` after building V21 for offline device routing, declaration/comment preservation, and Siemens interface-schema checks. The fixture bypasses side-effecting constructors and uses in-memory DB caches with no live Portal session. Static-column display and live repeat generation still require a separately authorized disposable-project test.

## Reference Files

- `GENERATOR_KNOWLEDGE_BASE.md`: canonical generator mental model, DSL reference, snippet workflow, tag behavior, troubleshooting, and safe workflows.
- `GENERATOR_ISSUES.md`: tracked notes for generator behavior issues and design candidates.
- `unit_template.xlsx`: planning workbook for G-009-style unit spec sheets. It is a local template artifact only and does not connect to or run TIA Portal.

## Build Notes

Use an up-to-date `main` as the baseline for new work. After fetching authorized remote updates, check `git status --short --branch` and `git log HEAD..origin/main --oneline` before creating a task branch. The consolidated generator includes the device instance-comment, V21 template, SCL interlock and staged-input fixes together. Run the offline suites in the G-017 audit after changes to the shared library, including `Tests/Verify-GenerationExecutionLog.ps1` for execution logging. `Specs/` and `SKILLS.md` remain ignored local handoff/session files; runtime templates and regression fixtures are tracked.

Preferred workflow is Visual Studio with `Debug|x64`.

| Version | Open solution | Library project | Siemens API directory |
| --- | --- | --- | --- |
| V20 | `etu-process-generator-TiAv20.sln` | `TIA_LIB.csproj` | `Portal V20/PublicAPI/V20` |
| V21 | `etu-process-generator-TiAv21.sln` | `TIA_LIB.V21.csproj` | `Portal V21/PublicAPI/V21/net48` |

Both apps retain .NET Framework 4.8, C# 7.3 for x64, and the existing pinned NuGet dependencies. V20 project/reference settings are preserved. V21 uses modular Siemens Base, Step7, WinCCUnified, and Sivarc assemblies, plus WinCC for the library's existing classic HMI import. These are V21 identities (`21.0.0.0`, token `29bfe5fdf4ba5d3b`), not renamed V20 references.

Restore NuGet packages first, then build the solution or the app project. NuGet packages are intentionally ignored by Git and restored from `packages.config`.

When validating with MSBuild:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' `
  'etu-process-generator-TiAv20.sln' `
  /t:Restore /p:RestorePackagesConfig=true

& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' `
  'TIAOpenness\TIA_Lib\TIA_LIB\TIA_LIB.csproj' `
  /p:Configuration=Debug /p:Platform=x64 /m

& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' `
  'etu-process-generator-TiAv20\etu-process-generator-TiAv20.csproj' `
  /p:Configuration=Debug /p:Platform=x64 /m
```

Running the app is not a harmless smoke test: it connects to TIA Portal and can mutate the open project.

V21 compile validation (does not run the generator):

```powershell
$msbuildPath = 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/amd64/MSBuild.exe'
& $msbuildPath 'etu-process-generator-TiAv21/etu-process-generator-TiAv21.csproj' /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /m /v:minimal
& $msbuildPath 'etu-process-generator-TiAv21.sln' /t:Build /p:Configuration=Debug /p:Platform=x64 /m /v:minimal
```

If packages are missing, restore the selected solution with `/t:Restore /p:RestorePackagesConfig=true` when network use is authorized. The existing BouncyCastle 1.8.9 restore reports moderate vulnerability advisories; dependency upgrades are separate from this migration.

Both V21 projects accept `/p:TiaV21ApiDirectory="D:/custom/Portal V21/PublicAPI/V21/net48"`. The default is under `$(ProgramFiles)/Siemens/Automation`. Missing required API files stop the build explicitly. V21 library outputs are `TIAOpenness/TIA_Lib/TIA_LIB/bin/V21/<Platform>/<Configuration>/`; intermediates and reference caches are under `obj/V21/<Platform>/<Configuration>/`. V20 outputs and caches are separate. The V21 app uses its own `bin/x64/Debug/` and receives all ten XML templates through the project reference, including the restored unit, operation, phase, and empty-network templates.

After building V21, run `./Tests/Verify-V21StagingXml.ps1` in Windows PowerShell for offline staging XML checks against the installed Siemens interface XSD. Use `-LibraryPath ./etu-process-generator-TiAv21/bin/Debug/TIA_LIB.dll` to check the AnyCPU output. The script checks UDT and complete DB fallback version/Namespace identifiers, UDT and nested dbIO members/comments, and unit staging interfaces without attaching to Portal or importing anything. These checks cover XML structure, not full TIA import semantics.

For a later authorized V21 runtime test, use an installed/licensed Portal V21 with the required PLC/Unified/SiVArc components, configured TIA Openness user access/firewall approval, and a disposable V21 project copy with the expected device libraries and templates. Set the working directory to `etu-process-generator-TiAv21/bin/x64/Debug/` (or the corresponding selected output), including when launching from Visual Studio: exports and some template reads use the current directory. Keep the generated `.exe.config` beside the executable.

Installed Siemens V21 help, **Connecting to the TIA Portal** (`Help/en-US/TIAPortalOpennessenUS.zip`, topic `37231359755/37752246795.htm`), says Copy Local is unsupported and documents application configuration bindings. Accordingly, V21 Siemens references use `Private=False`; the app build creates an intermediate `TIA.V21.App.config` with V21 `codeBase` paths from `TiaV21ApiDirectory` and deploys it as the executable configuration. No custom resolver or V20 binding redirects are used. Rebuild for the target installation path; do not copy Siemens DLLs alongside the executable.

On 2026-10-20 the V21 app rebuild and solution build passed against the installed V21 APIs, with no shared-source adaptations. Assembly identities, generated configuration paths, source parity, isolated artifacts, and template copies were checked without executing the app. A missing API directory correctly failed. V20 regression compilation is blocked by the absent standard V20 installation. Runtime loading, process attachment, XML import/export, Plant preservation, both staging modes, and HMI postprocessing remain untested on V21; compile success does not certify runtime compatibility.

