# UnifiedSprechstunde15

TIA Portal generator project for creating PLC function blocks, PLC tags, and Unified HMI-related objects from a declarative C# project definition.

## Purpose

`UnifiedSprechstunde15/Project.cs` is the active generator script. It describes the plant model through helper calls inherited from `TIA_LIB.PlcProject`, such as:

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

Most generator behavior lives outside this workspace in:

```text
C:\TIAOpenness\TIA_Lib\TIA_LIB
```

The app currently references the built DLL copied into:

```text
UnifiedSprechstunde15\bin\x64\Debug\TIA_LIB.dll
```

After changing `TIA_LIB`, rebuild it and copy the rebuilt `TIA_LIB.dll` and `TIA_LIB.pdb` into the app output folder before running from Visual Studio.

## Current Generation Flow

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

- `CHAT_KNOWLEDGE_TRANSFER.md`: debugging and project handoff notes.
- `GENERATOR_SNIPPETS_KNOWHOW.md`: rules for preparing generator snippets.
- `PROJECT_CS_METHODS.md`: project DSL method reference.
- `TASK.txt`: current source task for `Project.cs` changes.

## Build Notes

Preferred workflow is Visual Studio with `Debug|x64`.

The solution file may contain an outdated external `TIA_LIB.csproj` path. The app project itself can build against the local copied `TIA_LIB.dll`.

When validating with MSBuild, build the app project directly:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' `
  'UnifiedSprechstunde15\UnifiedSprechstunde15.csproj' `
  /p:Configuration=Debug /p:Platform=x64 /m
```

