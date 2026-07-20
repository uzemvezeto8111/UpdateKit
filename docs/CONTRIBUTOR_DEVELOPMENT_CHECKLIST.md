# Contributor development checklist

Use this checklist with [CONTRIBUTING.md](../CONTRIBUTING.md) for every focused change. Not every item requires code, but every item should be considered before a pull request is opened.

## Choose and understand the work

- [ ] Search open issues and pull requests for overlapping work.
- [ ] Confirm the expected user-visible behavior and keep the scope small.
- [ ] Identify the responsible layer: GitHub transport, versioning, asset selection, downloading, verification, Core integration, WinForms UI, WPF UI/view model, sample, test, or documentation.
- [ ] Note any public API, compatibility, security, credential, file-system, accessibility, or cancellation implications.
- [ ] Ask for direction before making a breaking API or architectural change.

## Prepare the repository

- [ ] Install the .NET 8 SDK and use Windows when building WinForms and WPF projects.
- [ ] Start from the current default branch with no unrelated local changes.
- [ ] Restore the full solution:

```powershell
dotnet restore UpdateKit.sln
```

## Implement the change

- [ ] Preserve nullable reference-type correctness and warnings as errors.
- [ ] Keep responsibilities separated; do not duplicate updater logic in UI or samples.
- [ ] Use existing `UpdateResult<T>` and `UpdateErrorCode` contracts for expected operational failures.
- [ ] Keep `HttpClient` ownership explicit: UpdateKit services borrow it and do not dispose it.
- [ ] Keep downloads streaming, cancellable, temporary-file based, and safe for existing destinations.
- [ ] Avoid broad refactoring or formatting unrelated files.
- [ ] Never add hard-coded credentials, private repository data, or personal paths.

## Add deterministic verification

- [ ] Add or update tests beside every behavioral change.
- [ ] Use a custom `HttpMessageHandler`; never call the real GitHub API in automated tests.
- [ ] Use isolated temporary directories for file-system tests.
- [ ] Cover success, expected failure, cancellation, and cleanup when they are relevant.
- [ ] Keep tests deterministic across repeated runs and independent of test order.
- [ ] For WinForms work, put logic in the testable controller/state layer when practical and document any necessary manual accessibility or rendering check.

## Update public guidance

- [ ] Update XML comments for changed public APIs.
- [ ] Update README or getting-started examples when usage changes.
- [ ] Check sample code and documentation for ownership, cancellation, and error-handling accuracy.
- [ ] Add accessible names, descriptions, keyboard order, and sensible focus behavior for UI changes.
- [ ] Verify every new Markdown link and keep pending media references inactive until files exist.

## Validate before opening a pull request

- [ ] Build the entire solution in Release configuration:

```powershell
dotnet build UpdateKit.sln --configuration Release --no-restore
```

- [ ] Run every test project:

```powershell
dotnet test UpdateKit.sln --configuration Release --no-build --no-restore
```

- [ ] Run `git diff --check`.
- [ ] Confirm `git status` lists only intentional source, test, documentation, and metadata changes.
- [ ] Confirm no `bin/`, `obj/`, packages, test results, recordings, or local secrets are tracked.
- [ ] Summarize the behavior, tests, compatibility impact, and remaining limitations in the pull request.

For vulnerabilities, stop and follow [SECURITY.md](../SECURITY.md) instead of opening a public issue or pull request with exploit details.
