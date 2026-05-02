# 005 — Freeze the F# DSL — no new feature work; retire when superseded

## Status

Accepted

## Context

The F# DSL (`ParameterizationExtractor.DSL` + `ParameterizationExtractor.DSL.Connector`) was built in 2019-2020 alongside the XML config (`ExtractConfig.xml`) as an exploratory parser, partly for the joy of writing an FParsec grammar. It is one of two ways to describe an extraction: the XML config is the working format, the DSL is a textual alternative.

The current direction of the project (2026) is to add a desktop UI as the primary operator-facing config surface. Maintaining three config surfaces — XML, F# DSL, UI — is unjustified for a single-author tool with no external user base for the DSL. The DSL has not been extended in years, has no production consumers outside the original author, and its only contributors are also the author.

This decision must be made now, ahead of the .NET 10 / MS.DI / UI work, because each of those touches the F# projects (build pipeline, MEF registrations of the Connector, future UI surface). Freezing them removes ambiguity about how much investment they get.

## Decision

We freeze `ParameterizationExtractor.DSL` and `ParameterizationExtractor.DSL.Connector`.

- **No new grammar features, no new AST nodes, no new DSL constructs.**
- The existing parser remains functional and is kept buildable through framework / dependency upgrades.
- Existing tests are preserved as a regression guard; new tests are added only when needed to prevent regression during a build/framework bump (not to expand coverage).
- The C#↔F# bridge (`DSL.Connector`) remains in the layering tripwire (still the only legal consumer of the F# AST).
- A "Frozen" banner is added to the F# section of `CLAUDE.md` and to `docs/methodology/fsharp-dsl.md` pointing here.

Alternatives considered:

- **Retire immediately (delete both projects).** Rejected for now — there may be DSL-config files in the author's own usage history that still need to parse. Retire after the UI ships and a survey confirms no surviving DSL configs.
- **Keep extending alongside the UI.** Rejected — three config surfaces is too many for a single-maintainer project, and the DSL has no user base demanding the investment.
- **Repurpose F# for something else** (e.g., a `Where`-clause expression language). Rejected as a new initiative — this ADR is about the DSL as it stands, not about future F# work. A future ADR can revisit if a real F#-fit need emerges.

## Consequences

- **Easier:** One fewer surface to evolve during .NET 10, MS.DI, and UI work. F# tripwires effectively reduce to "do not extend." The dotnet-cli recipe and architecture overview can stop equivocating about "two config front-ends." The mental model — XML today, UI tomorrow, DSL frozen in between — is simple to communicate.
- **Harder:** If a future user (likely the author) wants to express a new extraction shape in DSL form, they cannot. They must use XML or, once it ships, the UI. There is no DSL-to-XML migration tool; existing DSL files keep working but cannot be enriched.
- **Open follow-ups:**
  - Update `CLAUDE.md` § F# tripwires and `docs/methodology/fsharp-dsl.md` with a "Frozen — see ADR-005" banner. (Small follow-up, not urgent.)
  - After the desktop UI ships, evaluate retirement: keep, or delete `DSL` + `DSL.Connector` projects entirely. Captured as a separate decision at that time.
