# G-019 Unit modes and CFG InOut validation

Implementation baseline: main 356ac23, branch g-019-unit-modes-cfg-inout, 2026-10-26. User changes in V21 Project.cs are preserved and excluded. Implementation validation used no agent-run Portal/app execution or external-reference edits. Publication was subsequently authorized by the user.

## Behavior and contract

The shared UnitModePlan uses exact configured main device calls, ordered by their original declaration networks. OFF targets fbVlv2Out, fbVlvCtrl, fbMotS, fbMotAn and fbPIDCtrl. Service targets the first four only, setting CFG.<instance>.FEATURE.IsAutoModeDefault := NOT iMode_Service, then IsInterlockOff := iMode_Service. Sensors, extra monitors/interlock helpers, manual lookalikes and operation/phase controllers are excluded. Unsupported/missing/wrong-type contracts stop preflight with Unit/device/path context.

Both backends execute Devices, MODE Off, MODE Service, then Operations. Devices consume prior mode/configuration values on their current call; the new writes affect their next call. OFF and Service are independent. Both FEATURE flags are owned by cyclic mode logic and overwrite manual/HMI edits every scan, including on leaving Service. Earlier custom values are not saved/restored. The retained unit-cfg-inout-plan.xml explicitly records these destinations and timing/ownership semantics.

The outer Unit CFG is exactly one InOut member of the existing Unit UDT, bound directly to cfgData.<SafeUnit>. Device CFG pins stay Input. A whole-block ownership export must match before an existing Input member can move, with its original metadata/comments/nested overrides intact. Explicit InOut binding rejects duplicate/wrong-type declarations, negation, constants, shared/unsupported wiring and wrong/manual sources before mutation. Existing InOut reruns retain wire/access/cache identity. No temporary CFG copy, copy-back, vendor interface change or new IO type is introduced.

Input-to-InOut upgrade review exports CPU blocks and records the old Unit export hash. Unknown external calls, multi-instances, other Unit instance DBs, ambiguous/manual Plant calls or uninspectable definitions require reconciliation before imports. Only the known exact Plant call is upgraded. Existing instance DBs retain the established backup/import/compile/consistency/publication sequence; no delete/recreate, CPU-wide caller rewrite, ownership weakening or legacy migration-approval bypass is added.

Standalone assignments extend GeneratedNetworkModel with semantic reads/writes. Both renderers use the same ordered plan. FBD produces named/commented networks with Bool coils, source fan-out and inversion in the existing namespace; no synthetic FB call is used. Existing scheduling guards reject hoisting mode writes ahead of their device dependencies. Rendered modes are reconstructed from inventory after restoring verified editable shadows, so owned reopen regenerates one applicable pair. A conflicting source MODE title stops instead of being swept.

SCL preserves G-018 formatting/comments. It uses the allowed separate-OFF-assignment fallback: the current typed assignment model has one destination per statement and no verified chained-token implementation. Reading the same pure iMode_Off for each target preserves the reference chain's behavior and order. Service pairs have one blank separator per device. Empty groups are omitted; PID-only gets OFF without Service, and sensor-only/empty Units get neither. Phase source and fixtures are unchanged.

## Contract evidence

Read-only supplied library_types.scl confirms the four actuator OFF members are InOut Bool, CFG remains Input, and typeValveCfg/typeValveSpCfg/typeMotorCfg/typeMotorSpCfg contain both inline Bool FEATURE fields. fbPIDCtrl.scl confirms its OFF InOut Bool and CFG Input typePIDCfg. ExternalWritable=False is external-access metadata, not a ban on these PLC assignments. The user Unit export and all three screenshots were reviewed; the explicit after-Devices/before-Operations instruction takes precedence over the pictured FBD placement. No proprietary implementations are copied into the repository.

| External source | SHA256 |
| --- | --- |
| library_types.scl | 5840433D57A15BF14FC96D050BF4F9494BF9BF9EBF4B84AEED66C0C5B2605D71 |
| fbPIDCtrl.scl | AAB5251B32B94B8B1ED7EB9481341D95F6C1250982F4638CFC89BF1F1C41BAEF |

Actual preflight re-exports the selected PLC's library definitions and type paths; these saved source contracts do not authorize silently accepting another installed contract.

## Validation

```powershell
& 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/amd64/MSBuild.exe' 'etu-process-generator-TiAv21/etu-process-generator-TiAv21.csproj' /p:Configuration=Debug /p:Platform=x64 /m /v:minimal
& 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/amd64/MSBuild.exe' 'etu-process-generator-TiAv21/etu-process-generator-TiAv21.csproj' /p:Configuration=Debug /p:Platform=AnyCPU /m /v:minimal
powershell -NoProfile -ExecutionPolicy Bypass -File Tests/Verify-UnitModesAndCfgInOut.ps1
```

Run the twelve existing G-017/G-018 suites in separate fresh PowerShell processes. Repeat all thirteen against AnyCPU with -LibraryPath ./etu-process-generator-TiAv21/bin/Debug/TIA_LIB.dll. Check git diff --check excluding the unchanged user Project.cs when necessary. Final run results are recorded below.

The focused suite initially failed against the old Unit CFG Input contract. It now checks actual Add paths for all five OFF/four Service families, sensors/helpers/manual exclusions, empty/sensor-only/PID-only/mixed inventories, cached declarations, quoted names, v4/v5 reloads, both staging modes and both languages. Independent FBD coil/gate decoding and SCL assignment parsing compare sixteen scan states per matrix case, including all truth-table combinations, entering/leaving both modes, next-call consumption and Operations-after-writes state. A copied-Input counterexample demonstrates failure to update persistent configuration. The oracle models aliasing and supplied OFF consumption; it does not execute vendor FB implementations.

Interface fixtures cover actual runtime ownership restoration/capability, prior Input member movement, InOut reruns, nested metadata, duplicate/wrong types, manual Plant operands, idempotent caches and unchanged device Input CFG. FBD mode graphs validate against installed Siemens LAD/FBD schemas in both namespaces. A source golden checks exact mode layout; schedule fixtures reject moving CFG writes ahead of device calls. Caller-policy fixtures reject unknown calls, multi-instance references, instance DBs and ambiguous Plant calls. Existing batch-publication regressions preserve error/consistency gates; the Phase suite's original assertions remain unchanged.

Protected SHA256 values: PhaseTemplateBody.scl = 936E229937C6BCE7D0509B7D656D8F4FC86B8DEB89F34E440FB8D9463684CF45; EmptyPhase.xml = 3C1AF7FA1FF0613552EEEAB0E1A838A0BDAC6EB0B2FC1E4BD52B31CBE0720468. Preserved user Project.cs = 928B54E822255BF3014738C2A79CA9F8016C672998EAD0F05D16A511DE352442.

## Acceptance criteria

| Criterion | Status | Evidence / limit |
| --- | --- | --- |
| AC-1 | Pass (offline) | Exact main target inventory and supplied contracts; runtime preflight validates actual definitions. |
| AC-2 | Pass (offline) | Independent FBD/SCL scan traces, ordering assertions and scheduling hazards. Real PLC timing remains untested. |
| AC-3 | Pass (offline) | One Unit InOut member/direct global alias; device CFG remains Input; upgrade/reload fixtures. Portal aliasing remains untested. |
| AC-4 | Pass (offline) | Persistent alias oracle, copied-Input counterexample and unrelated-value protection. No real online/HMI trial. |
| AC-5 | Pass (offline) | Inventory/cache/reopen/namespace/idempotence/conflict and missing-contract fixtures. |
| AC-6 | Pass (offline) | Runtime ownership restoration, exact member preservation, manual/caller rejection and protected Phase assertions/hashes. |
| AC-7 | Pass (offline) | Both V21 configurations and all thirteen suites pass; V20/Portal limitations remain separate. |

Final results on 2026-10-26: V21 Debug x64 and AnyCPU builds pass with existing warnings. All thirteen suites pass in individual fresh PowerShell processes against both outputs (26 successful suite runs). Logs are retained in the system temporary directory as g019-final-build-<platform>.log and g019-<platform>-<suite>.log. The implementation whitespace check passes; the user Project.cs and protected Phase hashes match the pre-edit baseline. The focused alias guard checks both serialized symbols and cached operands, rejecting a manual source even before reload.

Live gaps: no generator/Portal execution. A later explicitly authorized disposable-project trial must verify both imports/languages, actual Unit/Plant/instance-DB contracts and consistency, all target library members, cyclic cfgData/HMI effects, repeat generation and next-call timing. The standard V20 API installation is absent, so V20 compilation was not attempted. Offline fixtures and V21 compile success do not certify these runtime behaviors.

## First live FBD failure and correction

The user trial saved GenerationErrors/20261026-121919-cfa95e109dbf4580a27a39d281f8c718.log with FBD/GeneratedDbUdt. It stopped during preflight, before artifact imports, reporting Missing writable mode structure: TK003/Q11/CFG.FEATURE.IsAutoModeDefault. Retained run 91549f80f8c6488b8a4cdeff3f030612 contains the actual typeValveCfg backup: FEATURE is a Struct with direct Member children following its AttributeList. The validator wrongly required a Sections wrapper; the Bool field was present.

The correction accepts direct inline Struct members, existing Sections-wrapped fields and referenced UDT fields. It still rejects duplicate/mixed representations, absent members and non-Bool leaves, without searching unrelated descendants or modifying the exports. A compact independent fixture first reproduced the exact failure against the old binary. The corrected validator passes that fixture and a read-only replay against the retained actual fbVlv2Out/typeValveCfg backups. Optional replay command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tests/Verify-UnitModesAndCfgInOut.ps1 -ContractSnapshotDirectory '<absolute path to the retained run directory>'
```

Both V21 Debug x64/AnyCPU builds and all thirteen suites pass again against both outputs (26 successful suite runs). Logs use g019-struct-* in the system temporary directory. The user's Project.cs and Phase hashes remain unchanged. No agent-run Portal mutation or full generation retry was performed; this fixes the demonstrated preflight parser failure, with later live steps still requiring retry.

## FBD Unit import connection failure and correction

The next user trial, GenerationErrors/20261026-122733-18d79bfd34564a45a2c8d526d689d922.log, passed preflight and compiled the operation prerequisites. Unit fbTK003 import then failed in retained run 676cc5902096453bbd1ecb849f43ff5e. TIA rejected compile unit B2 (MODE Off): power rail wire 23 could not accept IdentCon operand 22. The retained XML connected iMode_Off directly to five Coil.in connectors. Positive MODE Service coils used the same invalid shape. This was an import wiring failure, distinct from the previous type-contract parser failure.

RenderAssignments now inserts one single-input AND pass-through per shared positive operand, with its output fanning out to the original coils. Other operand consumers, including existing inversion gates, remain attached. No FB instance, extra call, source reassignment or timing change is introduced. This matches the user's illustrated pass-through gate and avoids the native IdentCon-to-coil signal restriction. The existing interlock-shadow renderer and exact Phase path are unchanged.

A new connection-shape regression failed against the old binary, then passed after the correction. It rejects direct IdentCon/OpenCon coil signals, verifies one shared pass-through and complete positive fan-out, and runs alongside schema checks and independent SCL/FBD multi-scan equivalence. Both V21 outputs are rebuilt and all thirteen suites pass against both outputs (26 runs); logs use g019-coil-* in the system temporary directory. Failed sources/logs and user Project.cs are preserved. Full live Unit import/compile retry remains pending; no agent-run Portal generation was performed.

## MODE Service operand fan-out failure and correction

User trial 20261026-123929-07dae54d9c434312b71be84b5656a7c9 progressed past MODE Off and failed on compile unit B7 (MODE Service) in run c1c376f871c14c4ba4c7db1b928a9faf. Wire 24 shared IdentCon 23 (iMode_Service) across three AND inputs. Wire 26 similarly shared the TRUE literal across two inverter inputs. TIA rejected the former as a power rail containing IdentCon. The earlier correction addressed coil signal inputs, but this raw operand fan-out was still invalid.

The assignment renderer now gives each gate-input connection its own source Access occurrence and point-to-point IdentCon wire, including literal operands. It retains signal fan-out from the shared positive A(1) output and the existing inversion gates. This changes no FB call count, value, assignment order or SCL text. The symbolic inputs are unchanged during these assignments, so the separate references preserve their semantics. Existing device/manual networks and the general interlock-shadow/Phase paths are untouched.

A new operand-wiring regression failed on the previous binary and passes with the correction. Every assignment operand wire must have one IdentCon and one consumer, and operand occurrences are not reused across wires. Existing coil-signal, complete fan-out, schema and independent multi-scan checks also pass. Both V21 configurations rebuilt and all thirteen suites passed against both outputs (26 runs), with logs under system temporary g019-operand-*. User Project.cs, protected Phase files and failed evidence are preserved. Native Unit import/compile after this change still requires a live retry.

## MODE Service power-flow ordering failure and correction

User log 20261026-124520-ab7037ab23344ae9aab708605df56556 (run dc4b5527140d4930beda8f05a1a53b0a) passed the earlier operand wiring rejection and failed on B7 MODE Service: element UId 44 must be sorted according to power flow. The retained source serializes destination coils before their producing gates; the shared positive gate also spans an intervening negative assignment branch. The latter connects Q11 and G11 InterlockOff coils across G11's AutoModeDefault branch.

RenderAssignments now creates independent assignment branches in declaration order and shares a positive gate only for consecutive equal assignments. MODE Off retains one shared gate; the alternating Service pairs have separate positive gates. A dependency walk serializes producers before their dependent coils, keeping fan-out branches contiguous and preserving the original coil/write order. Operand Access occurrences remain separate and point-to-point, and all coil inputs still receive gate signals. No new FB calls, state, SCL changes, or changes to the general interlock-shadow renderer/Phase templates are introduced.

New producer-order, contiguous-branch and consecutive-sharing regressions supplement the existing complete positive-coil coverage, connector, schema and independent multi-scan equivalence checks. The regression first failed against the preceding DLL; all thirteen suites now pass against both rebuilt V21 Debug x64/AnyCPU outputs (26 suite runs). Logs are in system TEMP/g019-order-*. User Project.cs and protected Phase hashes match the baseline; failed logs and generated evidence remain untouched. No agent-run app/Portal generation occurred. Native Unit import/compile after this correction still needs a live retry; offline success does not establish full G-019 runtime acceptance.

## User-confirmed live result and publication

On 2026-10-26 the user confirmed that FBD generation now works after the power-flow ordering correction, and requested commit, push and cleanup. This establishes user-reported successful live FBD generation for the tested project. Detailed exported contracts, SCL live generation, all mode transitions, cyclic alias/timing observations and repeat-generation cases were not independently recorded; the broader validation limits above still apply. The historical pending-retry notes describe the state at each correction, before this confirmation.

G-019 implementation, regressions and documentation are published as one focused commit. The user's current V21 Project.cs configuration is saved in a separate commit without edits. Specs/ and SKILLS.md remain ignored local context; retained GeneratedModels, ownership records and error reports are preserved. The completed task branch is fast-forwarded to main before publication and removed locally after the remote main is verified.
