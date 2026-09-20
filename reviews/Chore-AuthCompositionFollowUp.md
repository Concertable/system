# Code review — Chore/AuthCompositionFollowUp

> **This file is a work order, not a discussion.** If you're handed this file, fix the open `[ ]`
> findings directly and report what changed. Tick each `[x]` as you land it. Pause only for a genuinely
> irreversible or ambiguous finding: record its durable disposition, take the safe path, and keep going.

**Review status:** `complete`
**Reviewed up to commit:** `656b0f8`  `(2026-09-20)`
**Judgment:** `approved`

## Review pass — 2026-09-20 — composition

**Candidate base:** `a8066aec00dfe39d4f6bf7fe3632c21be4536d7c`
**Candidate head:** `656b0f8`
**Candidate branch:** `Chore/AuthCompositionFollowUp`
**Candidate scope:** `all`
**Candidate paths:** `README.md`, `compatibility/local.yaml`,
`src/Concertable.AppHost/CompatibilityManifest.cs`, `reviews/Refactor-PostgresAuthComposition.md`,
`reviews/Chore-AuthCompositionFollowUp.md` `(5 paths)`
**Work-order path:** `reviews/Chore-AuthCompositionFollowUp.md`
**Work-order mode:** `new`
**Pass judgment:** `approved`

Collects the three loose ends System #18 left behind, as the plan ledger's Next Steps step 1.

### Findings

No findings.

The one code change is a cherry-pick of `6426f6e`, written against #18's branch while that branch was
already in the merge queue and so unpushable. Re-reviewed here against `main`:

- The deleted `source:` block was **always wrong and never read**. Re-verified independently today: all
  thirteen pinned digests were resolved against GHCR and none carries the `d6f986c2` monorepo tag
  (`b2b-web` `a1baf9a7`, `customer-web` `b1b85d15`, `payment-web` `7aaf1669`, `auth` `0f80b89c`).
  `CompatibilityManifest` took `source.commit` through `Required()` into a `Commit` property with no
  consumer, and `.github/scripts/check_pins.py` never read it. A field that must be present, is always
  wrong, and is never read is exactly the straddle this project deletes.
- The deletion is complete: no `source:` key, no `SourceSection`, no `Commit` property, no constructor
  parameter, and the README sentence that repeated the claim is rewritten rather than left dangling. A
  tree-wide grep finds no surviving reference.

### Also carried

- `reviews/Refactor-PostgresAuthComposition.md`, the #18 review pass. It was written after #18 had already
  merged, so it could not ride its own PR; it lands here instead.
- **Not carried:** the two stale facts in `TECH_DEBT.md` — its worked example quotes `sha256:d888cb…`, the
  auth digest #18 replaced, and it says "three of the twelve images" where the manifest now pins thirteen.
  The skill router blocks that path for Claude (`base:docs-and-debt` is not installed), by both the shell
  and editor routes. Still owed; recorded in the plan ledger.

### Gates

Release build of `Concertable.System.slnx` 0 warnings / 0 errors; startup 4/4; architecture 4/4;
`check_pins.py` green (platform `0.2.0-alpha.0.14`, 13 images composed).
