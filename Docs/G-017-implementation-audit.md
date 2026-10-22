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

These primitives do not yet contain the complete executable statement model,
constructor adapters, source renderers, managed-source ownership protocol,
configuration migration preflight or live import backend. Their tests do not
establish backend parity. The runtime generator does not use them yet, and its
prompt remains operation-interlock-only to avoid advertising unavailable scope.

## Remaining implementation gates

1. Obtain current target FB/type definitions, especially updated fbControlSfc,
   fbPIDCtrl/typePIDCfg and the stateful phase/library dependencies. Check pin directions,
   type/version dependencies, EN behavior and relevant configuration/global
   writes. Caller XML alone cannot prove implementation side effects.
2. Complete the supported semantic statement model and actual Add/constructor
   adapters, then both renderers and independent parity fixtures. Do not infer
   arbitrary manual code from old XML or mark converted bodies as owned.
3. Generalize G-016 endpoint reconciliation for explicit local control operands,
   then integrate Unit/Operation/Plant interfaces and configuration planning.
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
