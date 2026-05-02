# Goldens

Pinned, normalised SQL output of the SQL Buldozer engine for each characterisation scenario. One file per scenario, named `{scenario-name}.sql`. Format: trailing-whitespace-stripped, LF line endings, single trailing newline (see `Harness/SqlNormalizer.cs`).

## Regenerate (after an intentional engine change)

Run **only the affected scenario** in record mode. Review the diff in git. Commit.

```powershell
$env:BULDOZER_GOLDEN_MODE = "record"
$env:BULDOZER_GOLDEN_SCENARIO = "only-parent-from-payment"
dotnet test "SQL Buldozer.sln" --filter "FullyQualifiedName~CharacterisationTests"
$env:BULDOZER_GOLDEN_MODE = $null
$env:BULDOZER_GOLDEN_SCENARIO = $null
```

Omit `BULDOZER_GOLDEN_SCENARIO` to regenerate every scenario at once.

## Fragility caveat

These goldens are pinned against real `budzdorov_Core` rows. Schema or row drift on the dev DB invalidates them. Step 02's `SchemaSurvey.md` documents seed IDs and the FK shape each scenario depends on; re-run the FK snapshot in `ongoing-tasks/characterisation-tests-checklist.md` `## Notes` after any DB rebuild.
