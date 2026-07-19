# Security policy

## Supported versions

Security fixes are applied to the latest revision of the default branch. This repository does not currently maintain supported historical release branches.

## Reporting a vulnerability

Please do not disclose suspected vulnerabilities in a public issue, discussion, pull request, or test fixture.

If this repository is hosted on GitHub, use the repository's **Security** tab and choose **Report a vulnerability** to open a private security advisory. Include:

- the affected component and revision;
- the impact and likely attack scenario;
- reproducible steps or a minimal proof of concept;
- any suggested mitigation;
- whether the report is subject to a disclosure deadline.

If private vulnerability reporting is not enabled, contact a repository maintainer through a private channel and ask for a secure reporting method without including exploit details in the initial message.

Maintainers should acknowledge a complete report within five business days, keep the reporter informed while the issue is assessed, and coordinate disclosure after a fix is available. Response times may vary because this is a contributor-maintained project.

## Security expectations for integrations

- Never hard-code GitHub access tokens or include them in logs.
- Give tokens only the repository permissions required by the host application.
- Obtain expected checksums through a trusted channel.
- Treat any file involved in a checksum or file-system error as untrusted.
- Keep .NET and the host application current with supported security updates.

