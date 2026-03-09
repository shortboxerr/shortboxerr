---
name: continue-dev
description: Run the next development iteration following the project's iteration protocol. Use when the user types "continue-dev", "continue", or asks to run the next iteration.
---

# Continue Development Iteration

Run the next iteration per the project's iteration protocol.

## Instructions

When triggered, execute the full iteration workflow defined in `docs/PROMPTS/CONTINUE.md`:

1. **Read the continuation prompt** at `docs/PROMPTS/CONTINUE.md`
2. **Follow ALL instructions** in that file, including:
   - Quality gates (build check, test baseline)
   - Pull next READY items from `docs/BACKLOG.md`
   - Implement vertical slice with code+tests+docs
   - Update `docs/WORKLOG.md`, `docs/BACKLOG.md`, `docs/SELF_CHECK.md`
   - Commit after logical breakpoints

## Quick Reference

Key files:
- `docs/PROMPTS/CONTINUE.md` - Main iteration protocol
- `docs/BACKLOG.md` - Work items (find READY items)
- `docs/WORKLOG.md` - Iteration history
- `docs/SELF_CHECK.md` - Status template

## Critical Rules

1. **ALL tests must pass before committing** - Fix failures first
2. **Never mask bugs** - Understand test failures before changing tests
3. **Commit messages** - Use conventional commits (feat:, fix:, chore:, etc.)
4. **Document decisions** - Log assumptions in `docs/ASSUMPTIONS.md`
