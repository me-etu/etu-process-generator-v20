# Project.cs Method Reference

This file documents the project-facing DSL used by [Project.cs](D:\Software\UnifiedSprechstunde15\UnifiedSprechstunde15\Project.cs) so another Codex project can understand and extend it without reverse-engineering the whole solution.

## Purpose

`Project.cs` is not ordinary business logic. It is a declarative build script for a TIA Portal automation project.

- The class `Project : PlcProject` inherits a set of helper methods from [PlcProject.cs](D:\Software\UnifiedSprechstunde15\TIAOpenness\TIA_Lib\TIA_LIB\PlcProject.cs).
- Those helpers create XML-backed PLC/HMI objects, upload them to TIA Portal, generate HMI tags, run SiVArc, and then adjust Unified faceplate/UI properties.
- Most of the work in `Project.cs` consists of calling methods like `AddDigital(...)`, `AddAnalog(...)`, `AddValve(...)`, `AddPH(...)`, `AddTp(...)`, and `AddRp(...)`.

## Mental Model

Another Codex should treat `Project.cs` as a domain-specific language with this rough hierarchy:

1. Units
2. Devices inside units
3. Operations inside units
4. Phases inside operations
5. Technical parameters (`TP`)
6. Recipe parameters (`RP`)
7. Final generation flow: tags, upload, SiVArc, UI adjustments

## Base Class And Runtime

The inherited base class is [PlcProject.cs](D:\Software\UnifiedSprechstunde15\TIAOpenness\TIA_Lib\TIA_LIB\PlcProject.cs).

Important constructor behavior:

- `PlcProject()` creates `Portal = new SiemensPortal();`
- it immediately calls `Portal.CompilePlc();`
- it creates `Plant = new XmlPlant();`

That means a `new Project()` call is not just object construction. It connects to TIA Portal and starts generation-related work.

## Core Project DSL Methods

These are the main methods another Codex should know and reuse.

### Structural Methods

`AddUnit`

Signature:

```csharp
public XmlUnit AddUnit(string unit_name)
```

Purpose:

- Ensures a unit exists in the internal `XmlPlant`
- Returns the `XmlUnit`

Typical use:

- Usually not called directly in `Project.cs`
- Most device methods auto-create the unit implicitly via `Plant.GetUnit(unit_name)`

`AddOP`

Signature:

```csharp
public XmlOperation AddOP(string unit_name, string op_name)
```

Purpose:

- Ensures a unit exists
- Ensures an operation exists inside that unit
- Returns the `XmlOperation`

Typical use:

- Usually indirect
- `AddPH(...)` effectively builds units and operations as needed

`AddPH`

Signature:

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

Purpose:

- Creates or gets a phase under a specific operation under a specific unit
- Can define up to 5 configurable phase parameters

Interpretation of parameter groups:

- `countP`: how many phase parameters are active
- `p0X_type`: likely switches between numeric and text-list mode
- `p0X_sp_min`, `p0X_sp_max`: allowed range
- `p0X_numb_points`: decimal digits / resolution
- `p0X_unit`: engineering unit
- `p0X_txt_list`: text-list name when using enumerated/list style parameters

Examples from [Project.cs](D:\Software\UnifiedSprechstunde15\UnifiedSprechstunde15\Project.cs):

```csharp
//AddPH("BV160", "OP1_Agitate", "PH_Agitate");
//AddPH("A2", "OP2_Pressure", "PH_Reaction", 2, false, 0.5, 10, 1, "bar(g)", "", false, 5, 1000, 0, "min");
//AddPH("450", "OP5_Charge", "PH_Drying", 2, false, 0, 600, 0, "min", "", true, 0, 5, 0, "", "txtList_OP5_Charge_PH_Drying");
```

### Device Methods

`AddDigital`

Signature:

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

Purpose:

- Adds a digital device to a unit
- Also registers a PLC tag named `IN_<name>` for later tag creation

Common meaning:

- `unit_name`: logical unit, for example `B110B`
- `name`: instrument/tag name, for example `LSA-4-1-8-3`
- `iconType`: visual or object type selection
- `colorType`: visual/status variant
- `instanceCount`: instance-related variation used by the underlying device generator
- `qualityBit`: also create quality bit behavior
- `neg`: signal inversion / negative logic

Active examples:

```csharp
AddDigital("B110B", "LSA-4-1-8-3", 2, 0, 0, true, true);
AddDigital("B110B", "FS-8-2-35-2", 4, 0, 0, true, false);
```

`AddAnalog`

Signature:

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

Purpose:

- Adds an analog device to a unit
- Also registers a PLC tag named `IN_<name>`

Common meaning:

- `unity`: engineering unit
- `numbDecPoints`: number of decimal places
- `limMin`, `limMax`: display or operating range

Active examples:

```csharp
AddAnalog("B110B", "LC-4-20-8-4", 2, 0, "%", 1, 0, 100);
AddAnalog("B110B", "QC-9-1-8-4", 2, 0, "pH", 2, 0, 14);
AddAnalog("B110B", "TC-5-3-31-1", 2, 0, "°C", 1, -50, 200);
```

`AddValve`

Signature:

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

Purpose:

- Adds a digital valve object
- Registers tag triplets:
  - `FB_OPN_<name>`
  - `FB_CLS_<name>`
  - `CTRL_<name>`

Important meaning:

- `interlockCount`: number of normal interlocks
- `interlockSafeCount`: number of safety interlocks
- `mon_opn`, `mon_cls`: enable open/close monitoring
- `mon_const`: constant monitoring mode
- `qualityBit`: create quality-related tags
- `neg`: inverted logic handling
- `tp_number`: optional technical parameter linkage
- `mon_t`: monitoring time / timeout

Active example:

```csharp
AddValve("B110B", "Q21", 2, 0, 0, false, false, false, true, true, 1);
```

`AddValveControl`

Signature:

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

Purpose:

- Adds an analog/control valve object
- Registers control tag `CTRL_<name>`

Common meaning:

- `unity` is typically `%`
- `numbDecPoints` controls display precision

Example from comments:

```csharp
//AddValveControl("A2", "VV1000", 2, 2, 1);
```

`AddMotor`

Signature:

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

Purpose:

- Adds a motor
- Registers tags:
  - `FB_ON_<name>`
  - `CTRL_<name>`

Common meaning:

- `mon_on`: enable running/on monitoring
- `mon_const`: constant monitoring mode
- `tp_number`: optional technical parameter reference
- `mon_t`: monitoring time

Active examples:

```csharp
AddMotor("B110B", "G21", 5, 0, 0, mon_on, false, -1, 10);
AddMotor("B110B", "E21", 6, 0, 0, false, false, -1, -1);
```

`AddMotorControl`

Signature:

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

Purpose:

- Adds a controllable motor, for example with speed setpoint

Common meaning:

- `unity` is often `rpm` or `%`
- `numbDecPoints` controls numeric precision
- monitor/time parameters behave similarly to `AddMotor`

Active examples:

```csharp
AddMotorControl("B110B", "G31", 6, 0, 0, "rpm", 1, mon_on, false);
AddMotorControl("B110B", "G41", 7, 0, 0, "rpm", 1, mon_on, false);
```

`AddPidControl`

Signature:

```csharp
public PidControl AddPidControl(
    string unit_name,
    string name,
    int iconType = 0,
    string unity = "",
    int numbDecPoints = 1,
    string unityOut = "%",
    int numbDecPointsOut = 2)
```

Purpose:

- Adds a PID controller object
- Used for pressure, temperature, or flow control loops

Examples from comments:

```csharp
//AddPidControl("A2", "TIC-1174", 1, "°C", 1);
//AddPidControl("410", "PIC-410-610-A", 1, "mbar", 0);
```

### Parameter Methods

`AddTp`

Signature:

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

Purpose:

- Creates a technical parameter `TP<number>`
- Backed by the shared `fbTPs` XML block

Common meaning:

- `number`: TP number, becomes `TP<number>`
- `def_value`: default value
- `sp_min`, `sp_max`: allowed range
- `unity`: engineering unit
- `isSafety`: marks safety-relevant parameters
- `numbDecPoints`: numeric precision
- `type`: when `true`, appears to indicate text-list / enumerated mode rather than free numeric entry
- `listname`: associated text list name

Examples from comments:

```csharp
//AddTp(1, 15, 1, 60, "s", true, 0);
//AddTp(204, 0, 0, 1, "", false, 0, true, "txtList_internal_jacket");
```

`AddRp`

Signature:

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

Purpose:

- Creates a recipe parameter `RP<number>`
- Backed by the shared `fbRPs` XML block

Common meaning:

- similar to `AddTp`, but no default value parameter
- used for batch/recipe-facing operator setpoints

Examples from comments:

```csharp
//AddRp(101, 0, 300, "L", 1);
//AddRp(104, 0, 1, "", 0, true, "txtList_heating_mode");
```

## Lifecycle / Generation Methods

These methods are project-relevant even if they are not pure DSL declarations.

`CreateTags`

Defined in [PlcProject.cs](D:\Software\UnifiedSprechstunde15\TIAOpenness\TIA_Lib\TIA_LIB\PlcProject.cs).

Purpose:

- Creates PLC tags in the `TEMP` PLC tag table
- Uses the internal lists collected during `AddValve`, `AddAnalog`, `AddDigital`, `AddMotor`, and `AddValveControl`
- Allocates marker addresses beginning at byte `M500.0`

Effects:

- creates tags like `FB_OPN_*`, `FB_CLS_*`, `CTRL_*`, `IN_*`
- may also create `_QB` quality-bit tags

`Upload`

Signature:

```csharp
public static void Upload()
```

Purpose:

- Uploads generated XML blocks into TIA Portal
- uploads plant blocks, datablocks, TP/RP blocks, phases, operations, interlocks, and units
- recompiles the PLC afterward

Important note:

- This is a major side-effecting step and depends on a live TIA Portal project

`GenerateSiVarc`

Signature:

```csharp
public void GenerateSiVarc()
```

Purpose:

- Calls `Portal.SiVarcGenerate(...)`
- regenerates HMI objects/tags using SiVArc
- recompiles PLC afterward

`UserInterface`

Signature:

```csharp
public void UserInterface()
```

Purpose:

- walks generated units, operations, phases, TPs, and RPs
- sets Unified faceplate/UI properties
- performs post-generation HMI adjustments

Direct portal-side helper calls used by the project flow:

- `Portal.CheckTags()`
- `Portal.DeleteRenamedTags()`
- `Portal.Portal.Dispose()`

From [SiemensPortal.cs](D:\Software\UnifiedSprechstunde15\TIAOpenness\TIA_Lib\TIA_LIB\SiemensPortal.cs):

- `CheckTags()` removes mismatched HMI tags and returns whether any were found
- `DeleteRenamedTags()` removes HMI tags whose names end with `_Renamed`
- `CompilePlc()` recompiles PLC software
- `SiVarcGenerate(...)` runs SiVArc generation against HMI and PLC names

## Actual Execution Flow In Project.cs

The current [Project.cs](D:\Software\UnifiedSprechstunde15\UnifiedSprechstunde15\Project.cs) ends with this flow:

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

1. Build tag definitions for the generated devices
2. Upload generated PLC/XML objects
3. Clean mismatched HMI tags
4. Run SiVArc once
5. Remove renamed tags
6. Re-check tags and optionally run SiVArc again
7. Apply final Unified UI adjustments
8. Disconnect from TIA Portal

## Methods Actively Used In The Current Project

These are the only non-commented DSL calls currently active near the top of [Project.cs](D:\Software\UnifiedSprechstunde15\UnifiedSprechstunde15\Project.cs):

- `AddDigital(...)`
- `AddAnalog(...)`
- `AddMotor(...)`
- `AddMotorControl(...)`
- `AddValve(...)`
- `CreateTags()`
- `Upload()`
- `Portal.CheckTags()`
- `GenerateSiVarc()`
- `Portal.DeleteRenamedTags()`
- `UserInterface()`

Large commented sections show that the project also expects support for:

- `AddPH(...)`
- `AddTp(...)`
- `AddRp(...)`
- `AddValveControl(...)`
- `AddPidControl(...)`

## Project Conventions Another Codex Should Follow

### 1. Unit names are the primary grouping key

Examples:

- `B110B`
- `A2`
- `410`
- `450`

Every device, phase, and operation is anchored to a `unit_name`.

### 2. Instrument names should be passed exactly as plant naming expects

Examples:

- `LSA-4-1-8-3`
- `TI-410-135`
- `XV-430-701`

These names feed PLC tag names, HMI tag names, block names, and faceplate generation.

### 3. Monitoring flags are usually predeclared once

At the top of [Project.cs](D:\Software\UnifiedSprechstunde15\UnifiedSprechstunde15\Project.cs), the project defines:

```csharp
bool mon_opn = true;
bool mon_cls = true;
bool mon_on = true;
```

These are reused in valve and motor calls to make the project definition more compact.

### 4. `iconType` is a project-specific visual/device variant selector

The exact numeric meaning is defined by the device classes and faceplate logic, not by `Project.cs` itself. Another Codex should preserve existing values unless there is a known project convention to change them.

### 5. Interlock counts matter

For valves, motors, and controlled devices, the `interlockCount` and `interlockSafeCount` values are part of the generated object model and should not be guessed casually.

### 6. TP and RP text-list mode exists

When `type = true`, the parameter often uses a text list:

```csharp
AddTp(204, 0, 0, 1, "", false, 0, true, "txtList_internal_jacket");
AddRp(104, 0, 1, "", 0, true, "txtList_heating_mode");
```

A new Codex should preserve both the boolean mode flag and the list name together.

### 7. The file is part template, part active configuration

Much of `Project.cs` is commented-out historical or reusable configuration. Another Codex should not assume commented examples are dead code; many are valid patterns for future projects.

## Safe Usage Guidance For Another Codex

When extending `Project.cs`, a Codex should usually:

1. Reuse the existing call style and ordering
2. Keep new devices grouped by unit and by type
3. Preserve naming exactly
4. Prefer copying an existing nearby call and changing only the minimum fields
5. Treat `CreateTags`, `Upload`, `GenerateSiVarc`, and `UserInterface` as execution steps, not helper utilities
6. Be careful with TP/RP numbers because they are identifiers, not just list positions

## Minimal Examples

Add a new digital input:

```csharp
AddDigital("B110B", "NEW_SENSOR", 2, 0, 0, true, false);
```

Add a new analog signal with limits:

```csharp
AddAnalog("B110B", "PI-NEW-001", 2, 0, "bar", 2, 0, 10);
```

Add a new motor with monitoring:

```csharp
AddMotor("B110B", "P01", 5, 0, 0, mon_on, false, -1, 10);
```

Add a technical parameter:

```csharp
AddTp(900, 10, 0, 60, "s", true, 0);
```

Add a recipe parameter with a text list:

```csharp
AddRp(901, 0, 1, "", 0, true, "txtList_yes_no");
```

Add a phase with one numeric parameter:

```csharp
AddPH("B110B", "OP1_Test", "PH_Test", 1, false, 0, 100, 1, "%");
```

## Key Files

- [Project.cs](D:\Software\UnifiedSprechstunde15\UnifiedSprechstunde15\Project.cs)
- [PlcProject.cs](D:\Software\UnifiedSprechstunde15\TIAOpenness\TIA_Lib\TIA_LIB\PlcProject.cs)
- [SiemensPortal.cs](D:\Software\UnifiedSprechstunde15\TIAOpenness\TIA_Lib\TIA_LIB\SiemensPortal.cs)

## Short Summary

If another Codex only remembers one thing, it should remember this:

`Project.cs` is a declarative generator script built on `PlcProject`. The important project methods are `AddDigital`, `AddAnalog`, `AddValve`, `AddValveControl`, `AddMotor`, `AddMotorControl`, `AddPidControl`, `AddPH`, `AddTp`, `AddRp`, followed by `CreateTags`, `Upload`, `GenerateSiVarc`, and `UserInterface`.
