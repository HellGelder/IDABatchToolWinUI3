---
name: graphify
description: "Use for ANY question about the codebase, its architecture, file relationships, or project content. If graphify-out/graph.json exists, treat the question as a graphify query first: run `graphify query \"<question>\"` (add --dfs to trace a chain, --budget N to cap tokens) and answer from the result instead of reading whole files — this saves tokens. Also builds a persistent knowledge graph from a folder of files (tree-sitter AST, no LLM): `graphify <path>` produces graphify-out/ with graph.json, GRAPH_REPORT.md and graph.html."
license: Apache-2.0
metadata:
  author: https://github.com/Graphify-Labs/graphify
  source-url: https://github.com/Graphify-Labs/graphify
  adapted-for: ZCode
---

# /graphify

Turn a folder of code/docs into a queryable knowledge graph (no LLM, tree-sitter AST). Three outputs: interactive `graph.html`, GraphRAG-ready `graph.json`, plain-language `GRAPH_REPORT.md`.

## Fast path — existing graph (do this first)

If `graphify-out/graph.json` exists relative to the project root your commands run from:

1. For a natural-language question about the codebase — **run `graphify query "<question>"` immediately**. Do not rebuild, do not detect files, do not ask the user to narrow.
   - BFS (default) = broad context ("what is X connected to?"); `--dfs` = trace a specific chain; `--budget N` = cap answer at N tokens.
2. Load `references/query.md` and follow it fully (vocabulary expansion, inline traversal fallback) when the CLI is unavailable or the result is thin.
3. `graphify path "A" "B"` — shortest path between two concepts; `graphify explain "Concept"` — plain-language explanation of a node.
4. Answer using only what the graph contains; quote `source_location` when citing a specific fact. Say plainly if the graph lacks the info — do not hallucinate edges.

## Build path — no graph yet

If `graphify-out/graph.json` does not exist:

1. Ensure the CLI: `uv tool install graphifyy` (preferred; fallback `pipx install graphifyy` or `pip install graphifyy`; note the package is **graphifyy**, the command is `graphify`).
2. Build: `graphify <path>` (default `.`), e.g. `graphify . --mode deep` for richer INFERRED edges. Output lands in `graphify-out/` (graph.json + GRAPH_REPORT.md + graph.html). `.gitignore` is respected automatically; add `.graphifyignore` to exclude more.
3. `graphify . --update` — incremental re-extract of new/changed files; `graphify . --watch` — auto-rebuild on code changes.
4. The graph is meant to be committed to git so the whole team starts with a ready map. `GRAPH_REPORT.md` answers broad architecture questions without re-reading code.

## When invoked

- `graphify query` / `path` / `explain` — use the Fast path above; load `references/query.md` for the full traversal flow.
- Bare `/graphify` or `/graphify <path>` — build/update the graph (Build path).