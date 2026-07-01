# G-010 Bugfix: Prune Unsupported Multilingual Cultures Before PLC XML Import

## Summary

Fix PLC block import failures caused by generated Simatic ML XML containing multilingual text cultures that are not enabled in the target TIA Portal project.

The immediate observed failure is:

```text
Cannot import multilingual text with culture 'it-IT' at line number 311 at line position 16:
the specified culture does not exist within the current project.
```

The implementation should prune unsupported `MultilingualTextItem` entries from generated XML before `PlcBlockComposition.Import(...)`, while preserving existing project-supported languages and existing manual/generated TIA content.

## Problem

Generator XML templates include multilingual text items for cultures such as `it-IT`, `fr-FR`, and `fr-BE`. If the open TIA Portal project does not have one of those cultures configured, importing generated PLC XML fails.

Observed stack:

```text
Siemens.Engineering.SW.Blocks.PlcBlockComposition.Import(...)
TIA_LIB.SiemensPortal.ImportPlcBlock(...) in TIAOpenness/TIA_Lib/TIA_LIB/SiemensPortal.cs
TIA_LIB.Xml.XmlBlock.Upload(...) in TIAOpenness/TIA_Lib/TIA_LIB/Xml/XmlBlock.cs
TIA_LIB.PlcProject.Upload(...) in TIAOpenness/TIA_Lib/TIA_LIB/PlcProject.cs
EtuProcessGeneratorTiaV20.Project..ctor(...) in etu-process-generator-TiAv20/Project.cs
```

The failed generated file was:

```text
etu-process-generator-TiAv20/bin/Debug/Plant.xml
```

The line around the failure contained:

```xml
<MultilingualTextItem ID="1C" CompositionName="Items">
  <AttributeList>
    <Culture>it-IT</Culture>
    <Text />
  </AttributeList>
</MultilingualTextItem>
```

The generated `Plant.xml` contained `en-US`, `de-DE`, `es-ES`, `it-IT`, and `fr-FR`, while the already-exported project content in the same file appeared to use only `de-DE`, `en-US`, and `es-ES`.

## Goals

- Prevent import failure when generated XML templates contain cultures that are not enabled in the target TIA project.
- Keep the fix surgical and local to repo-local `TIA_LIB`.
- Preserve existing valid multilingual texts for cultures already present in the exported TIA block/project XML.
- Avoid forcing users to enable extra TIA project languages such as Italian or French.
- Avoid broad template rewrites unless they are the smallest reliable fix.
- Leave the generator app side-effect behavior unchanged except for pruning unsupported XML culture items before import.

## Non-Goals

- Do not add or remove languages in the open TIA Portal project.
- Do not run the generator app as part of ordinary validation.
- Do not redesign multilingual text generation.
- Do not delete whole `MultilingualText` containers when only individual unsupported `MultilingualTextItem` children need removal.
- Do not change `Project.cs` unit/device definitions.
- Do not touch the old machine-level `C:\TIAOpenness\TIA_Lib\TIA_LIB` copy.

## Current Behavior

Relevant code path:

```text
Project.cs
  -> Upload()
  -> PlcProject.Upload()
  -> XmlBlock.Upload()
  -> Xml.Save(FileName)
  -> SiemensPortal.Current.ImportPlcBlock(UserGroup, _Name, FileName)
  -> user_group.Blocks.Import(new FileInfo(fileName), ImportOptions.Override)
```

Relevant files:

- `TIAOpenness/TIA_Lib/TIA_LIB/Xml/XmlBlock.cs`
- `TIAOpenness/TIA_Lib/TIA_LIB/SiemensPortal.cs`
- `TIAOpenness/TIA_Lib/TIA_LIB/Xml/XmlNetwork.cs`
- `TIAOpenness/TIA_Lib/TIA_LIB/Xml/EmptyNetwork.xml`
- `TIAOpenness/TIA_Lib/TIA_LIB/Xml/EmptyBlock.xml`
- `TIAOpenness/TIA_Lib/TIA_LIB/Xml/EmptyDatablock.xml`
- `TIAOpenness/TIA_Lib/TIA_LIB/Xml/EmptyPlant.xml`

`XmlNetwork` creates new networks from `Xml/EmptyNetwork.xml`. That template contains `MultilingualTextItem` entries for several languages. When those entries are inserted into an exported block that came from a project with fewer configured languages, TIA rejects import.

Current templates include cultures such as:

```text
en-US
de-DE
es-ES
it-IT
fr-FR
fr-BE
```

## References

- Error report from TIA Portal Openness import:
  - culture: `it-IT`
  - generated file: `etu-process-generator-TiAv20/bin/Debug/Plant.xml`
  - line: `311`
- Repo guidance:
  - `AGENTS.md`
  - `README.md`
  - `SKILLS.md`
  - `GENERATOR_KNOWLEDGE_BASE.md`
- Existing behavior notes:
  - Plant generation is additive and must preserve existing Plant networks/calls.
  - Running the app connects to or starts TIA Portal and mutates the open TIA project.
  - Compile-only checks are preferred for non-runtime validation.

## Proposed Architecture

Add a pruning step immediately before saving/importing changed PLC XML in `XmlBlock.Upload()`.

Recommended approach:

1. Determine the allowed cultures for the block XML.
2. Remove `MultilingualTextItem` elements whose child `<Culture>` value is not allowed.
3. Save the pruned XML to `FileName`.
4. Import the pruned XML using the existing import path.

Preferred allowed-culture source:

- Derive allowed cultures from the exported block XML that was loaded before new generated networks/items were added.
- Store that set on `XmlBlock` during construction, before generated template nodes are inserted.
- If no exported XML exists and the block starts from a template, fall back to a conservative default set already used in this project: `de-DE`, `en-US`, `es-ES`.

Rationale:

- The exported block is the best local evidence of cultures the TIA project already accepts.
- Dynamic pruning avoids hard-coding the current machine/project language setup forever.
- The fallback supports newly created blocks where no existing export is available.

Implementation shape:

```csharp
private HashSet<string> _AllowedCultures;

private HashSet<string> GetCultures(XElement xml)
{
    return new HashSet<string>(
        xml.Descendants("Culture")
           .Select(el => el.Value)
           .Where(value => !string.IsNullOrWhiteSpace(value)));
}

private void PruneUnsupportedMultilingualTextItems()
{
    if (_AllowedCultures == null || _AllowedCultures.Count == 0)
    {
        _AllowedCultures = new HashSet<string> { "de-DE", "en-US", "es-ES" };
    }

    var unsupportedItems = Xml
        .Descendants("MultilingualTextItem")
        .Where(item =>
        {
            var culture = item.Descendants("Culture").FirstOrDefault()?.Value;
            return !string.IsNullOrWhiteSpace(culture) && !_AllowedCultures.Contains(culture);
        })
        .ToList();

    foreach (var item in unsupportedItems)
    {
        item.Remove();
    }
}
```

The final code may use local helper names/style that better match `TIA_LIB`, but the behavior should remain as above.

Avoid relying on current thread culture. `Project.cs` currently sets `Thread.CurrentThread.CurrentCulture` to `en-US`, but the TIA import fault is about project languages in Simatic ML, not .NET numeric/string formatting.

## FRs

- FR-1: Before `XmlBlock.Upload()` saves and imports XML, remove unsupported `MultilingualTextItem` entries from `Xml`.
- FR-2: The allowed-culture set must be captured from exported XML when `SiemensPortal.Current.ExportPlcBlock(...)` returns an existing block.
- FR-3: When no exported XML exists, use the fallback allowed cultures `de-DE`, `en-US`, and `es-ES`.
- FR-4: The pruning step must remove only `MultilingualTextItem` elements with unsupported `<Culture>` values.
- FR-5: The pruning step must leave supported cultures and their text values unchanged.
- FR-6: The pruning step must be applied to all `XmlBlock` uploads, including Plant and generated FB/DB XML paths that share `XmlBlock.Upload()`.
- FR-7: The import path in `SiemensPortal.ImportPlcBlock(...)` should stay behaviorally unchanged unless a minimal diagnostic/logging addition is necessary.
- FR-8: Document the fix in `CHANGELOG.md`.
- FR-9: Add a concise `SKILLS.md` note with the symptom, root cause, and fix location.

## NFRs

- NFR-1: Keep changes small and localized, preferably in `TIAOpenness/TIA_Lib/TIA_LIB/Xml/XmlBlock.cs`.
- NFR-2: Do not mutate TIA Portal during compile-only validation.
- NFR-3: Do not introduce new package dependencies.
- NFR-4: Preserve deterministic XML output for a given source project and generator input.
- NFR-5: Avoid broad reformatting or unrelated cleanup.
- NFR-6: The code should remain compatible with the current .NET Framework/C# language level used by the project.

## Validation And Testing

Compile-only validation:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe' `
  'etu-process-generator-TiAv20/etu-process-generator-TiAv20.csproj' `
  /p:Configuration=Debug /p:Platform=x64 /m
```

If the Insiders path is unavailable, use the documented Community path when present:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' `
  'etu-process-generator-TiAv20/etu-process-generator-TiAv20.csproj' `
  /p:Configuration=Debug /p:Platform=x64 /m
```

Static XML validation:

- Use the existing failed/import XML evidence if present:
  - `etu-process-generator-TiAv20/bin/Debug/Plant.xml`
- After implementation, create or inspect a generated XML sample only if it can be done without running the app.
- If a helper method is accessible enough, test it with an in-memory `XElement` containing supported and unsupported `MultilingualTextItem` entries.

Runtime validation:

- Running the app is side-effecting and must be done only when the user explicitly intends to retry the TIA import.
- If runtime validation is approved, verify that `Plant.xml` no longer contains unsupported cultures such as `it-IT` or `fr-FR` when the exported Plant block only contains `de-DE`, `en-US`, and `es-ES`.
- Confirm the original `it-IT` import error does not recur.
- If TIA then reports another culture, use that as evidence that pruning did not run on the correct XML path or allowed-culture capture was wrong.

Regression checks:

- Existing Plant additive behavior must remain intact.
- Existing networks/calls such as legacy/manual Plant content must not be deleted.
- Existing supported multilingual text entries must remain.

## Plan

1. Inspect current worktree:

```powershell
git status --short --branch
```

2. Create a focused branch from `main` only after preserving/understanding existing user changes:

```powershell
git switch -c fix/prune-unsupported-xml-cultures
```

3. Read the exact implementation files:

```text
TIAOpenness/TIA_Lib/TIA_LIB/Xml/XmlBlock.cs
TIAOpenness/TIA_Lib/TIA_LIB/Xml/XmlNetwork.cs
TIAOpenness/TIA_Lib/TIA_LIB/SiemensPortal.cs
TIAOpenness/TIA_Lib/TIA_LIB/Xml/EmptyNetwork.xml
```

4. Add allowed-culture capture in the `XmlBlock` constructor after export/template load is understood.

5. Add a private pruning helper in `XmlBlock`.

6. Call the pruning helper in `XmlBlock.Upload()` before `Xml.Save(FileName)`.

7. Build the app project in `Debug|x64`.

8. Update `CHANGELOG.md`.

9. Update `SKILLS.md` with the reusable learning.

10. Review diff carefully:

```powershell
git diff -- TIAOpenness/TIA_Lib/TIA_LIB/Xml/XmlBlock.cs CHANGELOG.md SKILLS.md
```

11. Commit only the bugfix and documentation:

```powershell
git add TIAOpenness/TIA_Lib/TIA_LIB/Xml/XmlBlock.cs CHANGELOG.md SKILLS.md Specs/G-010-prune-unsupported-multilingual-cultures.md
git commit -m "Fix unsupported multilingual culture XML imports"
```

12. Push only if the user approves remote access:

```powershell
git push -u origin fix/prune-unsupported-xml-cultures
```

## Acceptance Criteria

- AC-1: The implementation prunes `MultilingualTextItem` entries for cultures absent from the allowed-culture set.
- AC-2: For the observed project language set, generated import XML no longer contains `it-IT`, `fr-FR`, or `fr-BE` entries when those cultures are unsupported.
- AC-3: Supported cultures such as `de-DE`, `en-US`, and `es-ES` remain in generated XML.
- AC-4: The app project builds successfully in `Debug|x64`.
- AC-5: No generator app run is performed unless explicitly requested.
- AC-6: `CHANGELOG.md` records the behavior fix.
- AC-7: `SKILLS.md` records the symptom and fix location for future sessions.
- AC-8: The final diff does not include unrelated refactors, broad template rewrites, or external `C:\TIAOpenness` changes.

## Open Questions

- Should the fallback allowed cultures be exactly `de-DE`, `en-US`, and `es-ES`, or should fallback be `en-US` only for brand-new blocks with no export evidence?
- Is there a reliable TIA Openness API available in this repo context to query configured project languages directly? If yes, a future enhancement could replace export-derived culture inference.
- Should unsupported culture pruning also run immediately after loading template XML for blocks that are never exported before import, or is the pre-save step sufficient?

## Git Specifics

- Starting state when this spec was created:

```text
main...origin/main
 M CHANGELOG.md
 M etu-process-generator-TiAv20/Project.cs
```

- Treat the existing `CHANGELOG.md` and `Project.cs` modifications as user/current-session changes. Do not revert or overwrite them.
- Create a focused bugfix branch before implementation when practical.
- Keep the implementation commit surgical:
  - `TIAOpenness/TIA_Lib/TIA_LIB/Xml/XmlBlock.cs`
  - `CHANGELOG.md`
  - `SKILLS.md`
  - this spec file if not already committed
- Do not include generated binaries, `bin/`, `obj/`, failed import XML, or unrelated workbook/template artifacts.
- Do not run `git reset --hard`, `git clean`, or checkout-based reverts.
- Do not push without explicit user approval for remote access.
