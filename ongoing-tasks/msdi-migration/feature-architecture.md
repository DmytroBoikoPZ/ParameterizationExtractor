# MEF → MS.DI Migration — Architecture Overview

> Internal refactor. Replaces the application's DI container without changing what gets resolved or in which lifetime.

---

## 1. Pipeline / Integration

The composition root moves from MEF to `IServiceCollection`. The runtime call graph is unchanged: same components, same dependencies, same lifetimes (transient/singleton choices preserved).

```
Before:
  Program.Main
     └── AppBootstrap.CreateAppBuilder
            ├── ConfigureAppConfiguration  (Microsoft.Extensions.Configuration)
            ├── ConfigureLogging           (Microsoft.Extensions.Logging + Serilog)
            ├── AddMSSQL()                 (MEF [Export] discovery)
            └── AddExecutor()              (MEF [Export] discovery)

After:
  Program.Main
     └── Host.CreateApplicationBuilder    (Microsoft.Extensions.Hosting)
            ├── Configuration               (same JSON + env)
            ├── Logging                     (same Serilog)
            └── services
                   ├── AddBuldozerSql()    (proposed name; approval required)
                   └── AddBuldozerExecutor()
```

| Stage | Before | After |
|-------|--------|-------|
| Registration | `[Export]` attribute | `services.AddX<Impl>()` |
| Many-of-T | `[ImportMany] IEnumerable<T>` | `IEnumerable<T>` ctor parameter (works out-of-the-box with multiple `AddSingleton<T, ImplN>`) |
| One-of-T | `[Import] T` | `T` ctor parameter |
| Lifetime | implicit / `[Shared]` | explicit `Add{Transient|Scoped|Singleton}` |
| Composition root | `AppBootstrap.CreateAppBuilder` | `Host.CreateApplicationBuilder` (or equivalent) |

---

## 2. Component Diagram

No new components. Same modules, same references. The change is what wires them.

```
ParameterizationExtractor (CLI)
        │
        │ uses → IServiceCollection (new)
        │ uses → Host.CreateApplicationBuilder (new)
        │ no longer uses → System.Composition (removed)
        ▼
Logic + Common + DSL.Connector + DSL  (unchanged)
```

---

## 3. Data Flow

### 3.1 Inbound / Ingestion

N/A — runtime data path unchanged.

### 3.2 Processing / Event Handling

`Program.cs` builds the host, resolves the top-level executor, runs it. The `IApp` / `BuildApp` shape may collapse into a smaller surface — that's a design call captured in step 02.

### 3.3 Query / Serving

N/A.

---

## 4. Data Stores Summary

N/A.

---

## 5. Extension Points

The `AddBuldozerXXX()` extension methods become the only extension surface. New parts plug in by extending those methods, not by dropping in MEF assemblies. This is a behavioural change for anyone who relied on MEF's "drop a DLL in the folder, get auto-discovery" model — the audit in step 01 must explicitly verify that no such consumer exists.

---

## 6. Security & Isolation

- No change to runtime trust boundaries.
- One small footprint reduction: removing `System.Composition` removes its assembly-loading code paths, which historically have been a vector in plug-in-style apps.
