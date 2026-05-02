# {Feature Name} — Architecture Overview

> Copy this file to `ongoing-tasks/{feature-name}/feature-architecture.md`.
> Fill in each section before or during the first step.
> This document gives every contributor (human and AI) a shared mental model of what the feature builds, how data flows, and where the pieces live.

---

## 1. Pipeline / Integration

Describe how this feature fits into the system's data or request flow. If the feature is a standalone service, describe its request/response lifecycle. If it feeds an existing pipeline, name the upstream and downstream stages.

```
(Replace with an ASCII or Mermaid diagram of the pipeline stages)
```

| Stage | What happens | Where |
|-------|--------------|-------|
| 1. … | … | … |
| 2. … | … | … |

---

## 2. Component Diagram

Show every service, store, queue, and external dependency this feature touches. Mark which are new vs. existing.

```
(Replace with an ASCII or Mermaid diagram of services, stores, and their connections)
```

---

## 3. Data Flow

### 3.1 Inbound / Ingestion

```
(Diagram: how data enters this feature — polling, webhooks, API calls, events, etc.)
```

### 3.2 Processing / Event Handling

```
(Diagram: how data flows through handlers, what each handler produces)
```

### 3.3 Query / Serving

```
(Diagram: how consumers retrieve the result — HTTP, RPC, internal call, event)
```

---

## 4. Data Stores Summary

| Store | Technology | Collections / Tables / Indices | Purpose |
|-------|------------|---------------------------------|---------|
| … | … | … | … |

---

## 5. Extension Points

Describe how new adapters, handlers, or capabilities plug in to this feature without modifying existing code.

---

## 6. Security & Isolation

- Authentication / authorization model
- Network boundaries (internal-only, ingress-exposed, external)
- Credential management (secret store, rotation)
- Trust boundaries (e.g., LLM output treated as untrusted, user-supplied content sanitized before rendering)
