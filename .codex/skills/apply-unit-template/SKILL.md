---
name: apply-unit-template
description: Convert a filled G-009 unit_template workbook into simple Project.cs edits using the repo's standard temp-variable Add... call pattern. Use when Codex needs to read workbook rows for Units, Devices, and optional IOBindings, then prepare or edit Project.cs without implementing DB/UDT staging, fcReadIn, or fcWriteOut.
---
# Apply Unit Template

Use this skill to turn a reviewed `unit_template.xlsx` or filled unit workbook into basic `Project.cs` generator calls.

This skill is intentionally narrow: create the normal generated process objects first, using the existing temp-variable style in `Project.cs`. Do not implement G-008 DB/UDT staging, `dbIO`, `fcReadIn`, or `fcWriteOut` here.

## Baseline

1. Inspect `git status --short` and preserve user changes.
2. Read `SKILLS.md`, `GENERATOR_KNOWLEDGE_BASE.md`, and the relevant `Project.cs` area before editing.
3. Confirm method signatures in `TIAOpenness/TIA_Lib/TIA_LIB/PlcProject.cs` or `GENERATOR_KNOWLEDGE_BASE.md`.
4. Read the workbook sheets needed for this pass: `Units`, `Devices`, and optionally `IOBindings` only for comments/source awareness.
5. Do not run the generator app or TIA Portal. A compile-only check is acceptable if requested or useful.

## Scope

Implement only workbook-to-`Project.cs` generated object calls:

- `Units.Enabled=true` defines unit setup context.
- `Devices.Enabled=true` becomes the matching `Add...` call.
- `IOBindings` is not used to create hardware bridge logic in this skill.
- `IOBindings.Comment` may be used as an inline source comment near generated calls when helpful.

## Standard Temp-Variable Pattern

Follow the existing local style in `Project.cs`.

Prefer:

```csharp
var unit = myProject.AddUnit("Vakuumzentrale");
var analog = myProject.AddAnalog(unit.UnitName, "100-BP1", iconType: 2, unity: "mbar", numbDecPoints: 0, limMin: -2000, limMax: 0);
var valve = myProject.AddValve(unit.UnitName, "101-MB1", iconType: 3, mon_opn: true, mon_cls: true, qualityBit: false, neg: true);
```

Adapt variable names to the file's current conventions. Keep names readable, deterministic, and local to the unit block.

## Device Mapping

Map enabled `Devices.DeviceType` rows to existing DSL methods:

- `Analog` -> `AddAnalog(...)`
- `Digital` -> `AddDigital(...)`
- `Valve` -> `AddValve(...)`
- `ValveControl` -> `AddValveControl(...)`
- `Motor` -> `AddMotor(...)`
- `MotorControl` -> `AddMotorControl(...)`
- `PidControl` -> `AddPidControl(...)`

Use workbook columns only when non-empty or clearly required. Let existing method defaults stand where the workbook cell is blank.

## Editing Rules

1. Add a clear unit section if the file already groups generated calls by unit.
2. Keep edits surgical: only add or update rows for the workbook unit.
3. Preserve existing manual code, generated code, comments, and unrelated units.
4. Do not invent interlocks, phases, parameters, PID behavior, panel behavior, `dbIO` variables, or bridge functions from `IOBindings`.
5. If a row is ambiguous or conflicts with `Project.cs` conventions, stop and ask with a concise list of blocking questions.

## Validation

After editing:

- Check that every enabled `Devices` row has one intended `Add...` call.
- Check that disabled workbook rows did not generate calls.
- Check that names are copied exactly unless the user approved normalization.
- Prefer a targeted compile of the app project over running TIA.
- Report any rows intentionally skipped and why.
