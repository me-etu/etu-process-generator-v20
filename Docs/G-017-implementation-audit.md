# G-017 implementation audit and planning checkpoint

Baseline: `main` at `398aa7f`. Work branch: `g-017-modular-generation`.
This checkpoint is **not an enabled global FBD/SCL generator**. Existing runtime
selection, interfaces, configuration paths and upload behavior are unchanged.

## User clarification

On 2026-10-22 the user explicitly chose to keep the existing global reset
references in standalone `fbTPs`/`fbRPs` for this upgrade. Their `HMI.RESET`
references are an intentional exception to Unit-local control routing.
Do not move these helpers into Units or introduce another invocation of them.

## Evidence and limits

- `C:/Users/User/Desktop/info/spec_details.md` is requirements prose, not PLC
  block/type implementations. Read only; no external file was changed.
- Retained trial `etu-process-generator-TiAv21/bin/Debug/fbA2.xml`, last written
  2026-10-21 14:14:31, contains expanded Static instance interfaces. SHA256:
  `6716A149AD59BF07E0BDD25C72173AFD330CEC117AD33173DF12EF0B0B71E824`.
- That trial proves the interfaces below at that time. Its controller interface
  lacks HSS, so it is not evidence of the updated controller definition. The
  user's successful G-016 run is runtime confirmation of that change, but is
  not a retained definition/body export.
- The trial contains no `fbMotS` instance and no vendor/device implementation
  bodies. Expanded configuration members are not an export of persistent
  `cfgData` start values or a capture of online actual values.
- V21 `TiaPortal.GetProcesses()` metadata enumeration returned no processes
  during this audit. No session was attached, project compiled, or live export,
  import, conversion or download performed.

## Checked contract table

### Additional current exports supplied during the audit

The user subsequently supplied two read-only evidence files in Desktop/info:

- `library_types.scl` (208170 bytes), SHA256
  `5840433D57A15BF14FC96D050BF4F9494BF9BF9EBF4B84AEED66C0C5B2605D71`.
  It defines nine FBs and seven types, including fbMotS/typeMotorCfg. The six
  non-PID device families all declare CFG in VAR_INPUT. Inspection found no
  ordinary direct CFG assignment in those exported implementations; a lexical
  assignment scan alone is not a complete alias/dataflow proof.
- `cfgData.db` (16757 bytes), SHA256
  `5B1A5B5D90C1FDA7CFEDA604457594BB0E1FFA442D61339D94BA5DC919F27D05`.
  Its VAR RETAIN declarations reference 96 typeAnalogCfg, 35 typeDigitalCfg,
  65 typeValveCfg, 10 typeValveSpCfg, four typeMotorSpCfg, two typeMotorCfg
  and 13 typePIDCfg members. There are 20 explicit BEGIN initialization
  assignments, including alarm text-list/index and permission/feature settings.
  Preserve these, retention and S7_SetPoint metadata. They are offline source
  initializers, not a capture of online actual values.

The bundle does **not** define fbControlSfc, fbPIDCtrl or typePIDCfg. Of quoted
declaration type dependencies in these two files, typePIDCfg is the unresolved
one. The older trial contains an expanded PID CFG input, but not the current
PID implementation. The remaining three definitions were requested from the
user. No external files were modified or copied into the public repository.

The table below records the original retained-trial findings; the new source
export now resolves its fbMotS gap. Current controller/PID evidence remains
incomplete until the missing definitions are supplied.

### Completed controller/PID contract evidence

The user subsequently supplied all three remaining definitions:

| Export | SHA256 | Verified contract |
| --- | --- | --- |
| fbControlSfc.scl | 961773010BCA915F735AE89EDC98A1617A8048A35528570C02020F3C2ED6EFA0 | Version 1.0, Input HSS/Reset : Bool, InterfaceInOut : DWord in VAR_IN_OUT |
| fbPIDCtrl.scl | AAB5251B32B94B8B1ED7EB9481341D95F6C1250982F4638CFC89BF1F1C41BAEF | Version 1.0, CFG : typePIDCfg in VAR_INPUT; no direct CFG assignment found |
| typePIDCfg.udt | 0B9CF20BD7623198E68823CABAC7D0554FD81C7E2900412FC80759CFA8D50110 | Version 0.1, split-range instruction metadata/defaults, permissions/features and AlarmBlock defaults |

The three-file evidence gap is closed. All seven device CFG contracts are
Input. Unit CFG direction is therefore Input, preserving the existing read-only
caller contract. These are current offline exports, not online-value captures.
fbControlSfc consumes HSS directly in its protection/held-state conditions;
do not invert it. The operation selector's unassigned Reset still must not be
silently changed to delayed reset. No provided library implementation is edited.

| Called library FB | Retained trial evidence | Configuration | Control pins checked |
| --- | --- | --- | --- |
| fbUnit | A2 instance | None | Inputs ALARM, WARN, OP_REQ, FUNC_TEXT; outputs QALARM, QWARN, QOP_REQ |
| fbControlSfc | sfc_A2 instance, older version | None | Input Reset : Bool; no HSS, MODE_OFF or MODE_SERV input in this snapshot |
| fbMonAn | FI1006 and other instances | Input CFG : typeAnalogCfg | Input RESET : Bool |
| fbMonDi | LS1181 and other instances | Input CFG : typeDigitalCfg | Input RESET : Bool |
| fbVlv2Out | VV1003 and other instances | Input CFG : typeValveCfg | Input RESET, PROT, PROT_EN : Bool |
| fbVlvCtrl | VV1000 and other instances | Input CFG : typeValveSpCfg | Input RESET, PROT, PROT_EN : Bool |
| fbMotS | Not present | Caller declares Input CFG : typeMotorCfg; target definition unverified | Caller declares RESET/PROT; target definition unverified |
| fbMotAn | AG110-M001 instance | Input CFG : typeMotorSpCfg | Input RESET, PROT, PROT_EN : Bool |
| fbPIDCtrl | TIC-1174 and other instances | Input CFG : typePIDCfg | Input RESET : Bool |

All six retained CFG interfaces are Input. This supports Input as the proposed
Unit CFG direction; it does not prove the seventh current target contract or
absence of hidden global writes in device implementations. No mode-pin mapping
may be invented on fbUnit: the checked wrapper has none. The five requested
Unit interface spellings and Plant global operands are specified independently.

## Executable artifact inventory

| Owner / creation path | Current executable content | G-017 responsibility |
| --- | --- | --- |
| XmlPlant.GetUnit | FBD global instance call to fb<Unit>, en and completed-inventory staging bindings | Keep FBD, preserve manual Plant networks, add five controls and optional CFG |
| XmlUnit | fbUnit wrapper/status aggregation, fbIntlck50 preconditions, fbControlSfc selector, device/operation networks | Shared model; FBD and SCL renderers; explicit controls and CFG |
| XmlOperation | fbControlSfc selector, interlocks, phase preconditions/controllers/calls | Explicit HSS/Delayed_Reset, correct selector and phase-controller scope |
| XmlPhase | StructuredText template with control flow and stateful instruction/library calls | Real FBD equivalent and SCL body, not a language attribute substitution |
| XmlInterlock | FBD template or G-014 SCL source | Preserve preflight and existing-content protection under general selection |
| AddTp / AddRp | Standalone fbTPs/fbRPs, globally instanced fbTP/fbRP calls | Selected executable backend; retain HMI.RESET by user instruction |
| Hardware UDTs / dbIO | Declarations from hardware inventory | Keep schemas independent of language; PID contributes no IO |
| Configuration DB | Side-effect registration through XmlGlobal / GetValueFromGlobalDB | Replace implicit ownership collection with explicit inventory/migration planning |

The generator creates no Plant call to standalone fbTPs/fbRPs. Their external
execution/caller ownership must not be replaced by guessed extra calls.
Current Upload imports Plant before many generated callers' prerequisites.
Global modular generation needs an explicit dependency graph and failure gate,
not an extension of that current category loop.

## Consumer and ownership map

| Consumer | Current operand | Planned scope / constraint |
| --- | --- | --- |
| Unit fbControlSfc.Reset | HMI.RESET | Local iReset |
| Unit fbControlSfc.HSS | F_Safe.HSS via G-016 | Local iHSS |
| Base devices RESET | HMI.RESET | Local iReset |
| Valve/motor PROT | F_Safe.HSS | Local iHSS, preserve polarity |
| Extra Analog/Digital monitor RESET | RESET_CYCL global tag | Separate existing cyclic-reset semantics; not one of the five replaced globals, do not silently substitute immediate reset |
| Operation controller HSS | F_Safe.HSS via G-016 | Operation local HSS |
| Phase controller HSS | F_Safe.HSS via G-016 | Operation local HSS; controller resides in Operation FB |
| Phase controller Reset | HMI.RESET_DELAYED | Operation local Delayed_Reset |
| Operation selector Reset | No current generated assignment | Do not newly assign delayed reset without a confirmed behavioral requirement |
| iMode_Off / iMode_Service | No current generated mode consumer found | Add requested Unit interface/binding; do not guess wrapper pins |
| Device CFG | cfgData.<unit>_<exact instance> | Unit local CFG.<exact instance> |
| Standalone Tp/Rp RESET | HMI.RESET | Keep global by explicit user clarification |

Analog and Digital create base configuration plus each configured `_InstanceN`
configuration. The other five families create their one device configuration.
Ordinary/safe `fbIntlck7` helper calls remain with their owning device; no
separate configuration registration was found for those helper calls.
Collect exact names at declaration time; never recover Unit/device ownership
by splitting underscores in a legacy flat name.

## Phase template audit

`EmptyPhase.xml` contains COMMANDS, FUNCTION, S10 - (with trailing spaces),
S20 - (with trailing spaces), MESSAGES, OPERATOR REQUEST, TRANSFER, TIMER and
STATUS regions. Preserve exact labels from the XML rather than this prose.
It contains IF/ELSIF, CASE, incremental step assignment, bit-oriented command
and status accesses, five Program_Alarm messages, fbOpMsg instances,
fbTransfer, fbTimer and an alarm block. It has no GlobalVariable access to
HMI/F_Safe in the checked template. Rendering only its assignments, or changing
ProgrammingLanguage to FBD, would discard control flow and instruction calls.

## Scan-order examples and completed pure planning work

`ModularGenerationPlan.cs` supplies isolated, immutable planning primitives:

- Explicit component/scoped references and the exact five-control/Operation
  forwarding contract; no Portal access or HMI/F_Safe member creation.
- Exact configuration ownership and old/new paths, deterministic first
  declaration order, duplicate/collision checks and copied member metadata,
  including nested fields, comments, versions and offline start values.
- Stable category ordering with rejection of read/write, write/read and
  write/write hazards that would change the original network schedule.
- Deterministic artifact dependency ordering with missing-dependency/cycle/name
  diagnostics, independent of hard-coded Unit/Operation/Plant category order.

For example, moving an Analog producer after a Digital consumer changes a
same-scan read to a previous-scan read. Moving an Analog reader after a Digital
writer changes a previous-scan read to a same-scan read. Both must reject
category grouping unless dependencies are explicitly reconciled. Conflicting
writes must also retain order. Independent networks can group stably.

The planning primitives now feed a supported FB/A/O network adapter and body
renderers described below. They do not yet cover the phase template's complete
control flow, managed-source ownership protocol or live import lifecycle.
Their tests do not establish complete backend parity. The runtime generator does
not use them yet; the prompt remains operation-interlock-only.

## Prepared control routing and read-only configuration inspection

`ModularControlRouting.Prepare` can prepare the five Unit inputs/Plant global
bindings, local Unit SFC HSS/reset, device RESET/PROT, Operation forwarding and
multi-phase controller HSS/delayed reset on the in-memory XML model. It is not
called by runtime generation yet: activation requires completed batch preflight
and backend integration. RESET_CYCL and standalone Tp/Rp global resets retain
their established semantics. No selector Reset behavior is invented.

The G-016 reconciler now delegates to a parameterized Boolean-reference helper.
Its compatibility entry point still uses global F_Safe.HSS. The preparation
pass supplies local references and removes only a replaced operand with no
remaining wire users, avoiding orphan hidden global references. Shared access
nodes and other/manual users remain intact. No HMI/F_Safe DB member is created.

`ConfigurationMigrationPlan.Inspect` derives exact base/monitor ownership from
configured device calls. It takes an **original DB section snapshot**, not the
mutable cfgData cache that existing constructors extend. This distinction keeps
fresh generated declarations from being misidentified as legacy configuration.
It copies existing member metadata/values, rejects source/type/member collisions
and reports separate consumer-review, offline-backup and online-value protection
preconditions. It does not modify the DB or apply/authorize a migration. Existing
nested CFG requires explicit UDT/value reconciliation rather than rebuilding
blindly; managed idempotent regeneration remains outstanding.

## Supported network renderers and configuration artifacts

`GeneratedNetworkModel` adapts the explicitly supported generator FB/A/O grammar
into immutable expressions, scoped symbols, invocations and output assignments.
It is not an arbitrary manual FBD decompiler. Call types must be registered by
the owner. Unknown instructions/pins, dangling or duplicate drivers, unsupported
access forms, cycles and wires outside the supported grammar reject with stable
network identity and original title. FB outputs feed consumers in dependency
order; fan-out and output negation do not execute the FB twice. Unconnected
parameters remain unassigned; InOut requires an unnegated writable symbol.

The FBD renderer retains the validated network snapshot. The readable SCL and
`StructuredTextBodyRenderer` render the same model. Tokenized SCL follows the
working repository StructuredText/v3 representation, allowing enclosing XML
interface metadata, multilingual instance comments and instruction versions to
be retained instead of flattening them into source declarations. Both SCL
representations are compared by an independent fixture token reader. PLC import,
EN-disabled output timing and independent stateful-call ordering still require
Siemens validation; these fixtures alone are not a PLC equivalence proof.

`GeneratedBlockBody` supplies Devices/category/original-title SCL nesting and
FBD structural group headers without moving a device across a non-device scan
boundary. The scheduling guard includes enclosing-instance versus member-field
conflicts, in addition to exact read/write hazards. FBD headers remain separate
from device descriptions on Static declarations.

`ConfigurationArtifacts` serializes exact ordered configuration leaves into
`udtCfg_<SafeUnit>` and expanded cfgData instance overrides, retaining nested
metadata/start values and per-leaf remanence. Its immutable DB proposal retains
all legacy/manual members and rejects an existing target member. Its prepared
binding pass adds Unit Input CFG, connects the Plant to cfgData.<SafeUnit> and
connects actual base/monitor CFG pins to local CFG.<instance>, without implicitly
creating any DB member. Empty configurations emit neither UDT nor CFG member.
Legacy application and managed nested reconciliation remain separate work.

Expanded verification covers actual seven-family device networks, monitors and
helpers in both staging modes/language options and v4/v5 reopened XML. It checks
FBD snapshot preservation, SCL source/token parity, exact category order, typed
CFG binding idempotence and absence of DB side effects. UDT and expanded DB
member interfaces validate against the installed Siemens Interface/v5 XSD.
New standalone command: `powershell -NoProfile -ExecutionPolicy Bypass -File
Tests/Verify-GeneratedNetworkModel.ps1`.

`Tests/Verify-ModularControlRouting.ps1` exercises this pass and configuration
inspection on actual seven-family device Add fixtures plus bare Unit/Operation
models, two phases, both staging/language options, v4/v5 and repeated reloads.
It checks exact scopes, effective and orphan global references, cyclic resets,
no invented selector reset, no global DB mutation, eleven configuration leaves
(seven devices plus four monitors), copied metadata/start values and migration
preconditions/collisions. Runtime activation and backend parity are not tested.

## Remaining implementation gates

1. Core library contract exports are now present and checked. Complete the
   executable statement/dependency audit, including phase stateful helpers and
   EN behavior, without guessing compiler instruction semantics.
2. Extend the supported network model/renderers to complete phase control flow
   and the finalized Add/constructor lifecycle. Complete independent semantic
   parity fixtures. Do not infer
   arbitrary manual code from old XML or mark converted bodies as owned.
3. Integrate the prepared Unit/Operation/Plant routing and configuration inspection
   into the finalized batch lifecycle; render the per-unit UDT/CFG interface.
4. Specify and test managed-content ownership before enabling additive updates.
5. Build read-only cfgData migration plans, including backups, current metadata,
   external consumer inventory and online-value capture requirements. Unknown
   consumers stop application. Do not delete old members or add dual writes.
6. Enable the general selector only with complete backend scope and failure
   gating. Verify supported FBD phase logic and retained stateful call order.

## Validation and acceptance status at this checkpoint

New command: `powershell -NoProfile -ExecutionPolicy Bypass -File Tests/Verify-ModularGenerationPlan.ps1`.
It tests pure planning only, including independent golden control/configuration
inventories and scheduling counterexamples. Run the five existing suites too.
No generator execution or live project changes are part of this checkpoint.

AC-1 through AC-8 are **not complete**. They require the runtime adapters,
complete renderers, migration protections and semantic/backend parity described
above. A passing C# build or planning test does not change that status.

| Acceptance criterion | Checkpoint status | Evidence / remaining scope |
| --- | --- | --- |
| AC-1 General language scope | Not tested | General selector/renderers not implemented or activated |
| AC-2 Equivalent interfaces/routing/CFG | Not tested | XML control preparation verified; CFG interfaces/backends not integrated |
| AC-3 Device grouping/presentation | Not tested | Seven-family grouping and SCL/FBD presentation verified offline; runtime remains |
| AC-4 Executable semantic/backend parity | Not tested | Supported FB/A/O adapter/renderers verified; complete phase/control-flow parity remains |
| AC-5 Complete per-unit CFG generation | Not tested | Seven-family UDT/DB serialization, schema and typed bindings verified; runtime/reconciliation remain |
| AC-6 Protected legacy migration | Not tested | Read-only inventory/precondition/collision checks pass; end-to-end reviewed migration remains |
| AC-7 Empty/PID/prior-feature runtime behavior | Not tested | Prior suites pass; new modular runtime paths not activated |
| AC-8 Complete builds/parity/live validation reporting | Not tested | V21 x64/AnyCPU and all seven offline suites pass; full backend parity absent, V20 APIs absent, live validation not run |
