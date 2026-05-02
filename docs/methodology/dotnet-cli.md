# .NET CLI module recipe — SQL Buldozer

> Stack-specific recipe for the C# modules of the SQL Buldozer CLI. Tripwires live in [`CLAUDE.md`](../../CLAUDE.md) — this file is the longer-form "how" for routine C# work.

This is a CLI app, not a service. Do not import service-shaped patterns (controllers, MediatR, EF, `IHttpClientFactory`, health-check endpoints, OpenAPI).

---

## Composition

- **Composition root:** `AppBootstrap.CreateAppBuilder(args)` in `ParameterizationExtractor/AppBootstrap.cs`. Returns an `IAppBuilder` (custom thin wrapper around `IServiceCollection`, defined in `ParameterizationExtractor/Common/SqlBuldozerApp.cs`). Module-level extension methods (`.AddMSSQL()`, `.AddExecutor()`) register parts. `IAppBuilder.Build()` delegates to `ServiceCollection.BuildServiceProvider()`.
- **DI container is `Microsoft.Extensions.DependencyInjection`.** Registrations use `services.AddSingleton<...>()` / `AddTransient<...>()` via the `IAppBuilder.ConfigureServices(...)` extension methods. **Constructor injection only** — no `[Export]` / `[Import]` attributes, no MEF, no plug-in-from-disk discovery. See [adr/006-msdi-container.md](../../adr/006-msdi-container.md).

---

## Configuration

- `appsettings.json` (committed, real defaults) + environment variables, layered via `Microsoft.Extensions.Configuration`. Bound to POCOs in `Program.cs` only — no `IConfiguration["..."]` lookups in business code.
- **CLI argument parsing:** `FluentCommandLineParser`. Args take precedence over `appsettings.json`.
- **Connection strings** live under `ConnectionStrings.*` in `appsettings.json` (Windows auth via `Integrated Security=True` is the shipped default; SQL auth works by overriding the connection string). Never hardcode a server, database, or credential anywhere else.

---

## Logging

- **Serilog** with Console and File sinks, configured from the `Serilog` section of `appsettings.json`.
- Resolve via `ILogger<T>`. No `Console.WriteLine` outside CLI startup or operator-facing CLI output.
- Use **named placeholders**, never string interpolation:
  ```csharp
  logger.LogInformation("Extracting {Table} with strategy {Strategy}", tableName, strategy);
  ```

---

## Module layering

Allowed `<ProjectReference>` graph (also a tripwire):

```
CLI            → Common, Logic, DSL, DSL.Connector
DSL.Connector  → DSL, Logic
DSL            → Logic
Logic          → Common
Common         → (nothing)
```

Back-edges are forbidden. If you find yourself wanting one, that is the signal to ask. See `adr/002-module-layering.md`.

Module-specific rules:

- **`ParameterizationExtractor.Logic`** is the only module that opens a `SqlConnection` or otherwise uses `System.Data.SqlClient`. Other modules never see ADO.NET types.
- **`ParameterizationExtractor.Common`** stays dependency-light: only `Microsoft.Extensions.Logging.Abstractions`. New third-party deps require an ADR — Common is referenced everywhere, so its deps leak everywhere.

---

## SQL generation

- The canonical surface for emitting `INSERT`/`UPDATE` text is the T4 template `ParameterizationExtractor.Logic/Templates/DefaultTemplate.tt`, driven by the SQL builder in `Logic`.
- No new ad-hoc string-concat paths for emitting SQL. Extend the template + builder. See `adr/004-t4-sql-generation.md`.

---

## Async / threading hygiene

- No `Thread.Sleep`, `.Result`, `.Wait()`, `async void` outside event handlers.
- Cancellation: `Program.Main` registers a `CancellationTokenSource` against `Console.CancelKeyPress`. New long-running paths take a `CancellationToken` and observe it.

---

## Error handling

- Explicit exit codes via the `ExitCode` enum in `Program.cs`. `Environment.Exit((int)code)` at the top level.
- Fatal errors are logged before exit. Do not swallow exceptions silently.

---

## Testing

- **Framework:** NUnit (3.12.0) via the single `Tests/` project (`net6.0`). No per-module test sub-projects.
- **Run:** `dotnet test "SQL Buldozer.sln"`.
- The `Tests` project references all four production projects directly, including the F# `DSL.fsproj`. Cross-stack tests are encouraged when a behaviour spans the F# parser and the C# engine.

---

## Project shape — quick reference

| Module | TargetFramework | RootNamespace |
|---|---|---|
| `ParameterizationExtractor` (Exe) | `net6.0` | `Quipu.ParameterizationExtractor` |
| `ParameterizationExtractor.Common` | `netstandard2.0` | `Quipu.ParameterizationExtractor.Common` |
| `ParameterizationExtractor.Logic` | `netstandard2.0` | `Quipu.ParameterizationExtractor.Logic` |
| `ParameterizationExtractor.DSL.Connector` | `netstandard2.0` | `Quipu.ParameterizationExtractor.DSL.Connector` |
| `Tests` | `net6.0` | (default) |
