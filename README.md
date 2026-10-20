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

## Recent Generator Behavior Notes

- `AddDigital(..., qualityBit: true)` creates the QB input and connects it, inverted, to `ERR_EXT` on the main monitor and all configured extra monitors. With `false`, it creates no QB input/tag and removes this device's old generated QB connections during regeneration; unrelated wiring is preserved. Existing PLC tags are not deleted by this correction.

- Unified HMI detection in `SiemensPortal.cs` is capability-based: it recursively scans device items for `HmiSoftware` and no longer requires `System:Device.PC` or `IPC_`.
- Plant generation now preserves existing `Plant` FB networks and unit calls. It adds a new unit-call network only when the current unit is not already called anywhere in `Plant`.
- `Plant` preservation is intentional so legacy/manual networks like `BV160-B110` remain intact while adding new units such as `BL170`.

Trailing `//` descriptions on `AddAnalog`, `AddDigital`, `AddValve`, `AddValveControl`, `AddMotor`, `AddMotorControl`, and `AddPidControl` calls are written to the called FB instance's direct `Static` declaration comment. For example, `// Level radar sensor BV130 B13` describes instance `LC-B13` of `fbMonAn`. Analog/digital monitor `_InstanceN` declarations receive the same description. For `AddValve`, `AddValveControl`, `AddMotor` (`fbMotS`), and `AddMotorControl` (`fbMotAn`), every configured ordinary and safe `_InterlockN` declaration (`fbIntlck7`) also receives the exact parent description. Out-of-range helpers and unrelated manual instances retain their own comments. Cached calls use the original configured target counts; changing counts does not reconcile topology. This works for new, existing, and cached devices in both staging modes, using the selected app's caller source file.

Empty or unavailable source descriptions preserve existing comments. Nonempty text replaces only the supported target language (en-US, otherwise de-DE, otherwise the first supported culture); other translations and nested comments, including the unit wrapper's `FUNC_TEXT`, remain intact. Source descriptions no longer populate FBD network comments. Existing network comments are preserved. Instance descriptions are independent of network formatting; SCL generation and regions remain future work.

Run `./Tests/Verify-DeviceInstanceComments.ps1` after building V21 for offline device routing, declaration/comment preservation, and Siemens interface-schema checks. The fixture bypasses side-effecting constructors and uses in-memory DB caches with no live Portal session. Static-column display and live repeat generation still require a separately authorized disposable-project test.

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

