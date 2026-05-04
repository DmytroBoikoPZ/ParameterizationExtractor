# Desktop Seed Tab — Root Inference — Architecture Overview

> Tiny UX patch that closes the operator's "what do I do here?" gap on the Seed tab. The SQL editor becomes the primary input; the Root picker auto-fills from the SQL and surfaces a non-blocking mismatch hint when the operator's manual pick disagrees with what the query targets.

---

## 1. Pipeline / Integration

```
Operator types into SQL editor
   ↓
SqlEditor.Text (DP)  ↔  ScriptEditorViewModel.SeedQuery (CTK [ObservableProperty])
   ↓ partial OnSeedQueryChanged(value)
SeedQueryParser.TryExtract(value)              (NEW, in Logic)
   │
   ├── returns SeedRoot(Schema, Name)?
   │
   ├── if root is null → no-op
   │
   ├── if VM.RootSchema/.RootTable both empty
   │      → set them from parsed root (auto-populate)
   │      → clear RootMismatchHint
   │
   ├── if VM root MATCHES parsed root (case-insensitive)
   │      → clear RootMismatchHint
   │
   └── if VM root non-empty AND DOESN'T match parsed root
          → RootMismatchHint = "SQL targets {parsed}; picked root is {vm}"
          → (do NOT auto-overwrite — operator decides)

Operator picks root from picker (TablePicker or inline ComboBox)
   ↓ partial OnRootRefChanged(value) (existing setter)
   → clear RootMismatchHint                    (NEW)
   → existing save-on-blur fires unchanged
```

| Stage | What changes | Where |
|-------|--------------|-------|
| Engine helper | `SeedQueryParser.TryExtract` static method | `Logic/Helpers/` |
| Desktop VM | `ScriptEditorViewModel.OnSeedQueryChanged` partial method gains parser call + hint logic; new `RootMismatchHint` observable | `Desktop/Views/Seed/ScriptEditorViewModel.cs` |
| Desktop view | `SeedView.xaml` adds inline hint banner; minor relabel of Root picker | `Desktop/Views/Seed/SeedView.xaml` |

---

## 2. Parser contract

```csharp
namespace Quipu.ParameterizationExtractor.Logic.Helpers;

public static class SeedQueryParser
{
    /// <summary>
    /// Extracts the first <c>FROM &lt;schema&gt;.&lt;table&gt;</c> (or bare <c>FROM &lt;table&gt;</c>)
    /// from a SQL string. Returns null if no FROM is found, the SQL is empty / whitespace,
    /// or the FROM is inside a single-line comment / string literal.
    /// </summary>
    public static SeedRoot? TryExtract(string? sql);
}

public sealed record SeedRoot(string Schema, string Name);
```

**Parsing rules:**

- Strip line comments (`-- …`) and block comments (`/* … */`) before scanning.
- Find the first occurrence of `FROM` (case-insensitive, whole-word — must be preceded by whitespace / start, followed by whitespace).
- After `FROM`, capture the next identifier:
  - `[<schema>].[<table>]` → `(<schema>, <table>)`
  - `<schema>.<table>` → `(<schema>, <table>)` (where each part may or may not be bracketed)
  - `[<table>]` → `("", <table>)`
  - `<table>` → `("", <table>)`
- Identifier characters: letters, digits, underscore, `#` (temp), `@` (table variable). `$` is rare but accepted.
- Stop at first whitespace, comma, semicolon, parenthesis, or newline after the identifier.
- CTE handling: `WITH … SELECT … FROM <real-table>` works because we strip past the `WITH` block by scanning for the matched closing parenthesis sequence + the trailing SELECT. **Decision:** Skip CTE handling in v1 — if the FROM after WITH is a CTE name (not a real table) the parsed value won't match the engine's expected table. Document the limitation; operator can override via the picker.
- Subquery `FROM (SELECT ... FROM real_table)` → parser returns the outermost identifier-style target; if the next token after `FROM` is `(`, return null (operator clearly intends something complex).

The parser is **regex-based** for v1 — fast, predictable, easy to reason about. If edge cases pile up, swap to a tiny token scanner. No FParsec; this is hot UI path code and FParsec adds startup cost.

---

## 3. Mismatch hint state machine

```
RootMismatchHint state diagram:

  empty ────(SQL changes; parsed root MATCHES VM root)───→ empty
  empty ────(SQL changes; parsed root DIFFERS from VM root)──→ "SQL targets X; picked root is Y"
  empty ────(SQL changes; parsed root null OR VM root empty)──→ empty
  hint  ────(SQL changes; parsed root MATCHES VM root)───→ empty
  hint  ────(SQL changes; parsed root DIFFERS from VM root)──→ updated hint
  hint  ────(operator picks root from picker)─────────────→ empty
  hint  ────(operator clicks dismiss button)──────────────→ empty
```

The hint is **informational** — never blocks Save / Run preview / engine extraction. The operator's authoritative root is `RootSchema` + `RootTable`; the SQL is preview-only persistence (per the engine seam contract from `desktop-seed-tab`).

---

## 4. Auto-populate vs override

When the operator opens a fresh script (root empty), parser-derived root **silently fills** the picker. This matches the user's mental model: "I write a SELECT against a table, and the tool figures out which table I'm querying."

When the operator manually picks a root via the combobox, that pick is **sticky** — subsequent SQL changes don't auto-overwrite it. They only surface the mismatch hint. Rationale: a multi-table SQL legitimately targets multiple tables; the operator's picked root is the engine's seed entry point and should not change without explicit consent.

---

## 5. Threading & cancellation

Parser runs **synchronously** on every keystroke (the SQL editor's text-change event). It's a regex over a string of bounded size — sub-millisecond. No debouncing needed at the parser; the existing 500ms save-on-blur debounce in `ScriptEditorViewModel` is unaffected.

Parser is pure / stateless / thread-safe. Tests can call it directly without DI.

---

## 6. Risks & open questions

- **CTE / subquery edge cases:** parser will return the wrong identifier on `WITH cte AS (...) SELECT * FROM cte`. Document; operator overrides via picker.
- **Bracketed identifiers with `]` characters:** `[my]]table]` (escaped `]`) is technically legal SQL Server. Parser doesn't unescape; treats as `my` + invalid. Acceptable v1 edge — extremely rare.
- **Cross-DB references (`<db>.<schema>.<table>`):** parser captures last two segments only. Engine doesn't support cross-DB seeds anyway.
- **Mismatch hint UX:** could grow into a "fix it" button that overwrites the picker. Defer; surface the disagreement and let the operator choose.

---

## 7. Out of scope

- No engine-side parsing — the engine still expects `RootSchema` + `RootTable` populated; the parser is UI-only.
- No multi-root inference — first `FROM` only.
- No SQL syntax validation (the engine's preview run still surfaces "Invalid object name" errors).
- No auto-fix of the picker on mismatch.
