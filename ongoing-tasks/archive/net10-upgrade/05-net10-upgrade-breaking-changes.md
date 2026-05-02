# 05 — .NET 10 Upgrade — Resolve breaking changes

## Goal

Catch-all step for any source change required to make the upgraded build green. By this point, framework + packages are bumped; everything that was going to break has surfaced. Fix it. No behavioural change.

## Track

dotnet (cross-cutting)

## What Exists

- All previous steps green at the build/test level except for residual source-level breaks captured in earlier `## Notes` entries.

## What to Build

- Code edits limited to making the build compile and the characterisation suite green. No refactors, no "while I'm in there" cleanups.
- Anticipated touch points (likely, not exhaustive):
  - `Program.cs` `ConfigureAppConfiguration` / `ConfigureLogging` lambdas — extension method shapes may have changed.
  - `Serilog.Settings.Configuration.ReadFrom.Configuration(IConfiguration)` — signature stable in recent versions but worth re-checking.
  - `FluentCommandLineParser.NETStandard` — if unmaintained on .NET 10, this becomes a *separate, approved* replacement decision (do **not** swap silently).
- For any change beyond a one-line API delta, leave a comment-free commit and a short `## Notes` entry explaining what broke and why.

## Acceptance Criteria

- [ ] `dotnet build "SQL Buldozer.sln"` succeeds with zero errors.
- [ ] `dotnet test "SQL Buldozer.sln"` exits 0; characterisation suite green.
- [ ] No new packages introduced (any new dependency is an approved decision recorded separately).
- [ ] No behavioural change captured by the characterisation suite.

## References

- Related ADRs: —
- Related methodology: `docs/methodology/dotnet-cli.md`.
- Depends on: `04-net10-upgrade-test-stack.md`.
