# ETU process generator — TIA Portal V20 and V21

TIA Portal generator project for creating PLC function blocks, PLC tags, and Unified HMI-related objects from a declarative C# project definition.

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

Both initially select unit `BV260-B210`; other unit definitions remain inside `#if false`. SiVArc generation remains commented out. Future plant-definition edits must target the intended app version.

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

At startup, the console asks for signal staging mode. Pressing Enter keeps the default marker-memory behavior. Selecting generated DB/UDT staging prepares `hwIN_<SafeUnitName>`, `hwOUT_<SafeUnitName>`, and `dbIO` artifacts, adds `hwIN`/`hwOUT` unit FB interfaces, and wires Plant unit calls to `dbIO.<SafeUnitName>.IN/OUT`.
Before that choice, both apps independently ask for the language of **operation interlock FBs only**: `1. FBD` (Enter/default) or `2. SCL`. Phase, operation, unit and device block languages are unaffected. Programmatic callers may set `PlcProject.InterlockLanguage = OperationInterlockLanguage.Scl` before constructing `Project`; changing language after model construction begins is rejected.
`Project.cs` performs these side-effecting steps:

```csharp
CreateTags();
Upload();
Portal.CheckTags();
// GenerateSiVarc(); // currently disabled
Portal.DeleteRenamedTags();
UserInterface();
Portal.Portal.Dispose();
```

This connects to an open or newly started TIA Portal instance and modifies the open TIA project.

### Operation interlocks in SCL (G-014)

`AddPH("BV130_B10_20", "OP5_Pressure", "PH_Vent")` and `AddPH(..., "PH_Fill")` generate one `OP5_Pressure_Interlocks` FB in the existing operation folder. The upload-time descriptor collects distinct phases in first declaration order. It renders an optimized version-0.1 FB with Static `State : Program_Alarm` version 1.0, 50 general Bool preconditions and 50 per phase (`01..50`). `AddOP` alone creates no interlock. The operation's `Interlocks` network, local multi-instance `Interlocks`, called block name and EN wiring retain their existing contract.

The SCL starter has an empty Function region followed by General Interlocks and one region per phase. **Every FALSE assignment executes cyclically.** Calculations added in Function can be overwritten by later FALSE assignments; adjust those defaults when implementing interlocks. These lines are starter logic, not initialization values or process-specific logic.

Before upload, preflight checks the complete batch for duplicate CPU-global names, generated-block conflicts, existing blocks in other folders, language/type mismatches and incompatible SCL interfaces. Compatible SCL blocks are reused without importing source or changing their body, comments or extra members. Missing requested phase members cause an actionable error: this release performs no conversion, additive merge, replacement, movement or deletion. Reconcile such blocks explicitly. Existing FBD interfaces retain the previous additive generation path; name/folder/language conflicts now stop before upload.

The backend creates one uniquely named Siemens external source for each new SCL FB and uses `GenerateBlocksFromSource(targetGroup, GenerateBlockOption.KeepOnError)`, then compiles the resulting block and reports nested diagnostics and warnings. It checks folder placement and the exported required interface, including Program_Alarm version/public access and State visibility/access/SetPoint flags. It does not hard-code Program_Alarm internals. Failed sources and `OperationInterlockSources/<run-id>/<block>.scl` plus `diagnostics.txt` remain for investigation; callers are not imported after a generation failure. Only the source created by that successful generation is removed from Portal. Retained files are not automatically deleted. Each interlock generates at most once per prepared batch despite the legacy repeated upload enumeration.

Backend evidence: installed V21 `Help/en-US/TIAPortalOpennessenUS.zip` topics `37231359755/131792485771.htm` (target group overload), `119205656075.htm` (KeepOnError and explicit compilation), and `53581488011.htm` (generation overwrites existing blocks). The installed `PublicAPI/V21/net48/Siemens.Engineering.Step7.dll` metadata confirms the signatures, result types and source deletion API. Existing-block preflight is therefore mandatory. The renderer is compiled into both library projects; no runtime template asset or redirected-drive access is required. SCL identifiers accept ASCII letters, digits, underscores and hyphens, up to 128 characters including generated suffixes, starting with a letter or underscore. Hyphenated phase names retain their identity: `"PH_Re-Inert_PRECOND_01" : Bool;` is referenced as `#"PH_Re-Inert_PRECOND_01" := FALSE;`. Simple identifiers keep their existing spelling. Quotes, control characters, non-ASCII names and other unsupported inputs fail with their AddPH identity before import.

Offline validation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tests/Verify-OperationInterlocks.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tests/Verify-V21StagingXml.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tests/Verify-DeviceStagingInputs.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tests/Verify-DeviceInstanceComments.ps1
# Repeat against AnyCPU output with:
# -LibraryPath ./etu-process-generator-TiAv21/bin/Debug/TIA_LIB.dll
```

On 2026-10-21 V21 Debug x64 and AnyCPU builds and both offline scripts passed. The fixtures compare complete SCL snapshots, cover both staging modes, cached repeated AddPH, preservation, preflight failures and repeated uploads, and exercise the existing FBD call builder without Portal constructors. V20 compilation was attempted but its standard Siemens API installation is absent; V20 API compatibility remains unverified. `Tests/Verify-DeviceInstanceComments.ps1` was initially absent because the G-012/G-013 dependency was missing from this branch; that implementation and its regression suite have now been restored. No generator or live Siemens source compilation was executed. Disposable-project generation, exact compiled interface/access parity, EN/ENO caller compilation, placement and edited-body reruns still require explicitly authorized live verification.

## Recent Generator Behavior Notes

- Unified HMI detection in `SiemensPortal.cs` is capability-based: it recursively scans device items for `HmiSoftware` and no longer requires `System:Device.PC` or `IPC_`.
- Plant generation now preserves existing `Plant` FB networks and unit calls. It adds a new unit-call network only when the current unit is not already called anywhere in `Plant`.
- Trailing device descriptions in `Project.cs` are written to the direct Static instance declaration comments, including configured monitor and interlock instances. New device descriptions are not written as network comments. Existing network text is preserved. The implementation and `Tests/Verify-DeviceInstanceComments.ps1` were recovered from local G-012/G-013 commit `6dee8e1`, which was absent from the active V21-fix branch's older main baseline.
- `Plant` preservation is intentional so legacy/manual networks like `BV160-B110` remain intact while adding new units such as `BL170`.

## Reference Files

- `GENERATOR_KNOWLEDGE_BASE.md`: canonical generator mental model, DSL reference, snippet workflow, tag behavior, troubleshooting, and safe workflows.
- `GENERATOR_ISSUES.md`: tracked notes for generator behavior issues and design candidates.
- `unit_template.xlsx`: planning workbook for G-009-style unit spec sheets. It is a local template artifact only and does not connect to or run TIA Portal.

## Build Notes

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

