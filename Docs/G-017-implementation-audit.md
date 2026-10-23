# G-017 implementation and validation audit

Audited 2026-10-23. Baseline `main` at `398aa7f`; work branch
`g-017-modular-generation`. This replaces the preparation-only checkpoint.
The modular runtime is now connected to both app entry points and PlcProject.
No live generator execution, PLC import/download, external file edits or network
access was performed during implementation. The user's V21 Project.cs changes
are retained separately from implementation commits.

## Implemented call path

1. Both Program.cs entry points parse optional migration approval, ask exactly
   `Select language for code generation:` (Enter/FBD or explicit SCL), then ask
   for hardware staging independently.
2. PlcProject freezes configuration, connects through the existing Portal path,
   creates ModularGenerationRuntime, then constructs Plant and the Add models.
   Existing project compilation remains part of the previous constructor flow.
3. Constructors use read-only folder lookup and defer target-folder creation.
   Managed exports restore an editable shadow after checking ownership; manual
   or unowned SCL bodies stop immediately. Unowned FBD may be inventoried but
   cannot be replaced by preflight.
4. CreateTags prepares routing, configuration and rendered artifacts before tag
   mutations. Generated staging first builds its inventory artifacts in memory.
   The batch exports backups, checks names/types/pins/ownership and computes
   the actual dependency graph before any artifact import.
5. Upload rechecks generated artifacts and exported external dependency snapshots,
   then materializes folders. Each datatype/block/instance is imported and
   compiled before dependent callers. Failure retains source/diagnostics and
   prevents all later imports. Existing instance DB exports are reimported to
   retain offline overrides; live growth/retention remains a validation gap.

## Checked library contracts and decisions

All evidence files below were supplied by the user in
`C:/Users/User/Desktop/info`, read only, and not copied into the public repo.

| Export | SHA256 |
| --- | --- |
| library_types.scl | 5840433D57A15BF14FC96D050BF4F9494BF9BF9EBF4B84AEED66C0C5B2605D71 |
| cfgData.db | 5B1A5B5D90C1FDA7CFEDA604457594BB0E1FFA442D61339D94BA5DC919F27D05 |
| fbControlSfc.scl | 961773010BCA915F735AE89EDC98A1617A8048A35528570C02020F3C2ED6EFA0 |
| fbPIDCtrl.scl | AAB5251B32B94B8B1ED7EB9481341D95F6C1250982F4638CFC89BF1F1C41BAEF |
| typePIDCfg.udt | 0B9CF20BD7623198E68823CABAC7D0554FD81C7E2900412FC80759CFA8D50110 |

The earlier fbA2.xml trial hash was
`6716A149AD59BF07E0BDD25C72173AFD330CEC117AD33173DF12EF0B0B71E824`.
It predates HSS and lacks fbMotS; the supplied current definitions resolve these
contract gaps. No library-definition evidence is still outstanding.

| Target | CFG Input type | Control contract used |
| --- | --- | --- |
| fbMonDi | typeDigitalCfg | RESET |
| fbMonAn | typeAnalogCfg | RESET |
| fbVlv2Out | typeValveCfg | RESET, PROT |
| fbVlvCtrl | typeValveSpCfg | RESET, PROT |
| fbMotS | typeMotorCfg | RESET, PROT |
| fbMotAn | typeMotorSpCfg | RESET, PROT |
| fbPIDCtrl | typePIDCfg | RESET |
| fbControlSfc | none | HSS/Reset Input Bool; InterfaceInOut InOut DWord |
| fbUnit | none | ALARM/WARN/OP_REQ/FUNC_TEXT; no mode inputs |

All seven CFG pins are VAR_INPUT; no ordinary direct CFG assignment was found
in the supplied implementations. This establishes the existing caller contract,
not a proof of absence of every possible hidden vendor global write. Unit CFG
is Input accordingly. fbUnit/fbControlSfc expose no mode pins to map; the five
requested Unit controls still exist without invented vendor connections.

The cfgData export contains 225 flat members and 20 explicit offline initializers,
VAR RETAIN and S7_SetPoint metadata. These are not online actual-value captures.

## Interfaces and executable scope

| Consumer | Generated binding |
| --- | --- |
| Plant -> Unit iReset/iReset_Delayed | HMI.RESET / HMI.RESET_DELAYED |
| Plant -> Unit iMode_Off/iMode_Service | HMI.MODE_OFF / HMI.MODE_SERV |
| Plant -> Unit iHSS | F_Safe.HSS |
| Unit controller Reset/HSS | local iReset / iHSS |
| Device RESET / applicable PROT | local iReset / iHSS |
| Unit -> Operation HSS/Delayed_Reset | local iHSS / iReset_Delayed |
| Operation selector HSS | local HSS; Reset stays unassigned |
| Operation's phase controllers HSS/Reset | local HSS / Delayed_Reset |
| Device/monitor CFG | local CFG.<exact-instance> |
| Standalone Tp/Rp RESET | global HMI.RESET, retained by user instruction |
| Extra monitor RESET_CYCL | existing global, separate cyclic-reset semantics |

No HMI/F_Safe DB fallback is created. Phase bodies have no hidden HMI/F_Safe
controls and acquire no unused control parameters. Tp/Rp remain standalone with
external callers; no extra Plant invocation is generated.

Both backends implement Unit/Operation/device/helper logic, phases, operation
interlocks and standalone Tp/Rp. Plant remains FBD and preserves unrelated/manual
networks; vendor FB definitions remain external. Empty/PID-only and both hardware
staging modes use the same controls and configuration rules.

GeneratedNetworkModel adapts only the supported generated FB/A/O grammar, with
explicit pin directions, negation, EN, InOut, output fanout and OR aggregation.
Unknown/manual instructions, duplicate drivers, unconsumed wiring and unsafe
schedules reject with context. GeneratedPhaseModel implements the audited fresh
EmptyPhase-1 body; edited templates/manual phase bodies cannot be auto-converted.
PhaseFbdRenderer emits real FlgNet/v5 networks (including set/reset coils for
conditional writes), with no hidden SCL fallback. Native instruction import
semantics still need live validation.

SCL is tokenized StructuredText/v3 inside SimaticML rather than external-source
FB declarations. This deliberate implementation choice preserves the XML
interface's expanded instruction/version/access/comment metadata. G-014's older
external-source backend remains compatibility code with regression coverage.

Devices group as Digital_Sensors, Analog_Sensors, Digital_Valves, Analog_Valves,
Motors_Direct, Motors_Control, PID_Controllers, retaining first declaration order
within categories. Monitors/Intlck7 helpers stay with their owner. SCL nests exact
original titles in Devices/category regions; FBD keeps exact titles with structural
comments. Reversed read/write dependencies, including enclosing-instance/field
aliases, stop grouping. Stateful call duplication is prohibited. Reordering
independent device instances still requires the stated live timing check.

Both FBD and SCL interlock starters now assign FALSE cyclically to all 50 general
and 50 per-phase preconditions. State Program_Alarm remains declared, not newly
called. These are starter process defaults, not initialization assignments.

## Managed regeneration and migration

Ownership format v1 is whole-block and conservative. After import, successful
compile and export, it records context/name/language/export fingerprint plus an
editable XML model shadow and staging mode. Fingerprints normalize serialization
IDs but retain logic/interfaces/comments/start values. A matching record restores
the owned body and retains the current compiler-expanded interface. Manual edits,
missing/corrupt ownership and language/staging changes stop; no regex SCL merge
or automatic pre-G-017 adoption is enabled. Compatible records permit new generated
devices/phases while preserving declared metadata. PLC re-export stability is not
yet live verified.

Keep `GeneratedModels/<project-CPU-hash>` under the app's working directory across
runs. It contains ledgers and private run backups, generated XML, configuration
plans and diagnostics and is ignored by Git. Changing project path, CPU or working
directory does not silently adopt another ledger.

Per-unit UDT/member proposals merge existing owned configuration and staging types
additively, retaining defaults, metadata and manual fields. Exact configured calls
supply ownership; old flat names are not parsed by splitting underscores. Fresh
constructor-only aliases are removed from the proposal, while original flat
members remain. Fresh empty Units omit CFG/UDT; PID-only Units have PID CFG and no
hardware IO. Type/name/spelling collisions stop before import.

Legacy migration writes `configuration-plan.xml` and an approval template before
imports. The default template has UnknownConsumers=true and all acknowledgments
false. A reviewed approval must match context, original cfgData fingerprint and
all old/new/type mappings, supply external-consumer and online-value evidence,
and acknowledge verified backup, retained flat members and divergence risk.
Invoke with `--configuration-migration-approval <reviewed-approval.xml>` after
review. Unknown consumers/stale plans are rejected. This does not waive the
independent manual-body/ownership checks. HMI/scripts are not automatically
rewritten; old/new values may diverge and there is no dual-write synchronization.
Online actual values must be protected separately; no download is performed.

## Validation on 2026-10-23

Build commands (installed VS 18 MSBuild; actual V21 APIs):

```powershell
$msbuild = 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/amd64/MSBuild.exe'
& $msbuild etu-process-generator-TiAv21/etu-process-generator-TiAv21.csproj /p:Configuration=Debug /p:Platform=x64 /m /v:minimal
& $msbuild etu-process-generator-TiAv21/etu-process-generator-TiAv21.csproj /p:Configuration=Debug /p:Platform=AnyCPU /m /v:minimal
```

Both pass with existing unreachable-code/unused-field/unused-user-variable warnings.
Run each suite with `powershell -NoProfile -ExecutionPolicy Bypass -File`:

| Script under Tests/ | Evidence |
| --- | --- |
| Verify-ModularGenerationPlan.ps1 | ownership/configuration metadata, additive merges, approvals, stale/missing evidence, scheduling hazards and batch failure gates |
| Verify-GeneratedNetworkModel.ps1 | supported FB/Boolean expressions, typed directions, scopes, EN/negation/fanout, Unicode regions and rejection fixtures |
| Verify-GeneratedPhaseModel.ps1 | 4096 scans across 2048 two-scan sequences against independent template oracle and emitted FBD interpreter, call/write traces, timer/alarm arguments, exact labels and installed FlgNet XSD |
| Verify-ManagedArtifactOwnership.ps1 | immutable ledger, identity normalization, metadata restore, manual/corrupt/language protection |
| Verify-ModularControlRouting.ps1 | actual runtime dependency scanner/rendering, seven families plus monitors/ordinary+safe helpers, both languages/modes/v4-v5, local CFG constructor reentry, operand transitions, standalone helpers, empty/PID-only |
| Verify-SfcHss.ps1 | exact endpoints, controller routes, reopening, scope reconciliation, ambiguity/idempotence |
| Verify-DeviceStagingInputs.ps1 | PID no-IO, disabled inputs and edited/added seven-family regeneration |
| Verify-DeviceInstanceComments.ps1 | Static descriptions, helpers, languages/schema and manual content |
| Verify-OperationInterlocks.ps1 | legacy G-014 renderer/backend guards plus general selectors and aligned project source/content lists |
| Verify-V21StagingXml.ps1 | real serializers, V21 metadata and installed interface XSD |

All ten pass against x64 and AnyCPU outputs. AnyCPU uses
`-LibraryPath ./etu-process-generator-TiAv21/bin/Debug/TIA_LIB.dll`.
`git diff --check` passes. Tests bypass Portal constructors and never Upload.
Full local logs are retained in the ignored directory
`TIAOpenness/TIA_Lib/TIA_LIB/obj/G017-verification-20261023-084104`.

Installed local Openness help/API evidence: StructuredText/v3 and FlgNet/v5
schemas, G-014 topics 131792485771/119205656075, and topic 58295603339 confirming
ICompilable on PlcType. Both explicit csproj Compile/Content lists align. V20
standard APIs are not installed. Attempted
`& $msbuild etu-process-generator-TiAv20/etu-process-generator-TiAv20.csproj /p:Configuration=Debug /p:Platform=x64 /m /v:minimal`
exited 1 with CS0246 (Siemens namespaces/assemblies missing); see build-V20.log.
V20 API compatibility remains unverified; V21 build success is not represented
as V20 API validation.

## Acceptance criteria

Pass below means the specified implementation/offline evidence, with live results
reported separately as required by AC-8.

| Criterion | Status | Evidence / limit |
| --- | --- | --- |
| AC-1 general language scope | Pass | Both entry points/runtime wired; real FBD phase rendering; Plant/vendor exceptions |
| AC-2 interfaces and routing | Pass | Integrated both-language/mode/v4-v5 fixtures; supplied library contracts |
| AC-3 grouping | Pass | Seven categories, exact titles, helper ownership and hazard counterexamples |
| AC-4 model/parity | Pass | Independent phase oracle/FBD interpreter, supported network fixtures, exactly-once and rejection checks; PLC timing untested |
| AC-5 CFG inventory/direction | Pass | Seven families/monitors/PID once; Input evidence, immutable/additive metadata/value fixtures |
| AC-6 deployed legacy migration | Not tested | Review/approval/preservation guards tested offline; real external consumers, HMI and online values not exercised |
| AC-7 empty/PID/regressions | Pass | Ten offline suites, explicit G-016 routing supersession |
| AC-8 V21/offline and gap reporting | Pass | Two V21 configurations pass; V20/live gaps stated |

## Remaining live verification

Use an explicitly authorized disposable project copy for representative FBD/SCL
imports, exact exported interfaces, native instruction semantics, EN-disabled
output timing, staged IO, HSS/reset propagation and repeat generation with added
devices/phases. Check instance DB start-value retention on interface growth,
post-compile export fingerprint stability and manual-edit rejection. Exercise a
backed-up legacy migration with known HMI/PLC/script references and separately
protected online values. No production conversion or download is authorized by
this implementation work. These are unverified runtime results, not missing
source wiring or an unresolved library-export request.
