# Security

UsageDock stores sensitive provider credentials. Windows DPAPI CurrentUser encrypts saved secrets at rest; it does not protect against malware running as your Windows user or an unlocked desktop session. Do not use the app on shared or untrusted Windows accounts.

Subscription adapters use experimental endpoints and existing credentials. They are not official third-party OAuth integrations and do not provide automatic credential refresh. Only connect accounts you own or have permission to administer. API reporting keys can have broad administrative access; use the least privileges the provider permits and revoke credentials at the provider if exposed.

Never attach access tokens, session keys, authorization headers, local credential stores, or unredacted provider responses to an issue. Public release binaries are currently unsigned. Compare checksums only when the checksum source itself is trusted.

## Reporting

Use [GitHub private vulnerability reporting](https://github.com/legitedeV/UsageDock/security/advisories/new) through the Security tab. Forks should enable their own private reporting or provide a private contact. Do not disclose an exploitable issue or a secret in a public issue. Include the affected version, a synthetic reproduction, likely impact, and suggested mitigation.

No response-time guarantee or supported-version service-level agreement is currently offered. Review the latest release notes before reporting a fixed issue.
