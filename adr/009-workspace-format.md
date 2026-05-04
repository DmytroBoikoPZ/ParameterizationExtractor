# 009 — Workspace format — JSON via `System.Text.Json`, `$kind` polymorphic discriminator, `.bws` wrapper

## Status

Accepted

## Context

The desktop UI ([ADR-007](./007-desktop-wpf-stack.md)) needs an authoring format for workspaces. The user-facing decisions in [`docs/design/desktop-ui/readme.md`](../docs/design/desktop-ui/readme.md) lock JSON as the canonical workspace format, the `.bws` extension, and a wrapper that carries workspace metadata + source-connection metadata + the engine-readable subtrees.

The engine today consumes XML via [`ParameterizationExtractor/Configs/ConfigSerializer.cs`](../ParameterizationExtractor/Configs/ConfigSerializer.cs) and an alternative F# DSL form via the connector ([ADR-003](./003-fsharp-fparsec-dsl.md)). The DSL is frozen ([ADR-005](./005-freeze-fsharp-dsl.md)). Adding JSON to the engine input is a binding decision: it commits the engine to a new dependency, a polymorphism strategy, and a long-tail of "both XML and JSON, indefinitely" support.

The shape decisions need to be locked **before** the engine reader is implemented (step 02 of the `desktop-workspace-format` feature) so step 02 has no design surface to invent and so any future feature that consumes JSON workspaces follows the same conventions.

## Decision

We will:

- **Add JSON as a first-class engine input format**, alongside XML, for `Package` and `GlobalExtractConfiguration`. JSON is canonical for new workspaces; XML stays readable indefinitely. No flag day, no migration tool — the formats coexist permanently.
- **Use `System.Text.Json` 10.0.0** as the only JSON library. No Newtonsoft.Json (zero present in the codebase today; keeping it that way avoids dual-library mental load and keeps source-generator paths open for later).
- **Apply `JsonNamingPolicy.CamelCase`** to property names. C# POCOs stay PascalCase internally.
- **Express `ExtractStrategy` polymorphism** via `[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]` + `[JsonDerivedType]` on each concrete subtype. Discriminator values are short user-facing names matching the inspector mockup: `FKDependency`, `OnlyOneTable`, `OnlyChildren`, `OnlyParent`. `IgnoreUnrecognizedTypeDiscriminators = false` and `UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization` — closed-set, no silent base-type fallthrough.
- **Adopt the file extension `.bws`** for workspace files. The `.bws` wrapper is JSON: a fixed envelope `{ $version, name, source, global, package }`. Only `$version: 1` is accepted today; unknown versions throw on load.
- **Make the wrapper desktop-only.** The engine never reads a `.bws` file directly — only its embedded `global` and `package` subtrees, through the new readers. This keeps the engine's responsibility narrow and lets the wrapper evolve independently of engine inputs.
- **Preserve pre-existing typos verbatim** in JSON property names (`throwExecptionIfNotExists`, `uniqueColums`). XML round-trip equivalence is simpler this way; a future "spelling-fix" feature can add `[JsonPropertyName]` aliases.
- **Engine connection** stays out of the engine input. The `source` block in the workspace wrapper is for the desktop's own use; the engine receives connections through `IUnitOfWorkFactory` as today.

Concrete shape spec lives at [`docs/methodology/workspace-format.md`](../docs/methodology/workspace-format.md). Sample at [`examples/sample.bws`](../examples/sample.bws).

Alternatives considered and rejected:

- **`Newtonsoft.Json`** — bigger surface, idiosyncratic polymorphism, no compelling feature for this workload. The repo has zero current usage; introducing it would expand mental load for no return.
- **No polymorphism — flatten `ExtractStrategy` into one type with a `kind` enum** — would require renaming every consumer of the abstract API. Keeps engine internals tied to JSON shape decisions; the polymorphic option is more honest about the model.
- **`$type` discriminator** (Newtonsoft style) — sets a false expectation that Newtonsoft conventions apply. `$kind` is unambiguous.
- **PascalCase JSON properties** — possible, but breaks modern convention and clashes with how every JSON example out there reads. C# POCOs are still PascalCase; the converter does the bridge.
- **Separate JSON wrapper schema for engine input** — over-engineering. The wrapper is desktop-only; the engine's input shape and the wrapper's `global`/`package` subtrees deliberately match.
- **Versioned JSON Schema document** — useful for IDE autocomplete eventually; not required for v1. Defer.

## Consequences

- **Easier:** the desktop has a single, agreed JSON shape to bind to before any UI feature lands. Engine consumers (CLI today, Desktop tomorrow, anything later) share one reader path. New `.bws` files are diffable, hand-editable, and reviewable. The `$kind` discriminator makes the model self-describing for any future tooling.
- **Harder:** the engine now has two input formats to maintain. Any future shape change to `Package` or `GlobalExtractConfiguration` must be applied in both XML attributes and JSON conventions, with characterisation tests proving parity. The "both indefinitely" commitment means no easy retirement of XML.
- **Open follow-ups:**
  - **Schema migration:** `$version` is the slot. When v2 lands (e.g. for a new field that isn't backward-compatible), a separate ADR + migration policy.
  - **CLI accepting `.bws` directly** — the CLI's `--package` flow today supports `.xml` / `.bc`. Extending it to `.bws` (or even `.json` for engine subtrees) is a small additive feature; not imposed.
  - **Password-at-rest** — `source.passwordEncrypted` is a placeholder field. The `desktop-connection-management` feature lands the DPAPI encryption flow + an ADR for the encryption choices.
  - **`[JsonPropertyName]` aliases for the typos** — `ThrowExecptionIfNotExists` and `UniqueColums` are preserved verbatim in JSON. A future cosmetic feature can add aliases so new workspaces use correct spellings while old ones continue to load.
  - **Source-generator paths for `System.Text.Json`** — could replace reflection-based deserialization with the source generator for AOT-friendliness. Optional optimisation; current code paths work without it.
