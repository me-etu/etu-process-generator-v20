# Generator Knowledge Base

Canonical knowledge base for the `etu-process-generator-TiAv20` TIA Portal generator.

This file supersedes:

- `CHAT_KNOWLEDGE_TRANSFER.md`
- `GENERATOR_SNIPPETS_KNOWHOW.md`
- `PROJECT_CS_METHODS.md`

Use this file first when changing `Project.cs`, preparing generator snippets, or debugging generator/runtime behavior.

## Purpose And Mental Model

This project generates TIA Portal PLC function blocks, PLC tags, and Unified HMI-related objects from a declarative C# project definition.

The active generator script is:

```text
etu-process-generator-TiAv20\Project.cs
```

`Project.cs` is best understood as a domain-specific plant description file, not ordinary business logic. Calls such as `AddDigital(...)`, `AddAnalog(...)`, `AddValve(...)`, `AddTp(...)`, and `AddPH(...)` build an internal XML-backed plant model. Later lifecycle calls create PLC tags, upload XML into TIA Portal, run SiVArc, and adjust Unified HMI objects.

Most real behavior is in repo-local `TIA_LIB`, especially:

- `TIAOpenness\TIA_Lib\TIA_LIB\PlcProject.cs`
- `TIAOpenness\TIA_Lib\TIA_LIB\SiemensPortal.cs`
- `TIAOpenness\TIA_Lib\TIA_LIB\Devices\*`
- `TIAOpenness\TIA_Lib\TIA_LIB\Xml\*`
- `TIAOpenness\TIA_Lib\TIA_LIB\Xml\Network\*`

When something fails in `Project.cs`, the root cause is often in `TIA_LIB`, generated XML, project references, template copy settings, or TIA Portal runtime state.

## Repository And Runtime Architecture

Current application name:

```text
etu-process-generator-TiAv20
```

Current solution and app project:

```text
etu-process-generator-TiAv20.sln
etu-process-generator-TiAv20\etu-process-generator-TiAv20.csproj
```

Repo-local `TIA_LIB` source of truth:

```text
TIAOpenness\TIA_Lib\TIA_LIB
```

An older machine-level copy may exist at:

```text
C:\TIAOpenness\TIA_Lib\TIA_LIB
```

Use the repo-local project for normal work. Historical notes, stack traces, or old paths may still mention `UnifiedSprechstunde15`, `C:\TIAOpenness\...`, or old desktop project folders. Treat those as historical context unless the user explicitly asks to compare or sync external files.

If Visual Studio reports missing names such as `TIA_LIB`, `PlcProject`, `AddDigital`, `AddAnalog`, `Portal`, `Upload`, or `GenerateSiVarc`, the likely cause is that `TIA_LIB` is not referenced, loaded, or built correctly. Fix the first real `TIA_LIB` or project-reference error before chasing cascade errors.

Recommended reference check:

1. Open the exact solution being run.
2. Confirm the app references repo-local `TIA_LIB.csproj`, not an old DLL or external folder.
3. Clean `bin` and `obj` when stale design-time errors persist.
4. Build `TIA_LIB`.
5. Build the app project in `Debug|x64`.

## Execution Lifecycle And Side Effects

`PlcProject()` is side-effecting:

- creates `Portal = new SiemensPortal();`
- immediately calls `Portal.CompilePlc();`
- creates `Plant = new XmlPlant();`

That means `new Project()` connects to or uses TIA Portal and starts generation-related work.

The current generation flow in `Project.cs` is:

```csharp
CreateTags();

Upload();

Portal.CheckTags();
GenerateSiVarc();

Portal.DeleteRenamedTags();

if (Portal.CheckTags())
{
    GenerateSiVarc();
}

UserInterface();

Portal.Portal.Dispose();
```

Interpretation:

1. Build PLC tag definitions from generated devices.
2. Upload generated XML-backed PLC objects.
3. Check HMI/PLC tag mismatches.
4. Run SiVArc.
5. Delete HMI tags ending `_Renamed`.
6. Re-check and optionally rerun SiVArc.
7. Apply final Unified HMI property adjustments.
8. Dispose the TIA Portal connection.

Running the app is not a harmless smoke test. It connects to TIA Portal and can mutate the open TIA project.

## Project DSL Reference

### Structural Methods

`AddUnit`

```csharp
public XmlUnit AddUnit(string unit_name)
```

Ensures a unit exists in the internal `XmlPlant` and returns it. Device methods normally create units implicitly through `Plant.GetUnit(unit_name)`.

`AddOP`

```csharp
public XmlOperation AddOP(string unit_name, string op_name)
```

Ensures a unit and operation exist, then returns the operation. `AddPH(...)` normally uses this path indirectly.

`AddPH`

```csharp
public XmlPhase AddPH(
    string unit_name,
    string op_name,
    string ph_name,
    int countP = 0,
    bool p01_type = false, double p01_sp_min = 0, double p01_sp_max = 0, int p01_numb_points = 2, string p01_unit = "", string p01_txt_list = "",
    bool p02_type = false, double p02_sp_min = 0, double p02_sp_max = 0, int p02_numb_points = 2, string p02_unit = "", string p02_txt_list = "",
    bool p03_type = false, double p03_sp_min = 0, double p03_sp_max = 0, int p03_numb_points = 2, string p03_unit = "", string p03_txt_list = "",
    bool p04_type = false, double p04_sp_min = 0, double p04_sp_max = 0, int p04_numb_points = 2, string p04_unit = "", string p04_txt_list = "",
    bool p05_type = false, double p05_sp_min = 0, double p05_sp_max = 0, int p05_numb_points = 2, string p05_unit = "", string p05_txt_list = "")
```

Creates or gets a phase under an operation under a unit. `countP` controls how many `P01...P05` parameter groups are active and wired as `SP01...SP05`.

Each parameter group in the public call is:

```text
type, min, max, decimals, unit, textList
```

`type = false` means numeric/real setpoint behavior. `type = true` means integer/text-list style behavior and should be paired with a text-list name.

Examples:

```csharp
AddPH("B110B", "OP1_Test", "PH_Test", 1, false, 0, 100, 1, "%");
AddPH("450", "OP5_Charge", "PH_Drying", 2, false, 0, 600, 0, "min", "", true, 0, 5, 0, "", "txtList_OP5_Charge_PH_Drying");
```

### Device Methods

`AddDigital`

```csharp
public Digital AddDigital(
    string unit_name,
    string name,
    int iconType = 0,
    int colorType = 0,
    int instanceCount = 0,
    bool qualityBit = false,
    bool neg = false)
```

Adds a digital input monitor and registers PLC tag `IN_<name>`.

- `unit_name`: logical unit.
- `name`: instrument/tag name.
- `iconType`: written to generated FB input `TYP_ICON`.
- `colorType`: public parameter, but currently not used by the `Digital` implementation; keep `0`.
- `instanceCount`: creates additional monitor instances and aggregates warning/alarm outputs; keep `0` for ordinary one-signal sensors.
- `qualityBit`: wires monitor external error to `IN_<name>_QB` when enabled.
- `neg`: inversion flag for `IN_<name>`.

Typical snippet:

```csharp
AddDigital("UNIT", "TAG", iconType, 0, 0, true, neg);
```

`AddAnalog`

```csharp
public Analog AddAnalog(
    string unit_name,
    string name,
    int iconType = 0,
    int instanceCount = 0,
    string unity = "",
    int numbDecPoints = 1,
    float limMin = 0.0f,
    float limMax = 500.0f)
```

Adds an analog input monitor and registers PLC tag `IN_<name>`.

- `iconType`: written to `TYP_ICON`.
- `instanceCount`: creates additional monitor instances; keep `0` unless multiple real monitor instances are required.
- `unity`: written to `UNIT`.
- `numbDecPoints`: written to `NUM_POINTS`.
- `limMin`: written to `LO_LIM`.
- `limMax`: written to `HI_LIM`.

Typical snippet:

```csharp
AddAnalog("UNIT", "TAG", 2, 0, "unit", decimals, min, max);
```

`AddValve`

```csharp
public Valve AddValve(
    string unit_name,
    string name,
    int iconType = 0,
    int interlockCount = 0,
    int interlockSafeCount = 0,
    bool mon_opn = false,
    bool mon_cls = false,
    bool mon_const = false,
    bool qualityBit = true,
    bool neg = true,
    int tp_number = -1,
    int mon_t = -1)
```

Adds a discrete valve and registers `FB_OPN_<name>`, `FB_CLS_<name>`, and `CTRL_<name>`.

- `interlockCount`: normal interlocks wired to `LOCK`.
- `interlockSafeCount`: safety interlocks wired to `S_LOCK`.
- `mon_opn`: enables/wires `FB_OPN_<name>`.
- `mon_cls`: enables/wires `FB_CLS_<name>`.
- `mon_const = false` with `tp_number != -1`: links `MON_T_ST` and `MON_T_DN` to `TP<number>|OUT`.
- `mon_const = true` with `mon_t != -1`: writes literal monitor times.
- `qualityBit`: combines feedback and quality bits before monitor wiring when feedback is monitored.
- `neg`: inversion flag for feedback inputs, not the command output.

Typical snippet:

```csharp
AddValve("UNIT", "TAG", 2, 0, 0, mon_opn, mon_cls, false, true, true, 1);
```

`AddValveControl`

```csharp
public ValveControl AddValveControl(
    string unit_name,
    string name,
    int iconType = 0,
    int interlockCount = 0,
    int interlockSafeCount = 0,
    string unity = "%",
    int numbDecPoints = 1)
```

Adds an analog/proportional valve and registers control tag `CTRL_<name>`.

- `interlockCount` and `interlockSafeCount` behave like valve interlocks.
- `unity`: written to `UNIT`, usually `%`.
- `numbDecPoints`: written to `NUM_POINTS`.
- generated output `QSETPOINT_INT` is wired to `CTRL_<name>`.

Typical snippet:

```csharp
AddValveControl("UNIT", "TAG", 2, 0, 0, "%", 1);
```

`AddMotor`

```csharp
public Motor AddMotor(
    string unit_name,
    string name,
    int iconType = 0,
    int interlockCount = 0,
    int interlockSafeCount = 0,
    bool mon_on = false,
    bool mon_const = false,
    int tp_number = -1,
    int mon_t = -1)
```

Adds a discrete motor and registers `FB_ON_<name>` and `CTRL_<name>`.

- `interlockCount`: normal interlocks wired to `LOCK`.
- `interlockSafeCount`: safety interlocks wired to `S_LOCK`.
- `mon_on`: enables running/on feedback monitoring.
- `mon_const`, `tp_number`, and `mon_t`: same monitor-time rule as valves.

Typical snippet:

```csharp
AddMotor("UNIT", "TAG", 5, 0, 0, mon_on, false, -1, 10);
```

`AddMotorControl`

```csharp
public MotorControl AddMotorControl(
    string unit_name,
    string name,
    int iconType = 0,
    int interlockCount = 0,
    int interlockSafeCount = 0,
    string unity = "%",
    int numbDecPoints = 1,
    bool mon_on = false,
    bool mon_const = false,
    int tp_number = -1,
    int mon_t = -1)
```

Adds a controlled motor, for example a speed-controlled agitator.

- `unity`: written to `UNIT`, often `rpm` or `%`.
- `numbDecPoints`: written to `NUM_POINTS`.
- `mon_on`, `mon_const`, `tp_number`, and `mon_t`: same monitoring model as `AddMotor`.
- current tag registration is not as explicit as `AddMotor(...)`; keep speed/control source evidence visible in comments.

Typical snippet:

```csharp
AddMotorControl("UNIT", "TAG", 6, 0, 0, "rpm", 1, mon_on, false, 2);
```

### Parameter Methods

`AddTp`

```csharp
public Tp AddTp(
    int number,
    double def_value,
    double sp_min,
    double sp_max,
    string unity = "",
    bool isSafety = false,
    int numbDecPoints = 1,
    bool type = false,
    string listname = "")
```

Creates technical parameter `TP<number>` in shared block `fbTPs`.

- `number`: numeric part only; omit the `TP` prefix.
- `def_value`: written to `SP_DEF`.
- `sp_min`: written to `SP_MIN`.
- `sp_max`: written to `SP_MAX`.
- `unity`: written to `UNIT`.
- `isSafety`: written to `IsSafety`; use true only with source evidence.
- `numbDecPoints`: written to `NUM_POINTS`.
- `type = true`: text-list/int-style mode.
- `listname`: written to `TEXTLIST_NAME`.

Examples:

```csharp
AddTp(900, 10, 0, 60, "s", true, 0);
AddTp(204, 0, 0, 1, "", false, 0, true, "txtList_internal_jacket");
```

`AddRp`

```csharp
public Rp AddRp(
    int number,
    double sp_min,
    double sp_max,
    string unity = "",
    int numbDecPoints = 1,
    bool type = false,
    string listname = "")
```

Creates recipe parameter `RP<number>` in shared block `fbRPs`.

- `number`: numeric part only; omit the `RP` prefix.
- `sp_min`: written to `MTR_MIN`.
- `sp_max`: written to `MTR_MAX`.
- `unity`: written to `UNIT`.
- `numbDecPoints`: written to `NUM_POINTS`.
- `type = false`: numeric RP with HMI range bindings.
- `type = true`: text-list/int-style UI path.
- `listname`: written to `TEXTLIST_NAME`.

Example:

```csharp
AddRp(901, 0, 1, "", 0, true, "txtList_yes_no");
```

## Generated Tags And Naming

`unit_name` selects or creates the unit. `name` is the device/instrument tag inside that unit.

Generated object names usually become:

```text
UnitName_TagName
```

PLC tags commonly use:

- `IN_<tag>` for analog and digital inputs
- `CTRL_<tag>` for valve and motor commands/setpoints
- `FB_OPN_<tag>` and `FB_CLS_<tag>` for valve feedback
- `FB_ON_<tag>` for motor running feedback
- `_QB` suffixes for quality bits

`CreateTags()` creates tags in PLC tag table `TEMP`.

Current marker allocation:

- valves: `Bool` tags starting at `M500.0`
- digitals: `Bool` tags starting at `M1000.0`
- control valves: `Int` tags starting at `MW1500`
- analogs: `Int` tags starting at `MW2000`
- motors: `Bool` tags starting at `M2500.0`

Known quirks:

- `qualityBit` affects generated monitor wiring for digitals and valves, but helper tag records have their own defaults, so `_QB` tags may still be created by default.
- `colorType` is present on `AddDigital(...)` but is not currently used by the `Digital` implementation.
- `AddMotorControl(...)` affects controlled-motor FB generation and UI properties, but its PLC tag registration is less explicit than `AddMotor(...)`.

## Snippet Preparation Workflow

### Source Priority

Use the source document that owns the data type:

- sensors: `30073487_List of Sensors Working List_01.xlsx`
- valves and motors: `30073487_List of Actuators Working List_01.xlsx`
- physical measuring ranges: `Instrument_List_Detail_Design_2026_03_24.pdf`
- electrical switch/NAMUR limits: `Special_Electrical_Items_2026_03_24.pdf`
- phases, operations, TP, and RP: functional description PDFs
- interlock and alarm text support: Cause and Effect Matrix plus text-list workbooks

When sources disagree, keep the source row or section visible in a comment and add a `Review:` comment instead of silently choosing a value.

### General Snippet Layout

Each snippet should include source document names, relevant `Add...` signatures when useful, assumptions, calls grouped by normalized unit, and source row/section comments directly above generated calls.

Example:

```csharp
// Row 24: PIT0060; Pressure; signal SL; assignment HYD-008-Supply
AddAnalog("GeneralSignals", "PIT0060", 2, 0, "bar(g)", 1, -1, 10);
```

### Naming Rules

- Remove dots from device names before writing C# calls or comments.
- Use the normalized project tag exactly.
- Keep unit names ASCII-only.
- Assign `HYD-008-Supply` workbook items to `GeneralSignals` in current project snippets.
- Do not write old-to-new conversion comments such as `old.tag -> newtag`; write only the normalized project tag.

### Sensor Snippets

- Start from the sensor working list as row authority.
- Classify analog signals from evidence such as `4...20mA`, `4-20mA`, HART, valve positioners, and analog process measurements.
- Classify digital signals from evidence such as NAMUR, `ZSC`, `ZSO`, `SOH`, `LS`, `FISL`, `GS`, `XV`, and `NC`.
- Exclude valve, control, and feedback rows when they belong to valve functions in TIA Portal.
- Keep duplicate source rows only when they represent separate real signals.
- Normalize units to ASCII, for example `deg C`, `m3/h`, `Nm3/h`, and `uS/cm`.
- Add `Review:` comments for missing ranges, undefined signal types, or inferred placeholder limits.

### Valve Snippets

- Start from the actuator working list as valve authority.
- Skip non-valve actuators.
- Collapse duplicate valve tags into one call and list all source rows in the comment.
- Infer `mon_opn` and `mon_cls` from matching `ZSO` and `ZSC` feedback rows.
- Use `AddValveControl(...)` only for analog/proportional setpoints.
- Keep HSP/SSP state in comments.
- Add `Review:` when no matching position feedback row is found.

### Motor Snippets

- Start from actuator rows whose source `Type` is motor-like.
- Do not promote valve descriptions that merely mention agitator, pump, or air supply.
- Use `AddMotorControl(...)` when the functional description controls a speed or setpoint.
- Keep source-name mismatches and related speed sensors in concise `Review:` comments.

### Phase Snippets

- Use functional description body sections, not only the table of contents.
- Group by unit, then operation, then phase.
- Normalize operation spacing, for example `OP1_ Pressure` becomes `OP1_Pressure`.
- Preserve source phase spelling when plausibly intentional.
- Put Non-Technical parameters in comments directly above the call.
- Map phase parameters to recipe parameter ranges where possible.
- Leave a comment when the phase text says `None`, a section is malformed, or a setpoint has `TBD` limits.

### TP And RP Snippets

- Omit the `TP` or `RP` prefix from the numeric argument.
- Normalize decimal commas to C# decimal points.
- Preserve source names as comments above each call.
- For list-style choices, set `type = true` and provide an explicit `txtList_...` name.
- Keep manual overrides visible in the snippet header.

### Assembly Into Project.cs

- Create or update a reviewable copy under `Codex Work`; do not overwrite reference files.
- Assemble only active project units.
- Use section order: sensors, valves, motors, phases.
- Append shared `AddTp(...)` and `AddRp(...)` sections after unit device/phase definitions.
- Keep useful `Review:` comments for trust-affecting assumptions.
- Avoid carrying old commented template blocks into the clean active project file.
- Count active `Add...` calls and compare with source snippets.

### Simulator Device UI Template

When adding or regenerating a unit, also create or update a unit-specific simulator UI file in:

```text
SimulationFiles
```

Use `SimulationFiles\DeviceUiTemplate.json` as the shape reference. The unit copy should include only the devices for that generated unit and keep PLC tag spelling exactly as exported from TIA Portal. `uiId` and UI control names must be WPF-safe: use letters, digits, and underscores.

Current expected content:

- `analogInputs` for `AddAnalog(...)` devices
- `digitalInputs` for `AddDigital(...)` devices, including `_QB` tags when quality bits are generated
- `valves` for `AddValve(...)` devices, including control, feedback, and feedback quality-bit tags when present
- `actuators` for motor-style devices when applicable
- `markerFallbacks` only when marker addresses are known from TIA tag exports or screenshots

These files are used by the simulator and are currently maintained manually. After the G-008 generated DB staging and G-009 external project-definition workbook upgrades, this template should be modified and integrated into the automatic generation workflow.

### Review Checklist

- all device names are dotless
- units are in scope
- `HYD-008-Supply` items are assigned to `GeneralSignals` when applicable
- valve feedback rows are not duplicated as sensors
- duplicate actuator tags are collapsed
- missing source evidence is marked with `Review:`
- decimal commas were converted to decimal points
- units are ASCII-safe
- the snippet header explains assumptions and omissions
- active `Add...` call count matches expectations

## Troubleshooting And Known Failure Modes

### HMI / PLC Not Found

Typical symptom:

```text
System.NullReferenceException
bei TIA_LIB.SiemensPortal...
```

Likely cause:

- `HmiSoftware` stayed null
- `PlcSoftware` stayed null
- no HMI connection was found
- code later accessed `.Name` or `.Connections`

Current desired behavior:

- identify HMI and PLC software by recursively scanning `SoftwareContainer` on device items
- do not rely only on `System:Device.PC` or `IPC_`
- throw explicit errors when HMI software, PLC software, or HMI connection is missing

### AddDigital / AddAnalog Methods Not Found

Typical errors:

```text
Der Typ- oder Namespacename "PlcProject" wurde nicht gefunden
Der Typ- oder Namespacename "TIA_LIB" wurde nicht gefunden
Der Name "AddDigital" ist im aktuellen Kontext nicht vorhanden
```

Likely cause:

- `TIA_LIB` failed to compile
- app project cannot resolve the `TIA_LIB` reference
- Visual Studio IntelliSense is showing cascade errors

Fix the first `TIA_LIB` compile error, check project references, clean `bin`/`obj`, and reload Visual Studio if design-time errors stay stale.

### NullReferenceException In XmlCall / XmlNetwork

Likely cause:

- empty network template lacks required containers
- `Network.Parts` or `Network.Wires` is null
- runtime output folder contains an old or incomplete template

Important XML areas:

- `NetworkSource`
- `FlgNet`
- `Parts`
- `Wires`
- `Call`
- `Wire`
- `NameCon`
- `IdentCon`
- `Access`
- `Component`

FlgNet namespace:

```text
http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4
```

### TIA Import XML Invalid

Observed exception:

```text
Siemens.Engineering.EngineeringTargetInvocationException
Cannot create the 'SW.Blocks.CompileUnit' object with Simatic ML ID '8'
The XML file is invalid.
```

Recommended response:

- preserve failed XML, for example as `*_IMPORT_FAILED.xml`
- inspect the line reported by TIA
- verify XML around the failing `CompileUnit`
- confirm the patched repo-local `TIA_LIB` output is actually loaded

### NullReferenceException In Analog.cs

Important detail:

- the fourth public `AddAnalog(...)` argument is `instanceCount`
- if `instanceCount > 0`, `Analog.cs` enters an instance-handling branch that may edit existing XML wires

Use `instanceCount = 0` only as a diagnostic workaround, because it changes generated behavior.

### Missing EmptyUnit.xml

Observed exception:

```text
System.IO.FileNotFoundException
Die Datei "...bin\x64\Debug\Xml\EmptyUnit.xml" konnte nicht gefunden werden.
```

Likely cause:

- `XmlBlock.cs` loads templates by relative path
- the `Xml` folder was not copied to the runtime output folder
- template XML files are not included as project content

Important templates:

```text
Xml\EmptyUnit.xml
Xml\EmptyOperation.xml
Xml\EmptyPhase.xml
Xml\EmptyBlock.xml
Xml\EmptyNetwork.xml
Xml\EmptyPlant.xml
Xml\EmptyDatablock.xml
Xml\EmptyGlobal.xml
Xml\EmptyConstant.xml
```

Long-term fix:

- centralize template path resolution
- load templates relative to a deterministic base path
- log the resolved template path before loading
- fail with a clear missing-template error

## Safe Workflows And Future Improvements

Preferred validation is a targeted compile check, not running the generator app.

Build commands:

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

Use this debugging order:

1. Read the first real exception, not only the last cascade error.
2. Check whether the stack trace points to the intended source path.
3. Confirm the repo-local `TIA_LIB` project is referenced.
4. Clean `bin` and `obj` if stale outputs are suspected.
5. Rebuild `TIA_LIB`.
6. Rebuild the app.
7. If XML import fails, preserve and inspect generated XML.
8. If a template is missing, fix copy-to-output settings.
9. If a null reference occurs inside `Devices\*`, inspect generated `XmlNetwork` and expected calls/wires.

High-value future improvements:

- keep device detection capability-based instead of type-string-based
- remove hard-coded path assumptions
- copy all XML templates reliably
- add startup logging showing the loaded `TIA_LIB` assembly path
- preserve failed XML automatically on import errors
- add null-safe XML query helpers for `NetworkSource`, `FlgNet`, `Parts`, and `Wires`
- reduce constructor side effects in `PlcProject` and `Project`
- split plant definition from execution steps

## Final Takeaway

The visible `Add...` call in `Project.cs` is usually only the trigger. The behavior lives deeper in repo-local `TIA_LIB`, generated XML, TIA Portal state, and Unified HMI/SiVArc side effects.

When in doubt, verify the loaded library path first, then inspect the generated XML or missing template path that the stack trace points to.
