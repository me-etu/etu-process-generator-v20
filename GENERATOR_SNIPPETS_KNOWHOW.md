# Generator Snippet Preparation Know-How

This note captures the working method for preparing reviewable C# snippets for the project generator code. The goal is to produce small, traceable `Add...` call blocks that can be copied into the clean `Project.cs` generator file with minimal rework.

## Core Principle

Treat `Project.cs` as a declarative generator script, not as ordinary application logic.

The snippets should describe the plant model through the existing project DSL:

- `AddDigital(...)`
- `AddAnalog(...)`
- `AddValve(...)`
- `AddValveControl(...)`
- `AddMotor(...)`
- `AddMotorControl(...)`
- `AddPH(...)`
- `AddTp(...)`
- `AddRp(...)`

Before creating or changing snippets, check `PROJECT_CS_METHODS.md` for the current method signatures and expected argument meaning.

## Source Priority

Use the source document that owns the data type:

- Sensors: `30073487_List of Sensors Working List_01.xlsx`
- Valves and motors: `30073487_List of Actuators Working List_01.xlsx`
- Physical measuring ranges: `Instrument_List_Detail_Design_2026_03_24.pdf`
- Electrical switch/NAMUR limits: `Special_Electrical_Items_2026_03_24.pdf`
- Phases, operations, TP, and RP: functional description PDFs
- Interlock and alarm text support: Cause and Effect Matrix plus text-list workbooks

When two sources disagree, keep the source row or section visible in a comment and add a `Review:` comment instead of silently choosing a value.

## General Snippet Layout

Each snippet file should have:

1. A short header that names the source documents.
2. The relevant `Add...` method signatures copied as comments.
3. Notes about manual rules, omissions, placeholder values, and inferred fields.
4. Calls grouped by normalized unit.
5. Source row or source section comments immediately above the generated call.

Example comment style:

```csharp
// Row 24: PIT0060; Pressure; signal SL; assignment HYD-008-Supply
AddAnalog("GeneralSignals", "PIT0060", 2, 0, "bar(g)", 1, -1, 10);
```

## Naming Rules

- Remove dots from device names before writing C# calls or comments.
- Use the project tag exactly after normalization.
- Keep unit names ASCII-only.
- Use current project units only: `HYD008`, `TK002`, `TCG032`, `SCR005`, and `GeneralSignals`.
- Assign `HYD-008-Supply` workbook items to `GeneralSignals` in generator snippets.
- Do not write old-to-new conversion comments such as `old.tag -> newtag`; write only the normalized project tag.

## Sensor Snippets

Use `AddAnalog(...)` and `AddDigital(...)`.

Preparation rules:

- Start from the sensor working list as the row authority.
- Classify analog signals from evidence such as `4...20mA`, `4-20mA`, HART, valve positioners, and analog process measurements.
- Classify digital signals from evidence such as NAMUR, `ZSC`, `ZSO`, `SOH`, `LS`, `FISL`, `GS`, `XV`, and `NC`.
- Exclude valve, control, and feedback rows from the sensor snippet when they belong to valve functions in TIA Portal.
- Keep duplicate source rows only when they represent separate real signals.
- Normalize units to ASCII, for example `deg C`, `m3/h`, and `Nm3/h`.
- Add `Review:` comments for missing ranges, undefined signal types, or inferred placeholder limits.

Digital call defaults used in this project:

```csharp
AddDigital("UNIT", "TAG", iconType, 0, 0, true, neg);
```

Analog call defaults used in this project:

```csharp
AddAnalog("UNIT", "TAG", 2, 0, "unit", decimals, min, max);
```

## Valve Snippets

Use `AddValve(...)` for discrete valves and `AddValveControl(...)` for control valves.

Preparation rules:

- Start from the actuator working list as the valve authority.
- Skip non-valve actuators.
- Collapse duplicate valve tags into one call and list all source rows in the comment.
- Infer `mon_opn` and `mon_cls` from matching `ZSO` and `ZSC` feedback rows in the sensor list.
- Use `AddValveControl(...)` for control valves, proportional valves, and analog-positioned valves.
- Use `AddValve(...)` for ball, diaphragm, solenoid, and other discrete valves.
- Keep HSP/SSP state in comments because it helps review monitoring and fail-state assumptions.
- Add a `Review:` comment when no matching position feedback row is found.

Discrete valve pattern used in this project:

```csharp
AddValve("UNIT", "TAG", 2, 0, 0, mon_opn, mon_cls, false, true, true, 1);
```

Control valve pattern used in this project:

```csharp
AddValveControl("UNIT", "TAG", 2, 0, 0, "%", 1);
```

## Motor Snippets

Use `AddMotor(...)` or `AddMotorControl(...)`.

Preparation rules:

- Start from actuator rows whose source `Type` is motor-like.
- Do not promote valve descriptions that merely mention agitator, pump, or air supply.
- Use `AddMotorControl(...)` when the functional description controls a speed or setpoint.
- Keep source-name mismatches and related speed sensors in concise `Review:` comments.

Current project pattern for the agitator:

```csharp
AddMotorControl("HYD008", "A001", 6, 0, 0, "rpm", 1, mon_on, false, 2);
```

## Phase Snippets

Use `AddPH(...)`.

Preparation rules:

- Use functional description body sections, not only the table of contents.
- Group by unit, then operation, then phase.
- Normalize operation spacing, for example `OP1_ Pressure` becomes `OP1_Pressure`.
- Preserve source phase spelling when it is plausibly intentional.
- Put Non-Technical parameters in comments directly above the call.
- Map phase parameters to recipe parameter ranges where possible.
- Leave a comment when the phase text says `None`, the section is malformed, or a setpoint has `TBD` limits.

## TP And RP Snippets

Use `AddTp(...)` for technical parameters and `AddRp(...)` for recipe parameters.

Preparation rules:

- Omit the `TP` or `RP` prefix from the numeric argument.
- Normalize decimal commas to C# decimal points.
- Preserve source names as comments above each call.
- For list-style choices, set `type = true` and provide an explicit `txtList_...` name.
- Keep manual overrides visible in the snippet header.

TP pattern:

```csharp
AddTp(number, def_value, sp_min, sp_max, "unit", isSafety, numbDecPoints);
```

RP pattern:

```csharp
AddRp(number, sp_min, sp_max, "unit", numbDecPoints, type, "txtList_name");
```

## Per-Unit Snippets

When regrouping snippets per unit, use this section order:

1. Sensors
2. Valves
3. Motors
4. Phases

Keep explicit empty comments such as `// No motors found` so a reviewer knows the category was checked.

## Assembly Into Project.cs

When preparing a clean generator file:

- Create or update a reviewable copy under `Codex Work`; do not overwrite reference files.
- Assemble only active project units.
- Append shared `AddTp(...)` and `AddRp(...)` sections after unit device and phase definitions.
- Keep useful engineering `Review:` comments when they affect trust in a tag, range, monitoring flag, or placeholder.
- Avoid carrying old commented template blocks into the clean project file.
- Confirm that legacy example units are absent.
- Count active `Add...` calls and compare with the source snippets.

## Review Checklist

Before delivering snippets, check:

- All device names are dotless.
- Units are one of the intended project units.
- `HYD-008-Supply` items are assigned to `GeneralSignals`.
- Valve feedback rows are not duplicated as sensors.
- Duplicate actuator tags are collapsed.
- Missing source evidence is marked with `Review:`.
- Decimal commas were converted to decimal points.
- Units are ASCII-safe.
- The snippet header explains assumptions and omissions.
- Active `Add...` call count matches expectations.

