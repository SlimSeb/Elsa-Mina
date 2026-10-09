# Contributing

Contributions are welcome.

If you want to contribute, just open a Pull Request and I’ll take a look.

## Guidelines

- Keep changes focused and minimal.
- Try to follow the existing code style and structure.
- If your change is significant, consider briefly explaining **what** and **why** in the PR description.
- Make sure the project builds and tests pass (if applicable).
- Make sure `./scripts/Build/code-analysis.sh` reports no Sonar issue: CI runs it on every pull request.
  To run it before each commit, enable the pre-commit hook once with `git config core.hooksPath scripts/hooks`.

## Questions

If you are unsure about something, feel free to open an issue or a draft PR for discussion.
