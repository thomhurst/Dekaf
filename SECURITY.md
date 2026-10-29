# Security Policy

## Supported Versions

Security fixes are applied to the latest stable release and to `main`. Older
versions may require an upgrade to receive a fix.

## Reporting a Vulnerability

Report suspected vulnerabilities through a
[private GitHub security advisory](https://github.com/thomhurst/Dekaf/security/advisories/new),
or by email to the security contact,
[oss-security-scanner@tomlonghurst.com](mailto:oss-security-scanner@tomlonghurst.com).
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
Dependabot version-update configuration. Dependency review gates incoming
changes; NuGet audit reports vulnerable packages as restore warnings. Secret
scanning provides additional detection. GitHub Actions must use least-privilege token
permissions, immutable commit pins for third-party actions, isolated handling
of untrusted pull requests, and trusted-only publishing paths.

The maintainers own alert triage. A security alert is closed only after it is
fixed, shown not to affect Dekaf, or covered by an approved temporary
exception. Suppressing a finding without recorded evidence is not acceptable.

### Documentation dependency: deepmerge

Assessed on 2026-10-29 for [#4014](https://github.com/thomhurst/Dekaf/issues/4014):
[GHSA-gcqx-w48m-w2h4](https://github.com/advisories/GHSA-gcqx-w48m-w2h4)
affects `deepmerge` 4.2.0 through 4.3.1. The advisory lists no patched release;
4.3.1 is also the latest npm release on the assessment date.

`npm explain deepmerge --prefix docs` identifies one dependency chain in
`docs/package-lock.json`: `@docusaurus/preset-classic@3.10.2`,
`@docusaurus/plugin-svgr@3.10.2`, `@svgr/webpack@8.1.0`,
`@svgr/plugin-svgo@8.1.0`, then `deepmerge@4.3.1`.
SVGR's [manifest](https://github.com/gregberge/svgr/blob/v8.1.0/packages/plugin-svgo/package.json)
declares `deepmerge`, but its
[implementation](https://github.com/gregberge/svgr/blob/v8.1.0/packages/plugin-svgo/src/index.ts)
and configuration helper do not import or call it. The installed package's
`dist/index.js` likewise uses `svgo` and `cosmiconfig`, with no `deepmerge`
reference. No other installed JavaScript consumer was found.

The affected merge operation therefore receives no input, including untrusted
input, in this dependency graph. SVG contents go to `svgo.optimize`; SVGR/SVGO
configuration comes from build configuration and local configuration files,
not documentation visitor requests. The deployment workflow builds repository
content and publishes static files to GitHub Pages; it does not run a Node.js
request handler. The npm dependency is not part of Dekaf's NuGet packages.

Disposition: the vulnerable code is unreachable in the assessed toolchain.
Retain the current dependencies rather than downgrade Docusaurus or substitute
an unrelated merge API. This is a reachability assessment, not a claim that
`deepmerge` is fixed, and it does not suppress npm audit or dismiss the GitHub
alert automatically. Reassess when the dependency chain or SVG processing
configuration changes, or when a patched release becomes available.

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
