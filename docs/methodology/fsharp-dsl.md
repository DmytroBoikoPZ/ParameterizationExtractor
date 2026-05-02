# F# DSL recipe — SQL Buldozer

> Stack-specific recipe for the F# parser in `ParameterizationExtractor.DSL`. Tripwires live in [`CLAUDE.md`](../../CLAUDE.md) — this file is the longer-form "how" for routine F# work.

The F# code in this repo serves one purpose: a small parser for the extraction DSL. It uses **FParsec** to produce an AST that the C# `DSL.Connector` translates into engine config. Treat the F# project as a focused parser library, not a general-purpose F# playground.

---

## Where things live

`ParameterizationExtractor.DSL/` contains:

| File | Role |
|---|---|
| `AST.fs` | Canonical F# types representing parsed DSL constructs |
| `Mapper.fs` | Helpers for shaping/transforming AST values |
| `ParserResult.fs` | Result wrapper used to surface parse outcomes across the F#↔C# boundary |
| `InternalCommandParser.fs` | FParsec parsers for the smaller command-level grammar |
| `Parser.fs` | Top-level FParsec parser composing the whole DSL grammar |

The C#↔F# bridge is `ParameterizationExtractor.DSL.Connector`. Nothing else in the C# code consumes the F# AST directly. See `adr/003-fsharp-fparsec-dsl.md`.

---

## Compilation order matters

F# files compile in the order they appear in `<ItemGroup><Compile Include>` in `ParameterizationExtractor.DSL.fsproj`. Alphabetical does not work — a file can only reference types/functions defined earlier in the order.

Current order:

```
AST.fs → Mapper.fs → ParserResult.fs → InternalCommandParser.fs → Parser.fs
```

When adding a new `.fs` file, insert it explicitly into the `<Compile Include>` list at the right position. Do not rely on the IDE to do this automatically.

---

## Parsing rules

- **Use FParsec.** No regex / `String.Split` substitutes for grammar parsing. If a construct is too messy for the grammar, refactor the grammar — don't slip back to regex.
- **`AST.fs` is the canonical representation.** Do not duplicate AST shapes in C#. The Connector consumes the F# types directly.
- **Surface failures via `ParserResult`, not exceptions.** Parse errors must cross the F#↔C# boundary as values that the Connector matches on — exceptions cause confusing stack traces in the CLI host.

---

## Interop with C#

- **No `null` returns from F# functions consumed by C#.** Use `Option<T>` (or `ParserResult` for parse outcomes). The C# side unwraps explicitly — usually in `DSL.Connector`.
- Prefer F# record types for AST nodes — they map cleanly to C# read-only views.
- If a public F# function is awkward to call from C# (curried args, unit returns), wrap it in a saner shape inside `DSL.Connector` rather than rewriting the F# side.

---

## Testing

- F# DSL behaviour is tested from the C# `Tests/` project (NUnit). The `Tests` project references `DSL.fsproj` directly. There is no separate F# test project.
- A typical parser test: feed a DSL fragment, assert on the produced AST. Test parse-failure paths through `ParserResult` cases, not by catching exceptions.

---

## Quick reference

| Item | Value |
|---|---|
| TargetFramework | `netstandard2.0` |
| Parser library | FParsec 1.1.0 |
| Build/test | `dotnet test "SQL Buldozer.sln"` (shared toolchain) |
| Bridge to C# | `ParameterizationExtractor.DSL.Connector` |
