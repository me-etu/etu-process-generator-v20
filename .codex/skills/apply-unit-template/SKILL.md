---
name: apply-unit-template
description: Convert a filled G-009 unit_template workbook or compact JSON unit handoff into simple Project.cs edits using the repo's standard temp-variable Add... call pattern. Use when Codex needs to read Units, Devices, and optional IOBindings or extra config comments, then prepare or edit Project.cs without implementing DB/UDT staging, fcReadIn, or fcWriteOut.
---
# Apply Unit Template

Use this skill to turn a reviewed `unit_template.xlsx`, filled unit workbook, or compact JSON handoff into basic `Project.cs` generator calls.

This skill is intentionally narrow: create the normal generated process objects first, using the existing temp-variable style in `Project.cs`. Do not implement G-008 DB/UDT staging, `dbIO`, `fcReadIn`, or `fcWriteOut` here.

## Baseline

1. Inspect `git status --short` and preserve user changes.
2. Read `SKILLS.md`, `GENERATOR_KNOWLEDGE_BASE.md`, and the relevant `Project.cs` area before editing.
3. Confirm method signatures in `TIAOpenness/TIA_Lib/TIA_LIB/PlcProject.cs` or `GENERATOR_KNOWLEDGE_BASE.md`.
4. Read the supplied source:
   - For workbook input, read `Units`, `Devices`, and optionally `IOBindings` only for comments/source awareness.
   - For compact JSON input, read the adjacent README/key file first, then parse the JSON.
5. Do not run the generator app or TIA Portal. A compile-only check is acceptable if requested or useful.

## Scope

Implement only workbook/JSON-to-`Project.cs` generated object calls:

- `Units.Enabled=true` or compact `units` entries define unit setup context.
- `Devices.Enabled=true` or expanded compact `devices` entries become the matching `Add...` call.
- `IOBindings` is not used to create hardware bridge logic in this skill.
- `IOBindings.Comment` and compact JSON `extraConfigs` may be used as inline source comments when helpful.
- Unsupported fields in compact `extraConfigs`, such as PID internal limits, are preserved as comments/source awareness unless the current DSL explicitly supports them.

## Compact JSON Intake

Accept compact JSON handoff files with schema `g009-unit-payload-compact-v1`.

Before editing `Project.cs`:

1. Read the README/key file adjacent to the JSON and use it as authoritative for row order, defaults, assumptions, and verification counts.
2. Parse `defaults`, `units`, `devices`, `extraConfigs`, and `verification`.
3. Expand each compact device row by applying `defaults[DeviceType]` and then row-specific values.
4. Treat expanded rows exactly like enabled workbook `Devices` rows.
5. Stop and ask if a compact row lacks a required field after expansion or if README row definitions conflict with JSON content.

JSON-specific rules:

- Do not invent generator parameters from `extraConfigs` unless an existing `Add...` method supports them.
- Add concise comments for comment-only `extraConfigs` that matter for future work.
- Confirm expanded counts match `verification` before and after editing.
- Report skipped or comment-only `extraConfigs` explicitly.

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

Map enabled/expanded `DeviceType` rows to existing DSL methods:

- `Analog` -> `AddAnalog(...)`
- `Digital` -> `AddDigital(...)`
- `Valve` -> `AddValve(...)`
- `ValveControl` -> `AddValveControl(...)`
- `Motor` -> `AddMotor(...)`
- `MotorControl` -> `AddMotorControl(...)`
- `PidControl` -> `AddPidControl(...)`

Use workbook/expanded JSON columns only when non-empty or clearly required. Let existing method defaults stand where a value is blank or omitted.

## Editing Rules

1. Add a clear unit section if the file already groups generated calls by unit.
2. Keep edits surgical: only add or update rows for the supplied workbook/JSON unit scope.
3. Preserve existing manual code, generated code, comments, and unrelated units.
4. Do not invent interlocks, phases, parameters, PID behavior, panel behavior, `dbIO` variables, or bridge functions from `IOBindings` or `extraConfigs`.
5. If a row is ambiguous or conflicts with `Project.cs` conventions, stop and ask with a concise list of blocking questions.

## Validation

After editing:

- Check that every enabled/expanded `Devices` row has one intended `Add...` call.
- Check that disabled workbook rows or disabled compact rows did not generate calls.
- Check that names are copied exactly unless the user approved normalization.
- For compact JSON, confirm generated call counts match `verification`.
- Report any rows intentionally skipped and why.
- Report any `extraConfigs` kept as comment-only because the current DSL does not support them.
- Prefer a targeted compile of the app project over running TIA.
