# Contributing

This is the workflow for maintainers and agents working on this C# BepInEx plugin; the rules the code keeps are in [`AGENTS.md`](AGENTS.md), and every command is in [`HUMANS.md`](HUMANS.md).

## Before review

Run [`gate`](https://github.com/Rethunk-Tech/rethunk-gate-cli) from the repo root ([`HUMANS.md`](HUMANS.md)). That is the required check: it restores with `--locked-mode`, builds and tests Release, and runs actionlint and shellcheck. Do not bypass hooks.

## Commits

- Conventional Commits: `type(scope): subject`, one logical change each.
- Stage explicit paths only.
- The body, when there is one, says why; the diff already says what.
