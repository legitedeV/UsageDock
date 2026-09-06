# Contributing

Use Windows 11 and the .NET 8 SDK. `global.json` selects .NET 8 while permitting newer feature bands. Keep changes focused and include the behavior being fixed and how you verified it in the pull request.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1` and `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1` before proposing a change. Tests use xUnit and coverlet; Core line coverage must be at least 80%. Artifacts are written under `artifacts/`, excluded from Git.

Write a failing test for new parser or connection behavior first. Use synthetic fixture values and injected HTTP handlers; tests must not require a real account, internet access to provider endpoints, or local CLI credentials. Cover missing versus zero data, provider schema changes, pagination, 401/403, 429, cancellation and account identity changes. Never commit credentials or real HTTP response captures.

`src/UsageDock.Core` owns models, provider logic and encrypted local persistence. `src/UsageDock.App` owns WPF and Windows integration. `tests/UsageDock.Core.Tests` owns deterministic tests. Preserve immutable Core models and keep credentials out of view models and diagnostic text wherever possible. New integrations need documented prerequisites and clear unavailable states.

For UI changes, run the desktop application and inspect the dashboard, connection editor and widget at common Windows scaling settings. Verify keyboard focus and light/dark themes. Screenshots must use demo data.

Packaging is performed by `scripts/package.ps1`; optional Inno Setup 6 produces a per-user installer. A version tag triggers a draft release only after checks. Do not describe fixture validation as a live-provider test, and do not claim release signing unless a signing pipeline is actually configured.

The release workflow downloads the pinned official Inno Setup 6.7.3 compiler, checks SHA-256 and its Authenticode publisher, and extracts it in portable mode under `artifacts/tools`. It does not register a machine-wide compiler installation. To prepare the same compiler locally, run `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\get-inno.ps1`, then pass its printed path to `package.ps1`.

After a Release build, `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-ui.ps1` runs the synthetic desktop smoke flow and captures the dashboard, editor and widget. Pass `-Executable <path>` to verify a packaged executable. Successful rendering does not replace human inspection of these images or real-account verification.
