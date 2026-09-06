# Integration contracts

Credentials remain in the desktop application. Only explicit configured provider origins receive requests; automatic redirects and implicit cookie storage must remain disabled. Provider errors are mapped to safe messages, and previous successful snapshots remain visibly stale on failure.

## Experimental subscriptions

Claude OAuth reads `/api/oauth/usage` from `api.anthropic.com`. Claude web sessions use `claude.ai` with an explicit organization and session key. Codex reads `/backend-api/wham/usage` from `chatgpt.com`, with an account ID when required. These contracts are observed integrations rather than public stability promises. Additional windows and absent fields are handled explicitly. Access tokens are imported or entered, not acquired by a built-in OAuth flow, and automatic renewal is not claimed.

The Codex connection displays Codex allowances associated with a ChatGPT account. There is no claimed general ChatGPT conversation quota feed.

## Official API reporting

[Anthropic costs](https://platform.claude.com/docs/en/api/admin/cost_report/retrieve) report fractional cents, converted to dollars. Costs are grouped by workspace and filtered locally; message usage uses the documented workspace filter. Token totals include reported uncached input, cache-read, cache-creation categories and output. Search request counts are not inferred to mean total requests.

[OpenAI organization usage](https://developers.openai.com/api/reference/resources/admin/subresources/organization/subresources/usage) and costs use Admin API credentials. Project filters use array query keys. Cost values are dollars; completions tokens are labelled separately from total platform usage.

Both API views use UTC month-to-date boundaries, bounded pagination and decimal money. Local budget settings never alter provider limits. Admin-key authorization and available reporting data vary; real-account acceptance requires the user to connect the relevant account.

## Change policy

Add a failing synthetic fixture before changing a parser. Preserve unavailable versus zero, prevent duplicate cursor loops, bound response size and never log raw credentials or provider bodies. A connection generation change must invalidate late responses after edit/delete. Do not reuse snapshots across identities.

## Banked Codex resets

Codex reset inventory is read from `GET https://chatgpt.com/backend-api/wham/rate-limit-reset-credits`. Explicit redemption uses `POST` to the same path plus `/consume`, with `redeem_request_id` and an optional `credit_id`. Both use the configured bearer credential and `ChatGPT-Account-Id`; they never target a caller-supplied URL. See the [official Codex client implementation](https://github.com/openai/codex/blob/main/codex-rs/backend-client/src/client/rate_limit_resets.rs) and [banked reset explanation](https://help.openai.com/en/articles/20001498-how-banked-codex-resets-work). This client contract is experimental and is not a stable public API guarantee.

`available_count` is independent of the possibly capped credit list. Missing inventory is unavailable, not zero. Individual entries retain status, type, grant date and expiry; an explicitly null expiry differs from an absent or invalid expiry. Only recognized available reset credits with a known valid expiry policy can be selected for redemption. Inventory failure must not discard a successful usage reading.

Redemption is a manual action with an account-specific confirmation. It can refresh eligible five-hour and weekly windows and change the weekly reset date. It does not buy extra credits. One logical attempt retains one UUID and selected credit across uncertain outcomes; no automatic POST retry is allowed. Provider replies `reset` and `already_redeemed` are distinguished from `nothing_to_reset`, `no_credit` and an uncertain response. Updated usage and inventory remain the source of truth; counters are never decremented optimistically.

Times show the local calendar date and time together with a countdown. Absolute timestamps include the UTC offset at the target date, including daylight-saving changes. Expired timestamps request a fresh reading instead of implying that the vendor has already renewed the allowance.
