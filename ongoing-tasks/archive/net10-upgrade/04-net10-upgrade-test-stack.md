# 04 — .NET 10 Upgrade — NUnit + Test SDK

## Goal

Bring the test stack to current. Decide between NUnit 3 (latest 3.x) and NUnit 4 — record the choice and apply it. Bump `Microsoft.NET.Test.Sdk` and the NUnit adapter.

## Track

dotnet (test infrastructure)

## What Exists

- `Tests/Tests.csproj`:
  - `nunit` 3.12.0
  - `NUnit3TestAdapter` 3.15.1
  - `Microsoft.NET.Test.Sdk` 16.2.0

## What to Build

- Decision: NUnit 3.x current vs. NUnit 4. NUnit 4 has small breaking changes around `Assert.That` constraint syntax and is the long-term direction. NUnit 3 current is a one-line bump.
- *Decision recorded in `## Notes` and approved before applying.*
- Bump `nunit`, `NUnit3TestAdapter` (or the NUnit 4 equivalent), `Microsoft.NET.Test.Sdk` per the decision.
- If NUnit 4: fix any constraint-syntax breaks surfaced by the build/test cycle.
- `dotnet test "SQL Buldozer.sln"` and verify the characterisation suite + any pre-existing tests are green.

## Acceptance Criteria

- [ ] Decision (3.x vs 4) recorded in `## Notes`, dated.
- [ ] Test packages bumped per decision.
- [ ] `dotnet test "SQL Buldozer.sln"` exits 0.
- [ ] Characterisation suite still passes.

## References

- Related ADRs: —
- Related methodology: `docs/methodology/dotnet-cli.md` § Testing.
- Depends on: `03-net10-upgrade-msext-serilog.md`.
