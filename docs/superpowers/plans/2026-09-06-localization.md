# Approved localization implementation

Languages: pl, en, de, fr, es only. New installations use Auto (Windows UI language, English fallback); existing settings missing Language retain Polish.

1. RED: pin new settings default and legacy migration; add resolution, catalog parity, time/number/budget tests.
2. Core foundation: immutable embedded JSON catalogs, independent preference/resolved language, culture and change event, localized time overloads, safe budget parser. Protocol data remain invariant.
3. UI: translate all surfaces, tray, dialogs, history and errors; preserve navigation, search, focus and unsaved settings. Translate pre-existing history at render time.
4. Installer: five built-in languages and localized custom messages (root ownership).
5. Verify: core >=80% line coverage, existing 104 UI checks plus localization regressions, five-language light/dark screenshots and long-label minimum-size checks. Preserve Capture helper.

Ownership: foundation executor owns src/UsageDock.Core and tests/UsageDock.Core.Tests during initial stage. Root coordinates later UI migration and owns packaging/documentation outside this plan. No commits, publishing, real-provider reads or reset consumption.

## Stage 1 evidence

- RED: new-settings auto and legacy-settings pl tests both failed against original implementation; later Auto display-culture regression failed before fix.
- GREEN: scripts/test.ps1 passed 195 tests, Core line coverage 97.46% (920/944), artifacts/tests/bffaa9cedda243698469178a64bb5e7b.
- Core foundation and 13 initial semantic catalog keys implemented. UI remains deliberately unmigrated in this stage.
- Review corrected InstalledUICulture to CurrentUICulture. App must not replace the thread UI culture; use Localization.Culture explicitly for display formatting. SetLanguage is UI-dispatcher-only.
- Remaining: every UI surface/catalog expansion, language setting live persistence/draft preservation, tray, history, dialogs/errors, UI smoke and screenshot matrix. Original UsageTime overloads pin pl; migrate UI calls to overloads with Localization.CurrentLanguage.

## Stage 2 evidence

- RED UI smoke: Missing control Settings.Language before implementation.
- Explicit source-key Ui.L calls implemented across main views, settings, widget, editor, reset/details dialogs and tray. Logical IDs, provider payloads, account names and reset journal code remain unchanged.
- PL/EN catalogs frozen at 278 keys. Root/helper own completing DE/FR/ES translations; those languages currently use English fallback for new keys.
- RED native-name regression exposed record ToString; override added and real popup text now verified.
- UI smoke: 159 checks, including prior 104 and 55 new language/draft/search/focus/history/editor/reset/tray checks. scripts/verify-ui.ps1 passed with five-language fixture matrix in artifacts/ui/0b4f2c5bda564673b05e3055fce4e127.
- Inspected English main/settings and language-options renders. Native dropdown names render correctly.
- Final remaining work: complete three translated catalogs, rerun Core parity/coverage and UI fixtures against full translations, review long-text layout and resolve final review findings.
