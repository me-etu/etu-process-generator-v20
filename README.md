# etu-process-generator-TiAv20

TIA Portal generator project for creating PLC function blocks, PLC tags, and Unified HMI-related objects from a declarative C# project definition.

## Purpose

`etu-process-generator-TiAv20/Project.cs` is the active generator script. It describes the plant model through helper calls inherited from `TIA_LIB.PlcProject`, such as:

- `AddDigital(...)`
- `AddAnalog(...)`
- `AddValve(...)`
- `AddValveControl(...)`
- `AddMotor(...)`
- `AddMotorControl(...)`
- `AddPH(...)`
- `AddTp(...)`
- `AddRp(...)`

The current active project definition is intentionally small and generates unit `BL170`.

## Important Runtime Dependency

Most generator behavior lives in the repo-local `TIA_LIB` source tree:

```text
TIAOpenness\TIA_Lib\TIA_LIB
```

The app references this project directly through the solution/project files. The old machine-level copy can still exist for comparison, but normal builds should not depend on:

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
GenerateSiVarc();
Portal.DeleteRenamedTags();
UserInterface();
Portal.Portal.Dispose();
```

This connects to an open or newly started TIA Portal instance and modifies the open TIA project.

## Recent Generator Behavior Notes

- Unified HMI detection in `SiemensPortal.cs` is capability-based: it recursively scans device items for `HmiSoftware` and no longer requires `System:Device.PC` or `IPC_`.
- Plant generation now preserves existing `Plant` FB networks and unit calls. It adds a new unit-call network only when the current unit is not already called anywhere in `Plant`.
- `Plant` preservation is intentional so legacy/manual networks like `BV160-B110` remain intact while adding new units such as `BL170`.

## Reference Files

- `GENERATOR_KNOWLEDGE_BASE.md`: canonical generator mental model, DSL reference, snippet workflow, tag behavior, troubleshooting, and safe workflows.
- `GENERATOR_ISSUES.md`: tracked notes for generator behavior issues and design candidates.
- `unit_template.xlsx`: planning workbook for G-009-style unit spec sheets. It is a local template artifact only and does not connect to or run TIA Portal.

## Build Notes

Preferred workflow is Visual Studio with `Debug|x64`.

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

