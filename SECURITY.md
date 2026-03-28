# Security Policy

## Supported Versions

VoxFlow is currently maintained as a pre-release project. Security fixes are applied to the latest state of the default branch unless maintainers explicitly state otherwise.

| Version | Supported |
|---|---|
| current default branch (`master`) | Yes |
| older commits, stale branches, and unpublished local builds | No |

## Reporting a Vulnerability

Please do not open a public GitHub issue with exploit details.

Preferred reporting path:

1. Use GitHub's private vulnerability reporting for this repository if it is available.
2. If private reporting is not available, contact the maintainer privately through GitHub before sharing details publicly.

Include as much of the following as you can:

- affected component or file path
- impact and attack scenario
- reproduction steps or proof of concept
- affected commit, branch, or version if known
- environment details such as OS, architecture, and host surface (`CLI`, `Desktop`, `MCP`, or `Core`)
- any logs, screenshots, or sample data with sensitive information removed

Do not attach real private recordings, transcripts, secrets, or credentials unless you have explicit permission and have minimized the data to what is strictly necessary.

## Response Expectations

Maintainers will try to:

- acknowledge a report within 3 business days
- provide an initial triage update within 7 business days
- coordinate on disclosure timing once severity and remediation are understood

Response times are best-effort and may vary with maintainer availability.

## Disclosure Guidance

- Give maintainers a reasonable opportunity to investigate and prepare a fix before public disclosure.
- Once a fix or mitigation is available, maintainers may disclose the issue in release notes, commit history, or a dedicated advisory.
- Reporter credit will be given when appropriate unless anonymity is requested.

## Scope Notes

Security reports are most helpful when they involve:

- privilege escalation
- path traversal or unintended file access
- command injection
- unsafe handling of local secrets or tokens
- vulnerabilities in packaging, update, or execution flows

The following are generally not treated as security vulnerabilities by themselves:

- requests for unsupported platform behavior
- local environment misconfiguration
- missing hardening for scenarios outside the documented scope
- bugs that require only the reporter's own local access and do not meaningfully increase risk
