# G-017 implementation and validation audit

Audited 2026-10-23. Baseline `main` at `398aa7f`; work branch
`g-017-modular-generation`. This replaces the preparation-only checkpoint.
The modular runtime is now connected to both app entry points and PlcProject.
The user confirmed successful FBD generation after the PLC-software compilation
fallback, following successful SCL trials. Both V21 configurations build and all
eleven offline suites pass when each suite runs in a fresh PowerShell process.
This confirms the reported generation trial; it does not certify production
migration, online values, PLC timing or V20 APIs. Earlier failed-trial notes below
remain as the diagnostic history and are superseded by this confirmation.
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
   prevents all later imports. FBs with planned global instances first validate
   their definition, prepare/compile those instance DBs, then verify final
   consistency and capture ownership before dependent callers. Prepared instances
   are not reimported a second time at their later graph position. Existing
   instance DB exports preserve offline overrides; live growth/retention remains
   a validation gap.

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

Both backends implement Unit/Operation/device/helper logic and standalone Tp/Rp.
By explicit user amendments on 2026-10-23, phase FBs retain the original SCL
template and operation interlocks retain their SCL regions/comments in both modes.
Plant remains FBD and preserves unrelated/
manual networks; vendor FB definitions remain external. Empty/PID-only and both
hardware staging modes use the same controls and configuration rules.

GeneratedNetworkModel adapts only the supported generated FB/A/O grammar, with
explicit pin directions, negation, EN, InOut, output fanout and OR aggregation.
Unknown/manual instructions, duplicate drivers, unconsumed wiring and unsafe
schedules reject with context. GeneratedPhaseModel now preserves the audited
EmptyPhase source intact; it no longer substitutes a flattened executable phase
model. Full IF/ELSIF/CASE structure, comments, disabled step transition, Action/Next
and transfer/status placeholders are part of the contract. Edited/manual phase
bodies remain protected. PhaseFbdRenderer is retained only for editable ownership
shadows/compatibility fixtures; published phases and interlocks are SCL.

SCL is tokenized StructuredText/v3 inside SimaticML rather than external-source
FB declarations. This deliberate implementation choice preserves the XML
interface's expanded instruction/version/access/comment metadata. G-014's older
external-source backend remains compatibility code with regression coverage.

Devices group as Digital_Sensors, Analog_Sensors, Digital_Valves, Analog_Valves,
Motors_Direct, Motors_Control, PID_Controllers, retaining first declaration order
within categories. Monitors/Intlck7 helpers stay with their owner. SCL nests exact
original titles in Devices/category regions; FBD keeps exact device titles without
generated group comments. Exact previous ETU headers are removed while preserving
all other comment text. Reversed read/write dependencies, including enclosing-instance/field
aliases, stop grouping. Stateful call duplication is prohibited. Reordering
independent device instances still requires the stated live timing check.

SCL interlock starters assign FALSE cyclically to all 50 general and 50 per-phase
preconditions in either selected mode. State Program_Alarm remains declared, not newly
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

Keep `GeneratedModels` under the app's working directory across runs. Ownership
ledgers remain in `<project-CPU-hash>`; private backups, generated XML, configuration
plans and diagnostics use `Runs/<unique-run-id>` and the complete tree is ignored
by Git. Changing project path, CPU or working
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
| Verify-GeneratedPhaseModel.ps1 | supplied legacy SCL golden including comments/placeholders, exact source/interface preservation, actual runtime rendering and SCL ownership round trips in both selected languages/staging modes; manual edit protection and interlock backend regression |
| Verify-ManagedArtifactOwnership.ps1 | immutable ledger, identity normalization, metadata restore, manual/corrupt/language protection |
| Verify-ModularControlRouting.ps1 | actual runtime dependency scanner/rendering, seven families plus monitors/ordinary+safe helpers, both languages/modes/v4-v5, local CFG constructor reentry, operand transitions, standalone helpers, empty/PID-only |
| Verify-SfcHss.ps1 | exact endpoints, controller routes, reopening, scope reconciliation, ambiguity/idempotence |
| Verify-DeviceStagingInputs.ps1 | PID no-IO, disabled inputs and edited/added seven-family regeneration |
| Verify-DeviceInstanceComments.ps1 | Static descriptions, helpers, languages/schema and manual content |
| Verify-OperationInterlocks.ps1 | legacy G-014 renderer/backend guards plus general selectors and aligned project source/content lists |
| Verify-V21StagingXml.ps1 | real serializers, V21 metadata and installed interface XSD |
| Verify-GenerationExecutionLog.ps1 | complete app entry-point wrappers, nested error/console capture, nonzero status, unique logs and fallback without Portal |

The ten original G-017 suites pass against x64 and AnyCPU outputs. The execution-log
suite also passes against both outputs after its later integration, alongside
the entry-point/project-list regression suite. AnyCPU uses
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

Runtime follow-up: the user's first attempt stopped exporting TEMP tags because
the former nested backup path was 261 characters in the normal bin/Debug location.
Run folders now sit directly under GeneratedModels/Runs, while ownership paths
remain unchanged. The longest hashed artifact path at that location is 231
characters. Both ledger and maximum artifact lengths are checked against classic
Windows MAX_PATH before writes/imports. Both V21 builds and the control-routing,
ownership and planning suites pass on both outputs after this fix. Actual Siemens
export retry has not been executed during the fix.

The next user run reached library validation and exposed an overly strict text
comparison: its retained fbMonAn XML declares UNIT as String[10], whereas the
existing CallInfo input uses the String family marker. Preflight now accepts that
input representation against valid bounded String declarations, retaining strict
explicit bounds, String/WString and Output/InOut distinctions. Diagnostics show
both types. Offline contract fixtures and both V21 builds pass; live import is
still unverified.

Subsequent execution evidence: the user reported successful SCL generation.
The latest FBD execution report, `20261023-073108-abb8cf720cd2423e8b1f6b7465fffd82.log`,
identifies an invalid TemplateValue named Type on the first phase's MOVE part.
Run `b5372996c3dd4f9eb8bc56b2262885d9` diagnostics show successful UDT/configuration/
dbIO imports and compilation, then failure at OP6_Pressure_PH_Vent before callers.
MOVE now emits only its Card template; operand connections retain their types.
The general XSD permits arbitrary template names and did not catch this defect.
Both rebuilt V21 outputs pass instruction-specific checks and the independent
4096-scan phase comparisons. Successful FBD import after this fix is not yet
verified; user-reported SCL success does not establish every live acceptance case.

Next FBD report `20261023-074148-c3e34218096c4399b290bac1ba8439d8.log`
(run `7fde7295e48141399f16bfa2a0fc8985`) passed the former MOVE error and
rejected negation on wire UId39, CompileUnit5D: an OR output fed an inverted AND
input in the TRANSFER_READY guard. The phase backend now pushes NOT through
Boolean expressions and keeps inversions only on direct operands. Standalone
negated coil/call/enable inputs use a Boolean gate with a direct inverted operand.
The instruction-placement regression and independent 4096-scan comparison pass
against both rebuilt V21 outputs; post-fix live import is still unverified.

## Phase template correction — explicit user amendment

The user supplied phase_template.scl (SHA256
A3D715DD355E7C0D11520E6DF78CB35789B47112C8C1A4DF23D021A0EE91E401)
and five screenshots in Desktop/info, then selected **Keep the phase template in
SCL in both modes**. Those files were read only. The body matches the repository's
original EmptyPhase token stream, including comments/placeholders.

The prior scan oracle could show equivalent starter execution while missing the
loss of editable IF/CASE scaffolding and unreachable placeholders. That was an
implementation defect: template fidelity is a separate requirement. The flattened
phase model and its FBD execution path have been removed from runtime generation.
Earlier FBD phase import attempts/fixes above are historical and superseded.

Phases now retain the original compile unit and SCL body, including source tokens,
comments and formatting. Actual phase ownership language is SCL in Editable,
preflight and post-compile Capture, independently of the selected backend. The
run context/plan and prompt explicitly identify this exception. Already managed,
unchanged SCL phases restore their original editable shadow; manual/unowned or
incompatible FBD bodies still require explicit reconciliation.

Tests/Fixtures/PhaseTemplateBody.scl is an independent golden extracted from the
user's reference body, with no private Unit/block identity. The actual runtime
renderer is exercised in both selected modes and staging modes with exact source
and interface comparison, source immutability and SCL ownership round trips.
No agent-driven Portal execution, import or user Project.cs edit was performed.
After this correction, V21 Debug x64 and AnyCPU builds and all eleven offline
suites pass against both rebuilt outputs (22 suite executions).

Further user amendments: interlocks now also remain SCL in both modes, including
the Function comments shown in the reference, General Interlocks and each phase
region. Only unchanged, recorded generator-owned FBD interlocks may be converted
under this explicit request; ownership/fingerprint checks reject manual edits.
The prompt now reads Select signal IOs generation style, and the marked default,
experimental and phase-template annotations are removed. New FBD group comments
are omitted, with exact old headers cleaned while retaining unrelated comments.

Report `20261023-083717-b98badcfad3b412cb7a15ed6b82d8608.log` identifies the later
failure: fbTK003 compiled with zero errors but its ownership export was rejected
as inconsistent. Runtime compilation now checks actual PlcBlock.IsConsistent
(confirmed in installed V21 API metadata), allows one extra compile pass for
updated FBD calls, and stops before export/callers if still inconsistent. Real
compiler errors are not retried or suppressed. Offline tests verify the bounded
gate; a live retry is still needed to establish whether this clears the reported
TIA inconsistency. Both V21 builds and all eleven suites pass after these updates.

The next user FBD run, logged in
`20261023-083256-04fbdfc32dc744cb8ba753c41a52a760.log`, successfully imported and
compiled all seven preserved SCL phases. It then stopped at the first interlock,
CompileUnit F: its empty Function heading was serialized as FlgNet/Parts with no
logic. Empty presentation networks now use the repository's
EmptyNetworkWithOutCall form (empty NetworkSource), retaining the heading and all
executable precondition networks. Both rebuilt outputs pass the exact blank-source
fixture and phase/interlock/routing suites. Post-fix live import remains unverified.

## Acceptance criteria

Latest execution follow-up: report
`20261023-085757-1a3feaf9f5104d1eb850e17fd1f1a622.log` and run
`99c556b846624e4c81e6e1d95ec244ba` show that two FB compile passes did not resolve
the Unit flag: the second reports all blocks up-to-date with IsConsistent=false.
The plan stopped before instance/TK003 and Plant. The retained Plant XML already
contains exactly one global fbTK003/TK003 call, so the missing call is a result of
aborted publication, not call omission in the model.

Publication now treats a generated FB and its planned global instance DBs as a
pair: compile the definition for real errors, prepare/compile companion instances,
require final FB/DB consistency, then export ownership and allow callers. Existing
instance backups preserve offline values, completed instances are not imported
twice, and unexpected new DBs without backup stop. The plan records
PrepareBeforeOwnershipExport. Pure executor tests cover this order, definition/
instance failures and no Plant import after failure. This changes lifecycle order
rather than adding more blind compile retries. Successful live resolution is
still unverified; no unambiguous Portal process was available for read-only
inspection, so no attachment/import/compile was performed by the agent.

Interactive failures now retain the console with a key prompt after printing the
log path; redirected execution skips it and still exits with status 1. Both V21
builds and all eleven offline suites pass against both outputs after this change.

Pass below means the specified implementation/offline evidence, with live results
reported separately as required by AC-8. AC-1/AC-4 use the user's later explicit
amendment: keep the original phase SCL template in both selected languages.

| Criterion | Status | Evidence / limit |
| --- | --- | --- |
| AC-1 general language scope | Pass | Both entry points/runtime wired; original SCL phases in both modes by user amendment; Plant/vendor exceptions |
| AC-2 interfaces and routing | Pass | Integrated both-language/mode/v4-v5 fixtures; supplied library contracts |
| AC-3 grouping | Pass | Seven categories, exact titles, helper ownership and hazard counterexamples |
| AC-4 model/parity | Pass | Original phase source preserved exactly against supplied legacy reference; supported network fixtures and rejection checks; PLC timing untested |
| AC-5 CFG inventory/direction | Pass | Seven families/monitors/PID once; Input evidence, immutable/additive metadata/value fixtures |
| AC-6 deployed legacy migration | Not tested | Review/approval/preservation guards tested offline; real external consumers, HMI and online values not exercised |
| AC-7 empty/PID/regressions | Pass | Eleven offline suites, explicit G-016 routing supersession |
| AC-8 V21/offline and gap reporting | Pass | Two V21 configurations pass; V20/live gaps stated |

## Remaining live verification

The user has now confirmed that FBD generation completes successfully with the
PLC-software compile fallback. The historical failures below are resolved for
that trial. Broader migration/runtime/instance-value tests listed at the end of
this section remain outstanding.

Run `a3a3ea3e85f143e6861433ff75d14c52` confirms the same zero-error
failure through a fresh target lookup and native XML export refusal. The Unit's
compile timestamp advances on the first block compile; subsequent block compiles
remain up to date without clearing the consistency flag. The companion DB is
consistent. The user reports that compiling the project after closing the
generator succeeds. Runtime now performs one PLC-software compile when both
block-only passes succeed but leave the block inconsistent. It retains the
software compiler messages, stops on software errors, and still requires the
actual block consistency flag before export/publication. This is a bounded
dependency reconciliation fallback, not a demonstrated root-cause correction.
Both V21 builds and targeted planning/template checks pass. Live recovery is
still unverified; a software compile may also report errors in other PLC blocks.

The subsequent FBD trial (run `1490c866715946d9941af5aba93cfa94`)
still stops at `fbTK003`: its definition compiles with zero errors, its
companion `TK003` DB compiles consistently, and two further FB compiles report
all blocks up to date while `IsConsistent` remains false. Preparing the instance
DB did not resolve the reported failure. The user also reports that the FB looks
normal and compiles successfully in the editor. Plant publication remains
blocked by the consistency/export gate; the retained Plant model contains the
expected Unit call. A current FBD XML export after manual compilation has been
requested to compare against the retained generator input. The GUI source export
does not support FBD, so manual export is no longer required: runtime diagnostics
now record compile/modification timestamps before and after compilation, re-read
the failed target and companion states through fresh lookups, and attempt an XML
export through Openness. An export refusal is retained without masking the
original failure or bypassing the publication gate. Both V21 builds and targeted
planning/execution-logging checks pass; live capture remains unverified. The two degree-sign
string warnings are not established as the cause. Do not treat the offline
lifecycle tests above as proof that this live failure is fixed.

Use an explicitly authorized disposable project copy for representative FBD/SCL
imports, exact exported interfaces, native instruction semantics, EN-disabled
output timing, staged IO, HSS/reset propagation and repeat generation with added
devices/phases. Check instance DB start-value retention on interface growth,
post-compile export fingerprint stability and manual-edit rejection. Exercise a
backed-up legacy migration with known HMI/PLC/script references and separately
protected online values. No production conversion or download is authorized by
this implementation work. These are unverified runtime results, not missing
source wiring or an unresolved library-export request.
