# Good first issue candidates

These eight self-contained tasks are suitable for GitHub's `good first issue` label. They describe proposed work only; they do not represent existing contributors, assignments, pull requests, or issue history.

## 1. Add an UpdateErrorCode troubleshooting table

**Suggested labels:** `good first issue`, `documentation`

### Context

The README explains how to branch on `UpdateErrorCode`, but a first-time integrator must still search source code to learn the likely cause and recommended user action for each code.

### Expected behavior

Add a concise troubleshooting table that lists every public `UpdateErrorCode`, what it generally means, and the appropriate caller or end-user response. Keep messages general and describe only the documented bounded download-retry policy; do not invent additional retryable categories.

### Likely files

- `README.md`
- `docs/GETTING_STARTED.md`
- `src/UpdateKit.Core/Results/UpdateErrorCode.cs` as the source of truth

### Acceptance criteria

- [ ] Every current public error code appears exactly once.
- [ ] Each row distinguishes retryable, configuration, cancellation, and integrity failures where appropriate.
- [ ] `ChecksumMismatch` and `FileSystemError` guidance treats affected files as untrusted.
- [ ] No runtime behavior or public API changes.
- [ ] All Markdown links and `git diff --check` pass.

## 2. Add a dedicated README for the minimal WinForms sample

**Suggested labels:** `good first issue`, `documentation`

### Context

The root README contains the complete minimal integration, but someone browsing directly to the sample directory has no local explanation of its five host-specific values or its relationship to the larger configurable example.

### Expected behavior

Add a short sample-local README explaining how to run it, which five values must be changed, why `MainForm` owns `HttpClient`, and when to use the full example instead.

### Likely files

- `samples/UpdateKit.Minimal.WinForms/README.md`
- `README.md`
- `samples/UpdateKit.Minimal.WinForms/MainForm.cs` for verified names and behavior

### Acceptance criteria

- [ ] Includes an exact `dotnet run` command from the repository root.
- [ ] Lists repository owner, repository name, current version, asset extension, and destination path.
- [ ] Explains that `UpdateClient` borrows and does not dispose `HttpClient`.
- [ ] Links back to the root five-minute guide and the configurable example.
- [ ] Adds no duplicated updater implementation or generated output.

## 3. Test rate-limit responses with an absolute Retry-After date

**Suggested labels:** `good first issue`, `tests`

### Context

`GitHubReleaseSource` supports `Retry-After` as both a delay and an absolute date. Existing deterministic coverage verifies the delay form and `X-RateLimit-Reset`, but not the absolute-date branch.

### Expected behavior

Add a deterministic HTTP-handler test proving that a rate-limited response with `RetryAfter.Date` returns `UpdateErrorCode.RateLimitExceeded` and includes the expected retry timestamp in the message.

### Likely files

- `tests/UpdateKit.Core.Tests/GitHub/GitHubReleaseSourceTests.cs`
- `tests/UpdateKit.Core.Tests/Http/StubHttpMessageHandler.cs`
- `src/UpdateKit.Core/GitHub/GitHubReleaseSource.cs` only if the test exposes a defect

### Acceptance criteria

- [ ] Uses a fixed `DateTimeOffset`, not the current clock.
- [ ] Uses the custom handler and performs no real network request.
- [ ] Asserts the error code and stable timestamp content without depending on local culture.
- [ ] Existing delay and reset-header tests remain unchanged and passing.
- [ ] Full Release build and test suite pass.

## 4. Test successful zero-byte asset downloads

**Suggested labels:** `good first issue`, `tests`

### Context

Download tests cover known and unknown nonzero lengths, replacement, cancellation, stream failures, and cleanup. A legitimate zero-byte asset with `Content-Length: 0` is not called out explicitly.

### Expected behavior

Add a deterministic test demonstrating that a successful zero-byte response creates or replaces the destination with an empty file, returns zero downloaded bytes, reports final progress consistently, and leaves no UpdateKit temporary file.

### Likely files

- `tests/UpdateKit.Core.Tests/Downloading/AssetDownloaderTests.cs`
- `tests/UpdateKit.Core.Tests/IO/TemporaryDirectory.cs`
- `src/UpdateKit.Core/Downloading/AssetDownloader.cs` only if the test exposes a defect

### Acceptance criteria

- [ ] Uses an in-memory custom HTTP response with explicit `Content-Length: 0`.
- [ ] Asserts a successful `DownloadResult` with `BytesDownloaded == 0`.
- [ ] Asserts the destination exists, is empty, and any prior content was safely replaced.
- [ ] Asserts progress semantics without inventing a percentage for a zero total.
- [ ] Asserts no `.updatekit-*.tmp` file remains.

## 5. Define explicit keyboard tab order for the update dialog

**Suggested labels:** `good first issue`, `accessibility`, `winforms`

### Context

The update dialog supports keyboard activation and accessible names, but its interactive controls rely on implicit creation order rather than documented `TabIndex` values. Explicit order is easier to review and protects keyboard navigation as the layout evolves.

### Expected behavior

Set a logical tab sequence for release notes, the primary action, Cancel, and Close. Noninteractive display labels and progress controls should not unexpectedly receive focus.

### Likely files

- `src/UpdateKit.WinForms/UpdateDialog.cs`
- `tests/UpdateKit.WinForms.Tests/UpdateDialogControllerTests.cs` for state expectations
- `docs/CONTRIBUTOR_DEVELOPMENT_CHECKLIST.md` only if manual verification guidance improves

### Acceptance criteria

- [ ] Tab order is explicit, stable, and follows the visual reading/action order.
- [ ] Enter and Escape retain their existing state-dependent behavior.
- [ ] No control becomes actionable while its corresponding state disallows the action.
- [ ] Keyboard navigation is manually checked in initial, update-available, downloading, and completed states.
- [ ] Existing deterministic controller tests and the full solution build pass.

## 6. Improve accessible descriptions for dynamic dialog status

**Suggested labels:** `good first issue`, `accessibility`, `winforms`

### Context

The dialog assigns accessible names to its status, progress, bytes, and error controls, but descriptions do not explain how these dynamic regions relate to the current update operation.

### Expected behavior

Add concise accessible descriptions—and an appropriate built-in live or alert setting where WinForms supports it—so assistive technology can distinguish normal status, progress, and actionable error content without custom native interop.

### Likely files

- `src/UpdateKit.WinForms/UpdateDialog.cs`
- `src/UpdateKit.WinForms/Internal/UpdateDialogViewState.cs`
- `tests/UpdateKit.WinForms.Tests/UpdateDialogControllerTests.cs`

### Acceptance criteria

- [ ] Status, progress, downloaded-byte, and error controls have nonredundant descriptions.
- [ ] Dynamic error content is identifiable without relying on color alone.
- [ ] Uses standard WinForms accessibility properties only.
- [ ] Existing visual text and controller state transitions remain unchanged.
- [ ] Includes a short manual Narrator verification note in the pull request.

## 7. Add TryGetValue to UpdateResult<T>

**Suggested labels:** `good first issue`, `api`, `tests`

### Context

Callers currently branch on `IsSuccess` before reading `Value` or `Error`; reading the wrong property throws by design. A conventional `TryGetValue` helper would make simple success-only flows easier while preserving the existing result contract.

### Expected behavior

Add an additive, nullable-annotated `TryGetValue` method that returns `true` and the non-null value for success, or `false` and the default value for failure. Existing properties and factories must remain unchanged.

### Likely files

- `src/UpdateKit.Core/Results/UpdateResult.cs`
- `tests/UpdateKit.Core.Tests/Results/UpdateResultTests.cs`
- `README.md` or `docs/GETTING_STARTED.md`

### Acceptance criteria

- [ ] The method has correct nullable flow-analysis annotations.
- [ ] Success and failure paths have deterministic tests.
- [ ] No existing exception, factory, or `IsSuccess` behavior changes.
- [ ] XML documentation describes the out value on both branches.
- [ ] README usage remains valid and the full Release build passes with warnings as errors.

## 8. Add a HasKnownTotal convenience property to DownloadProgress

**Suggested labels:** `good first issue`, `api`, `tests`

### Context

Progress consumers repeatedly check `TotalBytes is not null` to choose determinate or indeterminate UI. A named convenience property would make host code and samples more readable without changing percentage calculation.

### Expected behavior

Add a read-only `HasKnownTotal` property that is true whenever `TotalBytes` has a value, including zero, and false when the server omitted `Content-Length`.

### Likely files

- `src/UpdateKit.Core/Models/DownloadProgress.cs`
- `tests/UpdateKit.Core.Tests/Models/DownloadProgressTests.cs`
- `README.md`
- `docs/GETTING_STARTED.md`

### Acceptance criteria

- [ ] Returns false only when `TotalBytes` is null.
- [ ] Returns true for positive and zero totals.
- [ ] Does not change `Percentage`, validation, equality, or download reporting behavior.
- [ ] Includes XML documentation and focused deterministic tests.
- [ ] Updates one public usage example without unnecessary documentation churn.
