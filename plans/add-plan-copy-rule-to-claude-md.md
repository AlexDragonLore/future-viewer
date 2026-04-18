# Plan: Add "copy plan to repo" rule to CLAUDE.md

## Context
The user has a feedback memory requiring every plan to be saved into `plans/` in the repo root with a descriptive name. However, this rule lives only in auto-memory, which isn't always reliably applied. Adding it to `CLAUDE.md` ensures it is always enforced — CLAUDE.md is loaded into every conversation as a hard instruction.

## Changes

### 1. Create `CLAUDE.md` in repo root
Path: `/Users/aleksandr/RiderProjects/future-viewer/CLAUDE.md`

Add a rule:

```markdown
# Project Rules

## Plans
Every finalized /plan must be copied into `plans/` in the repo root:
- Use a descriptive kebab-case filename reflecting the plan topic (e.g. `streaming-ai-parallel-animation.md`), not the auto-generated Claude plan filename.
- Copy the plan right after ExitPlanMode is approved, before starting implementation.
```

No other files need to change. The `plans/` directory already exists.

## Verification
- Run `/plan` for any task — after ExitPlanMode the plan should appear in `plans/` with a descriptive name.
