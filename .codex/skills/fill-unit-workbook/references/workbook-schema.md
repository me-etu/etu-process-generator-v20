# Workbook Schema And Derivation Reference

Read this when filling unit workbook rows or reviewing extracted unit source data.

## Devices Sheet

`Devices` describes generated process objects. It maps to existing `Add...` methods.

Core columns:

```text
Enabled, UnitName, DeviceType, Name, IconType, ColorType, InstanceCount, QualityBit, Neg,
InterlockCount, SafeInterlockCount, MonOpn, MonCls, MonOn, MonConst, TpNumber, MonTime,
Unit, Decimals, Min, Max, UnitOut, DecimalsOut, SourceRef, Comment
```

Common mapping:

- `Analog` -> `AddAnalog(...)`, generated signal identity `IN_<Name>`.
- `Digital` -> `AddDigital(...)`, generated signal identity `IN_<Name>`.
- `Valve` -> `AddValve(...)`, identities `FB_OPN_<Name>`, `FB_CLS_<Name>`, `CTRL_<Name>`.
- `ValveControl` -> `AddValveControl(...)`, identity `CTRL_<Name>`.
- `Motor` -> `AddMotor(...)`, identities `FB_ON_<Name>`, `CTRL_<Name>`.
- `MotorControl` -> `AddMotorControl(...)`, controlled motor; source evidence for setpoint/speed control should be visible.
- `PidControl` -> `AddPidControl(...)`.

## IOBindings Sheet

`IOBindings` is hardware source evidence, not a manual dbIO schema.

Current columns:

```text
Enabled, UnitName, DeviceName, SignalRole, HardwareTag, SignalOverride,
ScaleRawMin, ScaleRawMax, ScaleEngMin, ScaleEngMax, SourceRef, Comment
```

Do not add or ask users to fill `DbIoPath`, `DbIoMember`, `DbIoType`, `DbIoUdtName`, or `GeneratedTag` for standard generated-device signals.

## SignalRole Derivation

Use standard derivation when `SignalOverride` is blank:

| SignalRole | Direction | Type | Signal identity |
| --- | --- | --- | --- |
| `AnalogInput` | `IN` | `Int` or analog raw type | `IN_<DeviceName>` |
| `DigitalInput` | `IN` | `Bool` | `IN_<DeviceName>` |
| `ValveOpenFeedback` | `IN` | `Bool` | `FB_OPN_<DeviceName>` |
| `ValveClosedFeedback` | `IN` | `Bool` | `FB_CLS_<DeviceName>` |
| `ValveCommand` | `OUT` | `Bool` | `CTRL_<DeviceName>` |
| `MotorRunFeedback` | `IN` | `Bool` | `FB_ON_<DeviceName>` |
| `MotorCommand` | `OUT` | `Bool` | `CTRL_<DeviceName>` |
| `ControlSetpoint` | `OUT` or internal by design | `Int`/`Real` | usually `CTRL_<DeviceName>` |
| `MaintenanceSwitch` | `IN` | `Bool` | derive only after naming convention is confirmed |
| `StatusOutput` | `OUT` | `Bool` or source-specific | requires `SignalOverride` unless tied to a generated device |
| `PanelCommand` | `INTERNAL` or source-specific | source-specific | requires `SignalOverride` unless tied to a generated device |
| `Other` | source-specific | source-specific | requires `SignalOverride` |

Future dbIO path shape is derived, not typed:

```text
dbIO.<UnitName>.IN.<safe member>
dbIO.<UnitName>.OUT.<safe member>
```

Signal identity keeps generator nomenclature. If TIA member naming rejects characters such as `-`, sanitize DB member names deterministically while preserving original signal identity/comment as metadata where possible.

Example fallback:

```text
FB_OPN_101-MB1 -> FB_OPN_101_MB1
```

## Clarification Checklist

Ask before writing rows when uncertain about:

- Expanded tag names such as `10X-MA1` or `110-MBX`.
- Whether a signal is a generated device row, interlock, panel command, status output, or custom logic.
- Valve `MonOpn`, `MonCls`, `QualityBit`, `Neg`, interlocks, or feedback availability.
- Analog range, unit, decimal places, raw scaling, or signal type.
- Whether custom rows should use `SignalOverride` and what exact canonical name to use.
