# Security Policy

## Supported Versions

Security fixes are applied to the latest stable release and to `main`. Older
versions may require an upgrade to receive a fix.

## Reporting a Vulnerability

Report suspected vulnerabilities through a
[private GitHub security advisory](https://github.com/thomhurst/Dekaf/security/advisories/new).
Do not open a public issue or disclose exploit details before a fix is
available. Include affected versions, impact, reproduction steps, and any
known mitigations.

The maintainers own intake, severity assessment, remediation, disclosure, and
release coordination. We target initial triage within these timeframes:

| Severity | Examples | Initial triage target | Remediation target |
| --- | --- | --- | --- |
| Critical | Active exploitation, arbitrary code execution, credential compromise | 1 business day | 7 days |
| High | Practical authentication bypass, sensitive data exposure | 3 business days | 30 days |
| Moderate | Exploitation requires uncommon conditions or limited access | 7 business days | 90 days |
| Low | Defense-in-depth issue with limited security impact | 14 business days | Next planned release |

These are targets, not disclosure deadlines. Severity considers exploitability,
impact, affected deployments, and available mitigations. Maintainers may raise
or lower priority as evidence changes and will coordinate disclosure timing
with the reporter.

If a credential may be exposed, revoke or rotate it immediately. Repository
cleanup and alert dismissal happen only after the credential is invalidated.

## Dependency and Workflow Security

Renovate is the dependency update service for this repository; do not add a
Dependabot version-update configuration. Dependency review and NuGet
vulnerability auditing gate incoming changes. CodeQL and secret scanning
provide additional detection. GitHub Actions must use least-privilege token
permissions, immutable commit pins for third-party actions, isolated handling
of untrusted pull requests, and trusted-only publishing paths.

The maintainers own alert triage. A security alert is closed only after it is
fixed, shown not to affect Dekaf, or covered by an approved temporary
exception. Suppressing a finding without recorded evidence is not acceptable.

## Security Exceptions

Every temporary exception must record:

- the exact advisory, package, workflow, or rule being excepted;
- affected versions and reachability or exploitability evidence;
- compensating controls and residual risk;
- an accountable maintainer and approval reference;
- an expiry date and concrete removal condition.

Exceptions must be as narrow as possible. Their owner must review them before
expiry and remove them when the dependency is upgraded, the vulnerable path is
eliminated, or the stated removal condition is met. Expired exceptions fail
closed: renew them with fresh evidence and approval or remove them.
