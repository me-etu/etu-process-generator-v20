# Generator Knowledge Base

Canonical knowledge base for the V20 and V21 ETU TIA Portal generators.

This file supersedes:

- `CHAT_KNOWLEDGE_TRANSFER.md`
- `GENERATOR_SNIPPETS_KNOWHOW.md`
- `PROJECT_CS_METHODS.md`

Use this file first when changing `Project.cs`, preparing generator snippets, or debugging generator/runtime behavior.

## Purpose And Mental Model

This project generates TIA Portal PLC function blocks, PLC tags, and Unified HMI-related objects from a declarative C# project definition.

The version-specific generator scripts are:

```text
etu-process-generator-TiAv20\Project.cs
etu-process-generator-TiAv21\Project.cs
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

Application names:

```text
etu-process-generator-TiAv20
etu-process-generator-TiAv21
```

Version-specific solutions and app projects:

```text
etu-process-generator-TiAv20.sln
etu-process-generator-TiAv20\etu-process-generator-TiAv20.csproj
etu-process-generator-TiAv21.sln
etu-process-generator-TiAv21\etu-process-generator-TiAv21.csproj
```

Repo-local `TIA_LIB` source of truth:

```text
TIAOpenness\TIA_Lib\TIA_LIB
```

V20 references `TIA_LIB.csproj`; V21 references the adjacent `TIA_LIB.V21.csproj`. These share implementation and existing assembly metadata, namespace, and assembly name (`TIA_LIB`). Keep their explicit source/template item lists aligned. V21 library outputs use `bin/V21/<Platform>/<Configuration>/` and intermediates use `obj/V21/<Platform>/<Configuration>/`, configured before `Microsoft.Common.props` so reference caches and generated framework attributes cannot overlap V20.

Both retain .NET Framework 4.8 and C# 7.3 for x64. Build the selected app project or solution with `/p:Configuration=Debug /p:Platform=x64`; see README for full commands. V21 accepts `TiaV21ApiDirectory`, defaulting to `$(ProgramFiles)/Siemens/Automation/Portal V21/PublicAPI/V21/net48`, and explicitly fails if a required API file is missing. It references Base, Step7, WinCCUnified, Sivarc, and (in the library) WinCC for the existing `Hmi.Communication` import, using V21 identities rather than V20/V18 reference identities. No shared-source API adaptation was necessary for compilation.

V21 local Siemens help (`TIAPortalOpennessenUS.zip`, topic `37231359755/37752246795.htm`) explicitly rejects Copy Local and supports `App.config` assembly bindings. V21 references use `Private=False`; before `PrepareForBuild`, the app copies its config to an intermediate file and fills `codeBase` paths from the selected API directory. The deployed executable config must stay beside the executable. This resolves paths before any Siemens type loads without a custom resolver or entry-point changes. Runtime dependency initialization still requires a correctly installed Portal V21 and remains untested.

The V21 library now defines `TIA_V21` in all configurations. `XmlUdt.BuildXml` uses it to emit `<Engineering version="V21" />` for generated staging UDTs, preserving `V17` for the V20 project. Installed V21 help topic `37231359755/59943517835.htm` lists V18–V21 as supported SimaticML import versions. A failed option-2 run left `hwIN_BV130_B10_20.xml` with a V17 header; that incompatible header is fixed, but the exact option-2 exception/runtime retry is pending. In-memory construction from the rebuilt V21 DLL verifies the V21 header without connecting to Portal. The user reports marker-memory generation now completes. Their active V21 scope was subsequently changed to `BV130_B10_20`; preserve the current source rather than restoring the initial migration baseline.

Use the selected app's output as runtime working directory, e.g. `etu-process-generator-TiAv21/bin/x64/Debug/`, because existing library exports/template reads use relative paths. Output isolation alone does not isolate a shared working directory. Project content copies now include all ten XML templates; this does not certify all project-specific runtime templates or device libraries are present.

The 2026-10-20 V21 app rebuild and solution build passed, with source parity and assembly/template/path checks. No Portal connection or XML import was exercised. V20 compilation is blocked by the missing standard V20 installation; its source/project/reference settings remain unchanged. Runtime validation needs explicit authorization and a disposable V21 TIA project copy.

An older machine-level copy may exist at:

```text
C:\TIAOpenness\TIA_Lib\TIA_LIB
```

Use the repo-local project for normal work. Historical notes, stack traces, or old paths may still mention `UnifiedSprechstunde15`, `C:\TIAOpenness\...`, or old desktop project folders. Treat those as historical context unless the user explicitly asks to compare or sync external files.

If Visual Studio reports missing names such as `TIA_LIB`, `PlcProject`, `AddDigital`, `AddAnalog`, `Portal`, `Upload`, or `GenerateSiVarc`, the likely cause is that `TIA_LIB` is not referenced, loaded, or built correctly. Fix the first real `TIA_LIB` or project-reference error before chasing cascade errors.

Recommended reference check:

1. Open the exact solution being run.
2. Confirm the app references repo-local `TIA_LIB.csproj` (V20) or `TIA_LIB.V21.csproj` (V21), not an old DLL or external folder.
3. Clean `bin` and `obj` when stale design-time errors persist.
4. Build `TIA_LIB`.
5. Build the app project in `Debug|x64`.

## Execution Lifecycle And Side Effects

`PlcProject()` is side-effecting:

- creates `Portal = new SiemensPortal();`
- immediately calls `Portal.CompilePlc();`
- creates `Plant = new XmlPlant();`

That means `new Project()` connects to or uses TIA Portal and starts generation-related work.

Both app definitions initially select `BV260-B210`, preserving other units inside `#if false`. Their current generation flow is:

```csharp
CreateTags();

Upload();

Portal.CheckTags();
// GenerateSiVarc();

Portal.DeleteRenamedTags();

// if (Portal.CheckTags())
// {
//     GenerateSiVarc();
// }

UserInterface();

Portal.Portal.Dispose();
```

Interpretation:

1. Build PLC tag definitions from generated devices.
2. Upload generated XML-backed PLC objects.
3. Check HMI/PLC tag mismatches.
4. SiVArc generation is currently disabled.
5. Delete HMI tags ending `_Renamed`.
6. The optional tag re-check/SiVArc rerun is currently disabled.
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

G-014 adds a run-wide `PlcProject.InterlockLanguage` (`OperationInterlockLanguage.Fbd` default, or `Scl`), selected before constructing `Project` and frozen by the base constructor. It affects only operation interlock FBs; `AddPH` signatures and operation call identity are unchanged. Ordered phase collection is explicit in `XmlOperation.PhaseOrder`; the interlock descriptor is finalized at upload, after all phases exist. An operation without phases has no interlock FB.

SCL interlock names accept ASCII letters/digits/underscores/hyphens (first character letter/underscore; 128-character limit includes generated suffixes). Hyphens are retained, not normalized away. The renderer quotes the whole member name in both declarations (`"PH_Re-Inert_PRECOND_01" : Bool;`) and local references (`#"PH_Re-Inert_PRECOND_01" := FALSE;`). Quotes are source syntax only; model/interface member names remain `PH_Re-Inert_PRECOND_01`. Region labels retain the original phase name. Unsupported quotes/control characters/non-ASCII characters are rejected before upload. Simple-name source snapshots remain unchanged.

SCL interlocks bypass the FBD network/member editor entirely. `OperationInterlockBatch` preflights all requested names and existing interfaces before generation. Existing compatible SCL bodies/comments/extra members remain untouched. Missing members, different languages/types, or wrong-folder/global-name collisions stop with a reconciliation diagnostic; no implicit merge or conversion occurs. New SCL blocks are generated directly into their existing folder using the external-source target-group overload, compiled, and checked through an actual export. KeepOnError retains failed generated evidence; dependent callers are not uploaded after failure. The backend only deletes its own successful external source and retains local sources/diagnostics. The legacy duplicate upload enumeration is idempotent for SCL.

The pure renderer embeds the normalized template contract, uses Program_Alarm instruction/version metadata (no hand-coded internals), and emits Function/general/phase regions with cyclic FALSE assignments. Later FALSE lines overwrite earlier Function calculations unless the user adjusts them. Preflight requires State public/access/visibility/SetPoint parity with the FBD template. Installed V21 help topics `131792485771.htm`, `119205656075.htm`, and `53581488011.htm` in `TIAPortalOpennessenUS.zip/37231359755` establish folder generation, compilation diagnostics, and overwrite semantics. V21 x64/AnyCPU builds and `Tests/Verify-OperationInterlocks.ps1` pass offline; live Program_Alarm attributes, SCL compilation, EN/ENO and rerun behavior remain unverified. V20 APIs are absent. See README for commands and boundaries.

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

Trailing `//` descriptions on `AddAnalog`, `AddDigital`, `AddValve`, `AddValveControl`, `AddMotor`, `AddMotorControl`, and `AddPidControl` calls are written to the called FB instance's direct `Static` declaration comment. For example, `// Level radar sensor BV130 B13` describes instance `LC-B13` of `fbMonAn`. Analog/digital monitor `_InstanceN` declarations receive the same description. For `AddValve`, `AddValveControl`, `AddMotor` (`fbMotS`), and `AddMotorControl` (`fbMotAn`), every configured ordinary and safe `_InterlockN` declaration (`fbIntlck7`) also receives the exact parent description. Out-of-range helpers and unrelated manual instances retain their own comments. Cached calls use the original configured target counts; changing counts does not reconcile topology. This works for new, existing, and cached devices in both staging modes, using the selected app's caller source file.

Empty or unavailable source descriptions preserve existing comments. Nonempty text replaces only the supported target language (en-US, otherwise de-DE, otherwise the first supported culture); other translations and nested comments, including the unit wrapper's `FUNC_TEXT`, remain intact. Source descriptions no longer populate FBD network comments. Existing network comments are preserved. Instance descriptions are independent of network formatting; selectable SCL operation interlocks are described above.

`XmlLocalInstance.SetDeclarationComment` writes the outer declaration comment and marks its block changed only when XML changes. `SetMemberComment` retains its nested-member meaning. Constructors keep the legacy `networkComment` named parameter for compatibility, but route it to the declaration. Covered Add methods also apply descriptions after cache lookup. Known declaration identities and FB datatypes are checked; missing/mismatched declarations produce a diagnostic rather than a phantom member. New local declarations use interface/v5; applying a description repairs legacy unqualified elements only within that instance.

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
- `qualityBit`: only when enabled, creates the QB input/tag and connects inverted `IN_<name>_QB` (or `hwIN.<SafeMember>_QB`) to `ERR_EXT` on the base and all configured extra monitors. Disabled regeneration neutralizes exact stale generated QB references with good-quality Bool constants, preserving UIds, wires, negation, unrelated manual wiring and existing PLC tags.
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

- Digital helper tag records now receive the explicit `qualityBit` setting, so disabled digital QB tags are not created. Valve helper tag records still have their own defaults; their `_QB` tag behavior is separate.
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

### Workbook Devices And IOBindings Split

For the G-009 unit workbook primer:

- `Devices` describes generated process objects and maps to existing `Add...` methods.
- `IOBindings` describes hardware source evidence: `UnitName`, `DeviceName`, `SignalRole`, `HardwareTag`, optional scaling, `SourceRef`, and `Comment`.
- Standard signal identity and dbIO paths are derived from generator rules, not typed into the workbook.
- Use `SignalOverride` only for non-standard/manual signals that cannot be derived from `DeviceName + SignalRole`.
- Generated DB/UDT staging copies available comments into generated dbIO/UDT members where supported.
- Generated DB/UDT staging uses the unit-first DB path shape: `dbIO.<UnitName>.IN.<safe member>` and `dbIO.<UnitName>.OUT.<safe member>`.
- Generated UDT names are `hwIN_<UnitName>` and `hwOUT_<UnitName>`.
- `AddPidControl` contributes no hardware IO in either staging mode. It inserts/reuses `fbPIDCtrl` and its direct Static instance description while retaining configuration, internal setpoint/output values, HMI, RESET and unit alarm integration. It does not register or wire `<name>_SP` / `<name>_OUT` hardware outputs; explicit similarly named outputs from other callers remain legitimate.
- Unit `hwIN`/`hwOUT` interfaces and Plant bindings are prepared by `PlcProject.PrepareSignalStaging` during generated `CreateTags`, once the inventory is complete. Fresh PID-only units still generate their device/unit calls, but have no inventory entry, staging UDT pair, dbIO unit member or new hardware interface/binding. A general empty dbIO follows the existing lifecycle.
- Regenerated UDTs for units still in the inventory omit former PID-derived fields. No migration/deletion pass removes old tags, dbIO members, interfaces, bindings or manual content; old artifacts for a PID-only unit can remain. Such cleanup requires separate ownership and preservation rules.
- `fcReadIn` and `fcWriteOut` creation/manipulation is intentionally deferred to a later spec; current files are source evidence only.
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

These files are used by the simulator and are currently maintained manually. After the G-009 external project-definition workbook upgrade, this template should be modified and integrated into the automatic generation workflow.

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

### SFC controller HSS connection (G-016)

The three constructor routes delegate to `XmlBlock.ConfigureSfcControl`: `XmlUnit` uses network `Unit sfc control` / instance `sfc_<unit>`; `XmlOperation` uses network `<operation>` / `sfc_<operation>`; `XmlPhase` uses its owning operation's network `Sfc control <phase>` / `sfc_<phase>`. The helper creates a call only when the network is new and otherwise requires the exact local `fbControlSfc` identity and Static declaration. Only fresh networks run the existing initializers. Cached AddUnit/AddOP/AddPH retain the configured calls.

`XmlNetwork.ConfigureSfcHss` configures Input Bool HSS as a direct GlobalVariable with components F_Safe/HSS. It is independent of signal staging and FBD/SCL interlock selection. It recognizes legacy unqualified v4 construction and qualified v4/v5 export, while new HSS elements use the source namespace. It adds missing wiring, reconciles simple wrong/negated connections and leaves already correct wiring/XML/dirty state unchanged. Shared old operands and their other users remain intact; only the targeted endpoint moves. Multiple drivers and unsupported logic sources fail before mutation. HSS wire/access/open/negation caches stay consistent. Only duplicate HSS wire indexing is tolerated during parsing so the targeted operation can diagnose the ambiguity; unrelated duplicate indexing retains existing behavior.

The target must have updated `fbControlSfc.Input HSS : Bool` and an existing `F_Safe.HSS : Bool`. HSS reference creation deliberately bypasses GetValueFromGlobalDB, which could otherwise add a missing member automatically. No hardware inventory, marker tags, unit interfaces, fallback member or library replacement is introduced; existing device PROT wiring is unchanged. Normal controller import errors include the prerequisite and preserve the original exception/XML path. Offline tests cannot confirm the supplied PLC FB's semantics or the live target's member/type compatibility.

Run `Tests/Verify-SfcHss.ps1` against the rebuilt V21 library (default x64; use `-LibraryPath ./etu-process-generator-TiAv21/bin/Debug/TIA_LIB.dll` for AnyCPU). Tests invoke the executable shared call-resolution/configuration path on bare unit/operation XML objects, with constructor integration guards and actual cached Add methods. They bypass live Portal/unit/operation/phase constructors and never Upload/import. A disposable-project compile with the updated library remains the live acceptance step.

### Duplicate connections when adding devices to an existing unit

A V21 import failure reporting a connection used multiple times can originate in unit alarm aggregation. The retained `fbBV130_B10_20.xml` (2026-10-22, AnyCPU output) has CompileUnit ID 3 at line 10498 with four wires driving `fbUnit.ALARM`: original wire 45 plus wires 64/70/76 added for TIC-1174, TIC-1176 and PIC-1164. Siemens internal part/connection identifiers in the exception are not the exported XML endpoint IDs.

The device constructors' aggregation searches still assumed FlgNet/v4 after the shared reader had been made namespace-aware. On exported v5 networks they missed the existing OR and appended another one for every new device. Analog, Digital, Valve, ValveControl, Motor, MotorControl and PidControl now switch their aggregation namespace to the unit network's `SourceNamespace` before looking up ALARM/WARN. Existing logic/wires are reused; no cleanup of arbitrary old/manual content is performed. `Verify-DeviceStagingInputs.ps1` exercises actual Add methods, editing/reopening existing devices and adding all seven families over two qualified v4/v5 cycles, both staging modes, unique endpoints, original OR identity/wire preservation, manual comments and every device's QERR contribution. No live generator/import is part of these tests.

### Device descriptions return as network comments

Check the active branch's source, not only session memory. `fix-v21-phase-template-headers` was based on main at `2ecfd32`, while the instance-comment implementation existed only in `6dee8e1` on `g-012-device-instance-comments`. Building the older branch restored old network-comment behavior. That dependency is now integrated into the current working tree while retaining subsequent fixes. All seven Add methods call SetDeviceDescription after device lookup, constructors call ConfigureInstanceDescription, and XmlLocalInstance.SetDeclarationComment updates the outer Static comment with namespace/culture preservation. Existing network comments are deliberately retained; these descriptions are no longer newly written there. Run `Tests/Verify-DeviceInstanceComments.ps1` with the other regression scripts on both output folders to detect recurrence.

### Undefined staged digital QB or disabled valve feedback

If `fbA2` compilation reports missing `#hwIN.LS1181_QB` or `#hwIN.FB_OPN_VV1008`, compare the generated network with the Add flags before adding UDT members. `AddDigital` defaults `qualityBit=false`; all additional monitors must honor that flag too. `AddValve` may enable only open or closed feedback; each side must be generated independently. The staging inventory intentionally omits disabled inputs.

These defects are fixed in Digital/Valve construction. Their cached Add paths also repair existing exact disabled generated input references through `XmlNetwork.ReplaceInputReferenceWithConstant`: disabled quality becomes TRUE (good quality before existing inversion), disabled position feedback becomes FALSE. The replacement retains access UId, wires and negations, updates access/wire caches, and leaves other operands intact. It performs no live import until the normal authorized generator run. It does not add fictitious hardware inputs or rewrite the rest of the network. Enabled valve feedback lookup uses the source network namespace. `Tests/Verify-DeviceStagingInputs.ps1` covers actual Add methods, fresh/cached/exported networks, flags, both staging modes and idempotence without live constructors. Interface-change diagnostics accompanying undefined operands may be secondary; a live compile is needed to determine remaining issues and warning details.

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

Confirmed on 2026-10-20 after restoring `EmptyUnit.xml`: the next run failed at `XmlCall.cs:49` (`Network.Parts.AddFirst(Xml)`). Repo `EmptyNetwork.xml` had only `<NetworkSource />`; it was well-formed XML but lacked the FlgNet/v4 `Parts` and `Wires` containers required by `XmlNetwork`. Restored that structure from the user-provided original library source. An offline harness using the actual network/call/wire classes and a non-Portal block stub reproduced the old failure and passed call/access/wire construction and serialization after the template fix. Verified both `bin/Debug` and `bin/x64/Debug`; no Portal run or import acceptance is implied.

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

On existing-network reruns, read the actual namespace from the network's FlgNet element. XmlNetwork.SourceNamespace is shared by its call/access/logic/wire readers; a hard-coded v4 lookup can miss an existing call and Parts container when a later export uses another namespace, causing XmlCall.AddFirst to throw. Offline qualified v4/v5 round-trip fixtures cover reuse/preservation and extensions. Newly created templates still use the legacy v4 path. Do not rewrite an existing network's namespace or infer its actual version from an installed schema filename. Unsupported non-editable structures stop with a block/network diagnostic before call insertion.

```text
http://www.siemens.com/automation/Openness/SW/NetworkSource/FlgNet/v4
```

### TIA Import XML Invalid

For the V21 generated-staging UDT error `Missing 'Namespace' identifier attribute from the '0' object`, the required identifier is `<AttributeList><Namespace /></AttributeList>` on `SW.Types.PlcStruct`, not an `xmlns` attribute on the document. The empty value selects the global PLC namespace. Installed help topics `183387162763.htm` and `159239377803.htm` document the V18+ requirement and UDT example. `XmlUdt` now emits it under `TIA_V21`.

A separate defect in generated staging interfaces placed `Member`, `Comment`, and `MultiLanguageText` outside the interface/v5 XML namespace. The installed `SW.InterfaceSections_v5.xsd` rejects those members. V21 `XmlDataMember.ToXElement` (including recursive dbIO structs) and `XmlBlock.GetInterfaceMember` now emit qualified elements; V20 behavior is preserved by the build constant. `Tests/Verify-V21StagingXml.ps1` checks both UDT directions, comments, nested DB members, and idempotent unit interface additions against the installed XSD using the rebuilt library, without invoking side-effecting constructors or Upload. Both Debug outputs pass; runtime import compatibility still needs a user test.

The subsequent option-2 run passed UDT import and stopped at `XmlDatablock.Upload`: `The engineering version 'V17' ... is not supported.` Retained `dbIO.xml` confirmed the DB fallback still inherited the old header from `EmptyDatablock.xml`. `XmlDatablock.LoadEmptyTemplate` now sets V21 engineering metadata and inserts an empty global Namespace only for the V21 new-DB fallback. XML exported from existing DBs bypasses this loader, and V20 uses the original template unchanged. The offline test now invokes this pure loader using the deployed template, adds/replaces the actual staged unit with `SetMember`, and verifies complete DB version/identifier plus interface XSD. Merely checking nested member XML is insufficient to catch document-header incompatibilities.

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

Confirmed during the V21 runtime test on 2026-10-20: `XmlBlock` creates the unit's TIA folder before loading `Xml/EmptyUnit.xml`, so this exception leaves an empty unit folder. The original project declared only six content files; successful builds and checks of those declarations missed four source-required templates.

Recovered `EmptyUnit.xml`, `EmptyOperation.xml`, `EmptyPhase.xml`, and `EmptyNetworkWithOutCall.xml` unchanged from the user-provided `\\tsclient\D\Software\UnifiedSprechstunde15\UnifiedSprechstunde15\bin\x64\Debug\Xml`. Both repo-local library project files now deploy all ten templates. Check template paths referenced by source as well as declared content; verify the actual configuration output (the reported run used `bin/Debug`, not `bin/x64/Debug`). XML parsing and matching template hashes were checked for both V21 Debug outputs. Runtime import compatibility still requires a separate test.

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
