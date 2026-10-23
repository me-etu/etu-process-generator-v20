# G-018 implementation and validation

Generated non-Phase SCL now follows the reviewed Unit screenshots and edited Operation/interlock exports: four-space nesting, aligned multiline arguments, operator spacing, blank separators, purpose comments, Unit Operations grouping and empty 17-asterisk phase separators. Existing comment text takes precedence; device descriptions supplement their unchanged Static comments. Rendering rejects noncontiguous operation groups without rescheduling them.

Only closed Bool literal/NOT/AND/OR enable trees can remove an invocation's SCL IF wrapper. Unknown symbols, ENO, false conditions and comparisons retain gating. Parameter expressions and values, call order and following output assignments are unchanged. FBD shadows and the Phase template bypass the style transformation. Prior block ledger v1 remains supported: verify the exact recorded export, restore the recorded editable body, render the new style and publish ownership only after successful compile/export. Manual comment/body/interface changes continue to stop regeneration.

Actual planned Unit UDTs target PLC data types/<SafeUnit> for either language. Runtime uses that folder for preflight, rechecks, import, compile, publication/export and ownership. CPU-global names and normalized Unit/folder collisions are checked. Type ledger v2 records project/CPU identity, path and exported-content fingerprint. Correct unchanged nested reruns pass policy checks; wrong-folder, unowned or modified types stop. Empty/PID-only inventories do not invent IO types; configuration-only types retain their Unit owner.

Existing root types stop with type-folder-plan.xml and backups before replacement. No safe move method exists in the inspected installed V21 public PlcType, PlcTypeComposition, PlcTypeUserGroup or PlcTypeUserGroupComposition metadata. Group composition exposes Create/Find and Types exposes Import/Find; none exposes Move/Add/parent reassignment. Installed help topics 58380970379 (Importing user data type), 87242587915 (Exporting user data type) and 58295603339 (compilation) document the operations used. There is no delete/recreate fallback or implicit adoption after a manual move. Prior type v1 records also lack the content fingerprint needed for a verified migration. This is the explicitly permitted conservative fallback in FR17.

The external styling references were inspected read-only. Runtime and tests have no Desktop dependency. Compact repo goldens cover a Unit, a two-phase Operation and a complete interlock starter; they deliberately use fixture identifiers and retain the reference comments/structure without copying vendor implementations.

## Offline verification

Build both V21 configurations:

```powershell
& 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/amd64/MSBuild.exe' 'etu-process-generator-TiAv21/etu-process-generator-TiAv21.csproj' /p:Configuration=Debug /p:Platform=x64 /m /v:minimal
& 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/amd64/MSBuild.exe' 'etu-process-generator-TiAv21/etu-process-generator-TiAv21.csproj' /p:Configuration=Debug /p:Platform=AnyCPU /m /v:minimal
powershell -NoProfile -ExecutionPolicy Bypass -File Tests/Verify-SclStyleAndTypeFolders.ps1
```

Run each of the eleven suites listed in Docs/G-017-implementation-audit.md in a separate PowerShell process. Repeat all twelve with -LibraryPath ./etu-process-generator-TiAv21/bin/Debug/TIA_LIB.dll for AnyCPU. Check git diff --check. Existing compiler warnings are retained; no dependency/source-list changes were needed.

The focused suite first failed on the old renderer's constant-TRUE IF wrapper. Its final fixtures cover reference goldens, zero/one/many arguments, quoted names, Output parameters, nested genuine conditions, following assignments, deterministic renderer reuse, multilingual existing-purpose precedence, all seven device families, 0/1/3 monitors/ordinary helpers and 0/2 safe helpers, both staging modes/languages and selected cultures. An independent gate evaluator compares repeated-scan call state/output counters before and after enable simplification; vendor FB execution remains outside this oracle. It checks actual runtime rendering, description ownership, exact FBD/shadow preservation and helper flags. Folder fixtures cover multiple Units, normalization collisions, unknown owners, v2 round trips, changed values, project/CPU mismatch, missing ownership, wrong folders, v1 root reconciliation and empty inventories. Existing regression suites retain PID-only/configuration/dependency/Phase/manual-body checks.

Protected SHA256 values:

| File | SHA256 |
| --- | --- |
| Tests/Fixtures/PhaseTemplateBody.scl | 936E229937C6BCE7D0509B7D656D8F4FC86B8DEB89F34E440FB8D9463684CF45 |
| TIAOpenness/TIA_Lib/TIA_LIB/Xml/EmptyPhase.xml | 3C1AF7FA1FF0613552EEEAB0E1A838A0BDAC6EB0B2FC1E4BD52B31CBE0720468 |

## Acceptance status

| Criterion | Status | Evidence and limit |
| --- | --- | --- |
| AC-1 | Pass (offline) | Reference goldens, actual Unit rendering, comments/argument fixtures. Portal source-export whitespace remains untested. |
| AC-2 | Pass (offline) | Operations parent and two-phase separator golden; split operation groups rejected. Compiler acceptance of separator labels remains untested. |
| AC-3 | Pass (offline) | Closed-tree proof, real/unknown/false/comparison/ENO counterexamples, repeated-scan gate oracle and helper matrix. No claim of vendor PLC runtime equivalence. |
| AC-4 | Pass | Protected hashes, exact Phase runtime/token fixtures and unchanged device FBD/shadows. |
| AC-5 | Not tested end to end | Nested runtime path compiled and pure policy tested. Existing-root migration safely stops under FR17; actual nested import/placement/repeat compilation needs Portal. |
| AC-6 | Pass (offline) | Prior v1 block shadow restore/new-style publication and manual edit rejection; v2 type folder/content guards. |
| AC-7 | Pass (offline) | Both V21 builds and all twelve suites pass against both outputs. Live UI/import/move validation and V20 remain separate. |

Final results on 2026-10-23: V21 Debug x64 and AnyCPU compile successfully with existing warnings. All twelve suites above pass in fresh individual PowerShell processes against each output (24 successful suite runs). git diff --check passes. The protected Phase hashes match the pre-edit baseline. The user's modified V21 Project.cs remains excluded from these edits, with SHA256 F50E396BFB9261B7683509050C62D8325431028D28D9F0D166DED62FFB5873BF. Build/suite logs are retained under the system temporary directory as g018-final-build-<platform>.log and g018-<platform>-<suite>.log.

No generator/Portal execution, external-reference writes or network operations are part of this implementation. V20 compilation requires the absent standard V20 API installation. A later explicitly authorized disposable-project trial must confirm editor formatting/export, separator-label acceptance, nested type import/compile and protected repeat generation. Existing-root movement remains unsupported and is not claimed tested.

## Publication checkpoint

The user subsequently reported that G-018 works and authorized commit, push and cleanup. This is a successful user-reported generation trial; it does not separately establish every UI/export/migration/repeat-generation case above. No further Portal execution was performed by the agent.

The current V21 Project.cs selects the Hydrogenator 410 configuration and retains the former TK003 example as comments. It is saved unchanged in a separate configuration commit (SHA256 F5DA6C26D34C2048A3DF0B08F51D0BAECFCBC5CFD03D0D0CACDD332867C904B4). Both V21 Debug x64 and AnyCPU builds pass with this definition. Its existing trailing whitespace is preserved; G-018 implementation files pass the whitespace check. Remote synchronization/publication is authorized for this checkpoint; the earlier implementation used no network.
