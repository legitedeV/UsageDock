# UsageDock product

## Audience and job

People who use multiple personal, team, or organizational AI accounts need one place to inspect available usage windows and API spending. UsageDock is a native Windows 11 x64 desktop application with a dashboard, a compact pinned widget, and a tray presence. Its purpose is monitoring; it never pools allowances, purchases credits, or switches the identity used by another CLI.

## Approved scope

- Multiple Claude subscriptions and Codex/ChatGPT accounts.
- Multiple Anthropic API organizations/workspaces and OpenAI API organizations/projects.
- Separate presentation of subscription windows and API month-to-date spending.
- Add, edit, refresh, favorite and remove connections; selected credential-file import.
- Widget, tray, theme preferences and optional usage notifications.
- Local settings and DPAPI-protected credentials, no UsageDock backend or telemetry.
- MIT source, documented limitations, reproducible scripts and Windows artifacts.

## Truthfulness

Zero is a known value. Missing data is unknown. Stale data retains its last successful retrieval time. A failed request must not look like unused allowance. Codex usage must not be described as a general ChatGPT chat quota. API budgets are user-chosen local thresholds. Every connection describes its prerequisites and whether it relies on an experimental endpoint.

## Acceptance

Core logic is tested with deterministic fixtures and at least 80% line coverage. A real WPF render and UI flow check complement unit tests. Release packaging must produce actual runnable artifacts. Live account access, code signing and public publication are independent from fixture/build acceptance and must never be implied by it.
