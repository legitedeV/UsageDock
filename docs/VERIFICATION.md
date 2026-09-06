# Verification

The source includes deterministic tests and a Core coverage gate in `scripts/test.ps1`, plus Release build and packaging scripts. The CI workflow runs on Windows; its presence is not evidence of a completed hosted CI run.

Local acceptance must record the exact test count, coverage, build result, UI evidence and artifact checks from one run. Live-provider access is separate and requires a user-configured account. No live credentials are included in fixtures.

The release workflow creates a draft release on a semantic version tag. Source publication, signing and release publication are maintainer actions. Installer creation requires an Inno Setup 6 compiler; a missing compiler must cause `-RequireInstaller` to fail rather than pretend an installer exists.

## Previous 0.1.0 desktop check (2026-09-05)

`powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-ui.ps1` exited 0 against the Release desktop build. Fourteen synthetic checks passed: account add/edit/search/favorite, theme, widget, editor rendering, credential removal, provider refresh, required credentials when changing provider, bounded refresh concurrency, skipping a queued edited account, discarding a deleted account response, and invalidating an edited account snapshot. Five WPF PNG renders were produced for dark/light dashboards, minimum dashboard size, widget and connection editor. These checks exercise the controller and real window rendering; they are not a full external mouse/keyboard automation suite.

The compiler preparation script also completed under Windows PowerShell 5.1: Inno Setup 6.7.3 was fetched from its official GitHub release, matched the pinned SHA-256 and a valid Pyrsys B.V. Authenticode signature, and was extracted in portable mode.

The 0.1.0 Core test task passed 74 of 74 tests with 97.03% line coverage (522/538 executable lines), above the 80% gate. These results use synthetic data; no live accounts were verified.


Single-instance coordination is limited to the current Windows session. Avoid running UsageDock concurrently under multiple Windows sessions for the same Windows user; that cross-session scenario is not supported by the current coordination mechanism.



## Version 0.2.0 redesign evidence (2026-09-05)

The local Core test task passed 74 of 74 tests with 97.03% line coverage. The redesign keeps the Core/controller behavior covered by that suite. Restore reported NU1900 because vulnerability-audit metadata could not be retrieved; retrying with network escalation did not resolve that warning. A passing test task is not a successful dependency vulnerability audit.

The screenshot comparison completed three visual iterations. The selected iteration measured 2.62% difference for the dashboard and 6.34% for the widget after the comparison script's antialiasing filter. Both remain above the strict less-than-2% target, so this is a measured approximation, not an exact pixel match. The reference is a small, blurred image; the original font is unidentified, and native Segoe UI rendering and blurred text colors remain known differences. These limitations do not explain away every differing pixel.

The measured reference crops are 564 x 378 and 134 x 274. WPF renders target 1128 x 756 and 268 x 548; comparison uses documented downsampling to the reference crop sizes. Public preview images contain synthetic demo accounts. Private reference analysis, overlays and intermediate renders stay outside the public package.


The final development-build UI smoke task passed 25 checks, including routed tab/search/favorite actions, preservation of a settings draft during refresh, real-account Codex labels, a dynamic account count, widget header hit testing and title-drag button guards. Seven deterministic PNG renders cover dark/light/minimum dashboards, the connection editor, the widget and both exact-size client targets. These are in-process controller and WPF rendering checks, not a full external mouse/keyboard automation suite. The public dashboard and widget previews are copied from this final post-review render.

## Version 0.3.0 tab refinement (2026-09-05)

The Core test project passed 74 of 74 tests with 97.03% executable-line coverage (261/269 in this run), above the 80% gate. Core sources, Core tests and DockController are unchanged by the tab refinement. The Release application compiled without warnings or errors.

The expanded in-process UI task passed 59 checks. Two regressions were demonstrated failing before correction: settings drafts lost on tab navigation and interval digits clipped by the input template. Passing checks now cover draft preservation during navigation and refresh; inline interval boundaries; save/revert/theme behavior; all reported allowance windows and unavailable percentages; real session-history filtering, clearing, bounding and deduplication; credential-generation and retry changes; keyboard focus restoration; horizontal scrollbar page commands; and keeping the settings save bar visible while scrolling.

Twenty-one synthetic WPF renders cover every tab in dark, light and minimum-size windows, plus unavailable statistics, empty lists, invalid settings and long account names. The final public previews use the reviewed dark render. Testing and rendering did not access real credentials or local account storage. These checks are not a full external mouse/keyboard automation suite and do not revalidate live provider access.

Review also addressed startup rollback on a failed settings save, missing focus after rebuilding navigation, input clipping and theme-inappropriate scrollbars. The previous screenshot pixel-difference metrics describe version 0.2.0 only. Version 0.3.0 deliberately expands the tab layouts and makes no new pixel-identity claim.

## Version 0.3.1 immediate theme selection (2026-09-05)

The Release build completed without warnings or errors. The expanded UI task passed 71 checks, including immediate theme changes through main-window, widget and settings controls; synchronized backgrounds and radio selection; preserved Topmost; and retaining unsaved interval, notification and startup preferences. The previously missing main theme control was demonstrated RED before implementation.

Twenty-two synthetic renders now include a light widget. Light screenshots are reached through the actual theme controls, so the saved theme and selected radio match the displayed palette. Main and widget controls persist only Theme. Core, DockController and credential handling are unchanged. These checks are offline, in-process WPF checks; no private account data enters release previews.

## Version 0.4.0 banked resets and precise times (2026-09-06)

The official Core test task passed 142 of 142 tests and enforced its coverage gate: 97.04% (788/812 executable line entries reported by the script). Synthetic fixtures cover reset inventory, authoritative counts versus capped details, status and expiry eligibility, strict timestamp parsing, fixed request routes and headers, consume outcomes, one-request dispatch, safe errors, cancellation and response limits. Date tests use fixed time and explicit time zones, including DST transitions.

The final development-build UI task passed 100 checks. These include exact date/countdown presentation, a text-only clock update that leaves history and focus intact, reset inventory states, account/expiry confirmation with No as the default, durable request IDs across uncertain results and restart, concurrent-click blocking, edit/delete guards, stale in-flight refresh rejection, authorization-error retries and rate-limit cooldowns. Twenty-five synthetic WPF PNG renders cover dark/light/minimum-size screens and the real reset-management dialog. These are in-process WPF/controller checks, not a complete external mouse/keyboard suite.

A read-only live acceptance probe using the new provider code successfully retrieved usage and reset inventory for one configured Codex account. Another configured account did not grant usage access. No real reset was consumed. Actual redemption is verified with synthetic HTTP/controller fixtures; provider-side idempotency and eligibility remain external dependencies.

A focused independent static security review found no new confirmed high/medium issues in the reset path. The application persists only the request/credit identifiers and an account binding for unresolved operations, never the credential in that journal. Unknown outcomes retain the same request identity. Local account settings and credentials are preserved by the desktop update.
