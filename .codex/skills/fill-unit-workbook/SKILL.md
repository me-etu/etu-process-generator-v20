---
name: fill-unit-workbook
description: Fill or prepare G-009 unit workbook rows from mixed unit source material. Use when Codex needs to extract unit Devices and IOBindings from TASKS-UNITS folders, tables, notes, screenshots, P&IDs, dbIO/db files, SCL fcReadIn/fcWriteOut examples, or other engineering inputs; ask clarifying questions before writing ambiguous workbook rows.
---
# Fill Unit Workbook

Use this skill to convert unit source evidence into reviewable workbook rows for `unit_template.xlsx` without guessing hidden engineering intent.

## Baseline

1. Inspect `git status --short` and preserve existing user changes.
2. Read `SKILLS.md` and `GENERATOR_KNOWLEDGE_BASE.md` sections on workbook/G-008/G-009 behavior.
3. Read `Specs/G-009-external-project-definition-workbook-prd.md` and `Specs/G-008-generated-db-staging-prd.md` only around `IOBindings`, workbook sheets, and generated DB/UDT staging.
4. Read `references/workbook-schema.md` from this skill before deriving rows.
5. Do not run the generator app or TIA Portal.

## Source Intake

Use all available local evidence before asking the user:

- Unit notes and tables such as `TASK.txt`, `Notes.txt`, copied Excel/CSV tables, Markdown, or text exports.
- Images/screenshots/P&IDs supplied inline or stored near the task. If local image viewing fails, report that limitation and continue from text sources.
- `dbIO.db`, UDT exports, tag tables, or SCL snippets such as `fcReadIn` and `fcWriteOut` as source evidence, not as authoritative generated output.
- Existing `Project.cs` and `PlcProject.cs` method signatures when mapping to `Add...` calls.

## Extraction Workflow

1. Identify the unit name and source files read.
2. Build candidate `Units` and `Devices` rows first.
3. For each generated device, map `DeviceType` to the existing DSL: `Analog`, `Digital`, `Valve`, `ValveControl`, `Motor`, `MotorControl`, `PidControl`.
4. Build optional `IOBindings` rows only as hardware source evidence: `UnitName`, `DeviceName`, `SignalRole`, `HardwareTag`, optional scaling, `SourceRef`, and `Comment`.
5. Derive standard signal identity from `DeviceName + SignalRole`; do not ask the user to type generated tags or dbIO paths for standard device signals.
6. Use `SignalOverride` only for custom/manual signals that cannot be derived from a generated device.
7. Keep comments useful; future generation should copy `IOBindings.Comment` into generated dbIO variable comments where supported.

## Ambiguity Rules

Ask the user before writing rows when any high-impact ambiguity remains:

- Device count, naming expansion, or tag normalization is uncertain.
- A source signal could be either a generated device, interlock, panel command, custom status, or pure logic signal.
- Valve feedback monitoring, quality-bit usage, inversion, interlock counts, or analog ranges are unclear.
- A hardware tag exists but the matching generated device/signal role is uncertain.
- A non-standard/manual signal needs `SignalOverride` and naming intent is unclear.

Prefer a concise findings table plus explicit questions. Do not fill the workbook until the user confirms assumptions or asks you to proceed with marked review notes.

## Output Shape

When reporting findings before editing:

- List source files inspected.
- Summarize confident `Devices` rows and uncertain candidates separately.
- Summarize `IOBindings` candidates using `SignalRole + HardwareTag + Comment`, not derived dbIO paths.
- List questions that block safe workbook entry.

When editing later:

- Update the workbook source generator if the schema changes; otherwise edit/regenerate the workbook consistently with the current template.
- Verify workbook headers after changes.
- Do not import to Google Sheets unless the user asks.
