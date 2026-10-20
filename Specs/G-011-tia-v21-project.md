# G-011 Separate TIA Portal V21 Generator Project

## Summary

On the user-selected `upgrade-to-TIA-V21` branch, add `etu-process-generator-TiAv21/` and `etu-process-generator-TiAv21.sln`. Build the new app and a separate build of repo-local `TIA_LIB` against the installed TIA Portal V21 .NET Framework APIs. Preserve the existing V20 app, solution, API references, and output directories.

Status: implementation/build milestone completed 2026-10-20. V21 runtime compatibility remains untested. Both app versions remain available while sharing library source.

## Problem

The current app and library target .NET Framework 4.8 but reference `Siemens.Engineering.dll` and `Siemens.Engineering.Hmi.dll` under `Portal V20/PublicAPI/V20`. Their reference identities still say `Version=18.0.0.0` with `SpecificVersion=False`.

The local installation has Portal V21 and a different assembly layout. Changing `V20` to `V21` in the existing hint paths would point to files that are absent from the inspected V21 API directory. Repointing the one existing library project would also change the dependency used by the V20 app.

## Goals

- Provide a distinctly named V21 app folder, executable, namespace, and solution.
- Resolve V21 Siemens assemblies consistently in the app and library.
- Share repo-local library implementation without mixing V20 and V21 build artifacts.
- Preserve the current generator definitions, staging modes, and PLC/HMI behavior.
- Establish compile validation and clearly separate it from later TIA runtime validation.

## Non-Goals

- Renaming or deleting the V20 app or migrating the repository directory itself.
- Migrating to modern .NET, changing NuGet dependencies, or redesigning the generator DSL.
- Duplicating the entire library source tree or editing `C:/TIAOpenness/TIA_Lib/TIA_LIB`.
- Installing Siemens software, modifying machine configuration, or committing Siemens binaries.
- Running the generator, upgrading a TIA project, or altering PLC/HMI content during implementation validation.
- Enabling SiVArc generation or changing active unit/device definitions as part of this migration.

## Current Behavior

- V20 solution: `etu-process-generator-TiAv20.sln`.
- V20 app project: `etu-process-generator-TiAv20/etu-process-generator-TiAv20.csproj`.
- Namespace and assembly: `EtuProcessGeneratorTiaV20` and `etu-process-generator-TiAv20`.
- App sources: `Program.cs`, `Project.cs`, and `Properties/AssemblyInfo.cs`; configuration: `App.config` and `packages.config`.
- Library project: `TIAOpenness/TIA_Lib/TIA_LIB/TIA_LIB.csproj`, referenced directly by the app.
- Both projects use .NET Framework 4.8; x64 configurations specify C# 7.3.
- `Program.Main` selects marker memory by default or optional generated DB/UDT staging, deletes its output `Plant.xml`, then constructs `Project`.
- `PlcProject` construction connects to TIA and compiles PLC software. `SiemensPortal` enumerates processes and attaches, or starts Portal if none was attached. This is side-effecting.
- Active `Project.cs` scope is `BV260-B210`; other unit definitions are inside `#if false`. `GenerateSiVarc()` is commented out. Preserve this exact selection in the initial V21 copy.
- Library code uses PLC, Unified HMI, and SiVArc namespaces. `PlcProject.cs` also imports `Siemens.Engineering.Hmi.Communication`; determine whether its classic WinCC dependency is needed during compilation.
- Library XML templates are copied via project content items. Some code uses the current working directory, so output isolation alone does not guarantee runtime file isolation.
- README and knowledge-base lifecycle examples contain older scope/SiVArc statements; use current source as migration baseline.

## References

Repository guidance and evidence:

- `AGENTS.md` instructions supplied in this session; `README.md`; `CHANGELOG.md`.
- `GENERATOR_KNOWLEDGE_BASE.md`, especially Repository And Runtime Architecture, Execution Lifecycle And Side Effects, and troubleshooting for XML/template failures.
- `Specs/G-010-prune-unsupported-multilingual-cultures.md` for preservation and compile-only conventions; do not repeat its implementation.
- Both current `.csproj` files, the V20 `.sln`, app entry point/configuration/assembly metadata, and library `SiemensPortal.cs` and `PlcProject.cs`.

Local Siemens evidence inspected read-only on 2026-10-20:

- API root: `C:/Program Files/Siemens/Automation/Portal V21/PublicAPI/V21/net48`.
- Assembly metadata: inspected DLLs report version `21.0.0.0`, public key token `29bfe5fdf4ba5d3b`.
- `Siemens.Engineering.Base.dll` and its XML documentation expose core engineering/compiler types.
- `Siemens.Engineering.Step7.xml` contains `Siemens.Engineering.SW.PlcSoftware`.
- `Siemens.Engineering.WinCCUnified.xml` contains `Siemens.Engineering.HmiUnified.HmiSoftware`.
- `Siemens.Engineering.Sivarc.xml` contains `Siemens.Engineering.SiVArc.Sivarc`.
- `Siemens.Engineering.WinCC.xml` contains classic `Siemens.Engineering.Hmi.Communication` types.
- Old `Siemens.Engineering.dll` and `Siemens.Engineering.Hmi.dll` names are absent from that directory.

These are local installation observations, not proof that every existing API call or XML template is compatible. No internet was used. Use installed XML documentation and compiler diagnostics to resolve remaining API differences.

## Proposed Architecture

### Versioned app and solution

Create a new folder containing only the six tracked app source/configuration files listed above, with the project renamed to `etu-process-generator-TiAv21.csproj`. Do not copy `bin`, `obj`, IDE state, generated XML, publish artifacts, or local user settings.

Set namespace to `EtuProcessGeneratorTiaV21`, assembly/title/product to `etu-process-generator-TiAv21`, and assign a new project GUID and app typelib GUID. Preserve assembly version policy and .NET Framework 4.8. Copy the current V20 plant definition, including comments and disabled regions, changing only its namespace unless a compiler-proven API adaptation is needed.

Create `etu-process-generator-TiAv21.sln` containing the V21 app and V21 library project, with a new solution GUID and correct project configuration mappings. `Debug|x64` is the supported validation target. Keep the V20 solution untouched.

### Versioned library project, shared source

Add `TIAOpenness/TIA_Lib/TIA_LIB/TIA_LIB.V21.csproj` beside `TIA_LIB.csproj`, with a new project GUID. Use the same source files and existing library assembly metadata, namespace `TIA_LIB`, and assembly name `TIA_LIB`. Reference it from the V21 app by project path/GUID; never reference a built DLL directly.

For the initial small change, mirror the existing explicit Compile/Content/None lists in the V21 project. This duplicates project item declarations, not implementation files. Keep those lists aligned when source/templates change; avoid introducing a general build-system refactor in this task.

Set V21 library `BaseIntermediateOutputPath` early, before `Microsoft.Common.props`, to `obj/V21/`, with configuration/platform-specific intermediate directories. Use `bin/V21/<Platform>/<Configuration>/` for library output. Ensure assembly-reference caches, generated framework attributes, and all other intermediate artifacts cannot overlap the V20 project. Preserve content-copy metadata so the app output receives all currently declared XML templates.

Keep app outputs within its own V21 folder. Document a runtime working directory inside the V21 app output because library exports use relative/current-directory paths. Do not rewrite global file handling without a demonstrated migration defect.

### V21 Siemens references

Use a V21-only MSBuild property such as `TiaV21ApiDirectory`, defaulting to `$(ProgramFiles)/Siemens/Automation/Portal V21/PublicAPI/V21/net48`, in both V21 projects. Permit a build-time override for installations elsewhere.

Initial reference candidates are Base, Step7, WinCCUnified, and Sivarc. Add WinCC only where existing classic HMI imports/types require it. Confirm any further transitive requirements from local metadata and build diagnostics; do not reference every installed API assembly indiscriminately.

Use actual V21 assembly names and identities, including the changed public key token, or simple assembly-name references with explicit verified hint paths. Do not carry the V18 reference identities or old DLL names into V21. Missing V21 API files must produce a clear build failure without fallback to V20 or cached older assemblies.

Validate runtime resolution separately from compile resolution. Inspect installed Siemens guidance/dependencies before deciding Copy Local and any runtime resolver. Do not assume that V20 binding redirects or a version-number substitution can bridge renamed assemblies with different signing identities. Never construct `PlcProject` to test resolution. Add a resolver only if local evidence requires it, before the first Siemens type is loaded, and only for the V21 app.

### Source/API compatibility

First compile the existing shared source against V21. Adapt only calls proven incompatible by compiler diagnostics and installed API documentation. Prefer source compatible with both versions; if unavoidable, use narrowly scoped `TIA_V21` conditional compilation in the shared files, defined only by the V21 library project. Document each adaptation and why it is needed. Ask the user before substantial generator behavior changes.

No blanket replacement of XML schema versions, namespaces, library type names, or SiVArc configuration. Compilation cannot validate import schemas or runtime HMI semantics.

## FRs

- FR-1: Add the separate V21 app, project, solution, namespace, executable identity, and distinct GUIDs.
- FR-2: Preserve the V20 project/solution files and their V20 reference configuration.
- FR-3: Build the V21 app through a project reference to the new repo-local V21 library project.
- FR-4: Resolve all Siemens compile references from the selected V21 API directory with no V20 fallback.
- FR-5: Isolate V21 intermediate/output files and preserve existing template-copy behavior.
- FR-6: Preserve `BV260-B210`, disabled unit regions, staging selection/default, and current SiVArc enablement state.
- FR-7: Preserve additive Plant behavior, supported-language pruning, generated DB/UDT staging, comments, and HMI detection. Adapt incompatible APIs only with evidence.
- FR-8: Update README and relevant knowledge-base architecture/build sections for both app versions and their supported validation commands. Update CHANGELOG for the implemented migration and SKILLS with reusable findings.
- FR-9: Document runtime prerequisites and remaining runtime-validation gaps without claiming untested V21 compatibility.

## NFRs

- NFR-1: Retain .NET Framework 4.8, existing package versions, and C# 7.3 unless a demonstrated V21 requirement makes that impossible; surface such a finding before expanding scope.
- NFR-2: No internet, remote Git operations, generator execution, TIA project upgrade, or external file edits without the applicable user authorization.
- NFR-3: No new source fork of `TIA_LIB`; keep changes focused and preserve user edits.
- NFR-4: Missing local prerequisites are explicit validation blockers, never reasons to silently substitute another API version.
- NFR-5: Builds of either version must not overwrite the other version's library output or intermediate caches.

## Validation And Testing

Implementation validation is compile-only by default. The following MSBuild executable exists on the inspected machine:

```powershell
$msbuildPath = 'C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/amd64/MSBuild.exe'
& $msbuildPath 'etu-process-generator-TiAv21/etu-process-generator-TiAv21.csproj' /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /m /v:minimal
& $msbuildPath 'etu-process-generator-TiAv21.sln' /t:Build /p:Configuration=Debug /p:Platform=x64 /m /v:minimal
```

Use a diagnostic build log if needed to verify resolved Siemens paths, actual assembly identities, V21 library project selection, and isolated intermediate/output paths. Ensure missing API references are treated as failures even if a build produces only reference-resolution warnings. Check output metadata without executing the generator.

Compare new app source against the V20 baseline to confirm only intended namespace/API changes. Check that each original Compile/Content item exists in the V21 library project and templates reach the V21 app output. Review the V20 files for unintended changes.

If a genuine V20 API installation is available, rebuild the V20 app in `Debug|x64` and, when shared source changed, repeat the V21 build afterward to exercise output isolation. The inspected Automation directory listed Portal V21 but no Portal V20; recheck prerequisites and report V20 compilation as blocked if absent. Never validate V20 against the V21 assemblies as a substitute.

Restore packages only if needed and network use is authorized. Do not add implementation-mirroring tests for copied metadata. Add focused offline tests only for substantive new compatibility logic that can execute without Portal attachment.

Runtime phase, only with explicit authorization and a disposable V21 TIA project copy: verify correct process/version attachment, PLC/Unified detection, XML export/import, preservation of Plant networks, both staging modes, and intended HMI postprocessing. Keep SiVArc disabled unless separately authorized for that test. Record outcomes separately from build results; do not present compile success as runtime acceptance.

## Plan

1. Recheck branch/status and current source; preserve any changes since this spec. Stay on the explicitly selected branch.
2. Reconfirm V21 API files/identities, .NET/MSBuild/packages, local loading guidance, and availability of a V20 installation.
3. Add the V21 app by copying only tracked inputs and updating identity/namespace/GUIDs.
4. Add the separate V21 library project with shared source lists, isolated intermediates/outputs, V21 references, and template-copy metadata.
5. Wire the V21 app project reference and new solution/configuration mappings.
6. Build against actual V21 APIs. Investigate each primary compiler error before cascade errors; apply minimal, documented adaptations.
7. Verify reference resolution, output isolation, source parity, template copying, and V20 configuration preservation. Run available V20 regression compilation when warranted.
8. Update README, CHANGELOG, relevant knowledge-base sections, and local SKILLS memory. Record runtime work as pending unless explicitly performed.
9. Review `git diff --check`, tracked/untracked/ignored files, and the exact intended change set. Commit or push only as authorized by the active task.

## Acceptance Criteria

- [x] AC-1: The V21 folder/project and dedicated solution exist with correct V21 identity and distinct project/solution GUIDs.
- [x] AC-2: V21 app and solution compile in `Debug|x64` against the locally installed V21 APIs.
- [x] AC-3: Reference evidence shows V21 assemblies only, including correct modular names and signing identities; no unresolved or mixed-version Siemens references remain.
- [x] AC-4: V21 library shares existing source, has independent intermediate/output paths, and copies required existing templates into the V21 app output.
- [x] AC-5: V20 app/solution/reference settings remain intact; any shared-source edits preserve V20 compatibility or have explicit version guards. V20 compile results or missing-prerequisite limits are recorded.
- [x] AC-6: Initial V21 unit definitions, comments, disabled regions, staging modes, and SiVArc state match the current V20 baseline.
- [x] AC-7: Documentation explains which solution to open, how to build, runtime working directory/prerequisites, and compile-versus-runtime validation status.
- [x] AC-8: No unintended Portal execution, TIA project conversion, external edits, vendor binaries, generated output, or unrelated cleanup appears in the change.

Runtime compatibility remains pending until the separately authorized runtime phase passes; AC-1 through AC-8 describe the implementation/build milestone.

## Open Questions

- Does the user ultimately want V20 retired? This spec defaults to preserving it and adding V21 alongside it; removal would be a separate scope decision.
- Which exact API member adaptations and runtime loading configuration does this installed V21 release require? Resolve with its local documentation and compiler evidence during implementation.
- Is a genuine V20 API installation available elsewhere for regression compilation? None was listed in the inspected standard Automation directory.
- Which disposable V21 PLC/Unified project and device libraries should be used for eventual runtime validation? No runtime validation is authorized by creating this spec.
- Should this spec be committed? `Specs/` is ignored by the current repository policy; use an explicit single-file force-add only when preparing an authorized commit, or intentionally change the ignore policy in a separate process decision.

## Git Specifics

- Inspected starting state: clean `upgrade-to-TIA-V21...origin/upgrade-to-TIA-V21` at `192be74` (`Merge pull request #4 from me-etu/g-008-generated-db-staging`). This describes local state; no fetch established remote freshness.
- User explicitly selected this branch for the migration. Do not create another branch or switch to main merely to follow a generic workflow. No fetch, merge, push, or other network operation was authorized.
- Expected implementation scope: new V21 app/solution, `TIA_LIB.V21.csproj`, narrowly necessary shared API adaptations, and relevant documentation. Preserve V20 project files and external machine-level source.
- Suggested implementation commit title: `Add TIA Portal V21 generator project`.
- Stage exact files deliberately; do not stage `bin/`, `obj/`, packages, vendor DLLs, generated XML, TIA project files, or ignored local inputs.
- At spec creation, `.gitignore` ignores both `Specs/` and `SKILLS.md`. Existing G-010 is tracked, which does not make new spec files visible to ordinary `git status`. This new spec is local until explicitly included; report that limitation.
- Do not run destructive Git cleanup/reverts or push without explicit authorization.

## Implementation Results (2026-10-20)

- AC-1 through AC-8: Pass for the implementation/build milestone. Dedicated identities/GUIDs, V21 references, isolated artifacts, mirrored library item metadata, six template output hashes, namespace-only app-source parity, and unchanged V20/shared implementation files were verified.
- App `/t:Rebuild` and solution `/t:Build`, both `Debug|x64`, passed with installed V21 APIs. Rebuild has seven pre-existing code warnings; solution incremental build has no warnings. Diagnostic evidence is in ignored local `V21-resolution.log`; rebuild evidence is in `V21-build.log`.
- Invalid `TiaV21ApiDirectory=Z:/Missing-TIA-V21/net48` fails with an explicit required-file error. Deployed executable config paths resolve to installed V21 assemblies (`21.0.0.0`, token `29bfe5fdf4ba5d3b`); no Siemens API DLLs remain in app output.
- Installed Siemens help (`TIAPortalOpennessenUS.zip`, topic `37231359755/37752246795.htm`) rejects Copy Local. Implemented its app.config approach with build-generated codeBase paths, preserving Program.cs except namespace; no resolver or shared-source adaptation was needed.
- NuGet restore of existing pinned versions was authorized by the user and succeeded. BouncyCastle 1.8.9 has restore-reported moderate advisories; versions were preserved.
- V20 regression build: Not tested, missing standard V20 API installation. No shared source or V20 project settings changed. Runtime validation: Not tested; no generator execution/Portal mutation was authorized or performed.
- Updated README, CHANGELOG, knowledge base, and ignored SKILLS memory. No commit, push, or ignore-policy change. This spec and SKILLS.md remain ignored local handoff files.
### Runtime follow-up: missing templates (2026-10-20)

The user's first marker-memory V21 runtime attempt attached and detected PLC/Unified, then stopped on missing `Xml/EmptyUnit.xml` after creating the unit folder. Initial compile/content checks did not cover undeclared source-required templates. Recovered the original unit, operation, phase, and empty-network templates unchanged from the user-provided original app output under `\\tsclient\D\Software\UnifiedSprechstunde15`; both library projects now deploy ten content templates. Debug|x64 Rebuild and Debug|AnyCPU Build pass; source-template coverage, XML parsing, and output hashes pass for both app outputs. Runtime import compatibility remains pending; no generator rerun was performed by the agent.
### Generated staging follow-up (2026-10-20)

User reports successful marker-memory generation; generated DB/UDT staging still errors (exact exception pending). Retained first generated UDT file has hard-coded V17 engineering header, outside the import versions listed in installed V21 help topic 59943517835. Added V21-library-only TIA_V21 constant and conditional UDT V21 header; V20 retains V17. Debug|x64 and Debug|AnyCPU builds pass, and in-memory UDT construction from rebuilt DLL verifies the header. No runtime upload was executed; option 2 remains unconfirmed. Preserve user's subsequently edited BV130_B10_20 active V21 unit scope.
### Confirmed staging import defect (2026-10-20)

The option-2 screenshot identifies missing Namespace on the first UDT import. Added mandatory empty global Namespace identifier to V21 UDT AttributeList using installed Siemens documentation. Installed interface XSD also rejects the old unqualified staging members; V21-only XML qualification now covers UDT/member comments, nested dbIO structs, and unit hwIN/hwOUT interfaces. Added Tests/Verify-V21StagingXml.ps1; both rebuilt Debug library outputs pass its offline schema/identifier/idempotence checks. Both app builds pass. An isolated source check without TIA_V21 confirms prior V20 serialization. Runtime import and complete option-2 execution remain to be retested by the user; no Portal connection or project mutation was performed by the agent.
### DB fallback metadata follow-up (2026-10-20)

Next runtime attempt passed UDT imports but failed on dbIO with unsupported engineering V17 from the DB template. Added a V21-only metadata update in the new-DB fallback loader (V21 header plus global Namespace); existing exports and V20 fallback are preserved. Expanded Tests/Verify-V21StagingXml.ps1 to cover the actual deployed DB template loader and SetMember path with full DB version/identifier checks, XSD validation, and replacement idempotence. Both Debug builds/tests pass; no Portal run was performed and full staging runtime completion remains pending.