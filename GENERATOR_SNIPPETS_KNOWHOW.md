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

## Implementation-Backed Argument Notes

The `Add...` methods are thin wrappers over repo-local `TIA_LIB` classes. Use these notes when choosing snippet arguments:

- `unit_name` always selects or creates the unit; `name` is the device/instrument tag inside that unit.
- Device object names become `UnitName_TagName`, while PLC tags commonly use prefixes such as `IN_`, `CTRL_`, `FB_OPN_`, `FB_CLS_`, and `FB_ON_`.
- `iconType` is written into the generated FB call as `TYP_ICON`; preserve known values unless a source explicitly identifies the visual/device variant.
- `interlockCount` and `interlockSafeCount` create `fbIntlck7` calls and wire them to `LOCK` and `S_LOCK`; do not invent counts from vague comments.
- `instanceCount` on analog/digital signals creates extra monitor instances and aggregates their alarm/warning outputs; leave it `0` unless multiple real monitor instances are required.
- `qualityBit` changes generated monitor wiring for digitals and valves, but current tag creation still tends to create `_QB` tags by default because the helper tag records have their own defaults.
- `mon_const = false` with a valid `tp_number` links monitoring time to `TP<number>|OUT`; `mon_const = true` with `mon_t` writes a literal monitoring time.
- `type = true` on phase, TP, or RP parameters means integer/text-list style behavior; keep it paired with a real text-list name.

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

Argument reminders:

- `qualityBit = true` wires the monitor external-error input to `IN_TAG_QB`; use it when the source signal has usable signal-quality information.
- `neg` is the inversion flag for `IN_TAG`; derive it from NC/NO/NAMUR behavior when the source is clear, otherwise mark for review.
- `colorType` is passed through the public signature but is not currently used by the `Digital` implementation; keep the project default `0`.
- `instanceCount` should remain `0` for ordinary one-signal sensors.

Analog call defaults used in this project:

```csharp
AddAnalog("UNIT", "TAG", 2, 0, "unit", decimals, min, max);
```

Argument reminders:

- `unity`, `decimals`, `min`, and `max` are written directly into `UNIT`, `NUM_POINTS`, `LO_LIM`, and `HI_LIM`.
- Analog input tags are generated as `IN_TAG` integer marker words, so physical scaling assumptions should be visible in comments when the source range is not obvious.
- Use ASCII-safe engineering units in snippets, for example `deg C`, `m3/h`, `Nm3/h`, and `uS/cm`.

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

Argument reminders:

- `mon_opn` wires `FB_OPN_TAG`; `mon_cls` wires `FB_CLS_TAG`; infer them only from matching open/closed feedback evidence.
- `qualityBit = true` combines feedback and feedback quality bits before wiring the valve monitor; use `false` only when quality-bit handling is intentionally absent.
- `neg` applies to feedback inputs, not the command output.
- `tp_number` is only used when `mon_const = false`; the common pattern above links monitoring time to `TP1|OUT`.
- If `mon_const = true`, pass `mon_t` deliberately and comment the source of the fixed time.

Control valve pattern used in this project:

```csharp
AddValveControl("UNIT", "TAG", 2, 0, 0, "%", 1);
```

Argument reminders:

- Control valves write `UNIT`, `NUM_POINTS`, and output `QSETPOINT_INT` to generated `CTRL_TAG`.
- Use `AddValveControl(...)` only when the actuator has an analog/proportional setpoint, not merely open/closed feedback.

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

Argument reminders:

- `AddMotor(...)` registers `FB_ON_TAG` and `CTRL_TAG` for PLC tag creation.
- `AddMotorControl(...)` writes controlled-motor config, unit, decimals, and monitoring parameters, but current tag registration is not as explicit as `AddMotor(...)`; keep related speed/control source evidence in comments.
- `mon_on` enables running feedback monitoring.
- Monitoring-time arguments follow the same rule as valves: TP-linked when `mon_const = false`, literal when `mon_const = true`.

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

Argument reminders:

- `countP` controls how many `P01...P05` parameter groups are active and wired as `SP01...SP05`.
- Each parameter group is `type, min, max, decimals, unit, textList` in the public `AddPH(...)` call.
- For `type = false`, HMI range/GMP setup targets the real setpoint tag.
- For `type = true`, HMI range/GMP setup targets the `_SP_INT` tag and the text list name should be explicit.

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

Argument reminders:

- `number` becomes `TP<number>`; omit the `TP` prefix in snippets.
- `def_value`, `sp_min`, and `sp_max` are written to `SP_DEF`, `SP_MIN`, and `SP_MAX`.
- `isSafety` is written to the generated `fbTP` call, so only mark it true with source evidence.

RP pattern:

```csharp
AddRp(number, sp_min, sp_max, "unit", numbDecPoints, type, "txtList_name");
```

Argument reminders:

- `number` becomes `RP<number>`; omit the `RP` prefix in snippets.
- `sp_min` and `sp_max` are written to `MTR_MIN` and `MTR_MAX`.
- Numeric RPs get HMI range bindings; `type = true` uses the text-list/int-style UI path.

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
