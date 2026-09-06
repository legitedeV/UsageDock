# Changelog

## 0.5.0 — 2026-09-06

- Five interface and installer languages: English, Polish, German, French and Spanish.
- Automatic Windows language detection, a saved language preference and live interface switching.
- Localized dates, countdowns and numbers, with unambiguous decimal budget input.
- Five translated README pages with language navigation and an animated language tour.

Verified with 196 Core tests (97.46% line coverage) and 161 separate UI checks. Existing accounts and preferences are preserved.

## 0.4.1 — 2026-09-06

First public downloadable release.

- Reproducible screenshot rendering on CI machines with smaller virtual displays.
- Animated README tour, light/dark previews and accessible still-image alternatives.
- Issue forms, pull request template and private vulnerability reporting.

All application features from 0.4.0 are included.

## 0.4.0 — 2026-09-06

Initial desktop build of UsageDock for Windows 11 x64.

- Multiple Claude and Codex connections in a native dashboard and pinned desktop widget.
- API costs and reported tokens, with optional local budgets and workspace/project filters.
- Banked Codex reset counts, grant/expiry dates and explicit redemption with confirmation.
- Durable retry identity for uncertain reset results and protection against duplicate dispatch.
- Exact local reset timestamps and countdowns that update without network polling.
- Light and dark themes, statistics, session history and grouped settings.
- Self-contained portable ZIP and per-user installer.

### Verification

142 Core tests and 100 separate in-process UI checks passed locally for this version. Core coverage was 97.04% under the project's coverage task. Twenty-five synthetic screenshots were inspected. A live read-only Codex inventory check succeeded; actual reset consumption was tested only with fixtures.

### Known limitations

The interface is currently Polish. Subscription endpoints are experimental. Automatic credential renewal, general ChatGPT conversation quotas and persistent historical billing are not implemented. Release binaries are unsigned.
