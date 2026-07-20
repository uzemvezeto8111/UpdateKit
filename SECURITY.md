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
- Supply tokens through `UpdateClientOptions.AccessToken`, not `HttpClient.DefaultRequestHeaders`.
- Treat caller-provided HTTP handlers as trusted code; they can observe requests and must never copy authorization to an arbitrary redirect target.
- Obtain expected checksums through a trusted channel.
- Treat any file involved in a checksum or file-system error as untrusted.
- Keep .NET and the host application current with supported security updates.

## Credential and asset-download boundary

UpdateKit sends a configured bearer token only to HTTPS `api.github.com` endpoints. Release-list pagination must remain on that host. For asset downloads, the API URL must match the repository owner, repository name, and numeric asset ID returned by GitHub in the authenticated release response.

When a token is configured, UpdateKit requests asset bytes through that verified API URL with GitHub's binary media type. The initial API request receives the bearer token, user agent, and API-version header. Redirect requests created by UpdateKit receive none of those headers, are limited to ten hops, and must remain on HTTPS for an authenticated download. This also covers release checksum-file assets.

When no token is configured, UpdateKit preserves public-release behavior and uses `browser_download_url` without adding credentials. It never adds the configured access token to a browser URL, GitHub CDN URL, or arbitrary host.

The supplied `HttpClient` remains caller-owned. Standard .NET automatic redirection clears the `Authorization` header. A custom handler is trusted host code and must not defeat that behavior by copying credentials to another request. Hosts should not configure sensitive default headers on a shared client used for arbitrary URLs.
