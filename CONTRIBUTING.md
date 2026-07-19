# Contributing to UpdateKit

Thank you for improving UpdateKit. Small, focused changes with deterministic tests are easiest to review.

## Prerequisites

- .NET 8 SDK
- Git
- Windows for building and testing the WinForms projects

The SDK selection is recorded in `global.json` and rolls forward to the latest installed .NET 8 feature band.

## Set up the repository

```powershell
git clone <repository-url>
cd UpdateKit
dotnet restore UpdateKit.sln
dotnet build UpdateKit.sln --configuration Release --no-restore
dotnet test UpdateKit.sln --configuration Release --no-build --no-restore
```

## Development guidelines

- Preserve nullable reference-type correctness and the warnings-as-errors build.
- Keep GitHub transport, versioning, asset selection, downloading, verification, and UI concerns separated.
- Use `UpdateResult<T>` and existing `UpdateErrorCode` values for expected operational failures.
- Preserve `HttpClient` ownership: public services borrow it and do not dispose it.
- Keep downloads streaming, cancellable, and safe for existing destination files.
- Add or update deterministic tests with every behavioral change.
- Use custom `HttpMessageHandler` implementations in tests; automated tests must never call the real GitHub API.
- Avoid committing credentials, user-specific paths, IDE state, generated packages, `bin/`, `obj/`, or test results.
- Update XML comments and user documentation when public behavior changes.

## Pull requests

Before opening a pull request:

1. Rebase or merge the current default branch as appropriate for the repository.
2. Run the full Release build and test commands above.
3. Confirm `git status` contains only intentional source and documentation changes.
4. Describe the user-visible behavior, tests, compatibility impact, and any remaining limitations.
5. Keep unrelated formatting or generated-file churn out of the change.

Bug reports should include the UpdateKit revision, .NET version, operating system, expected behavior, actual `UpdateErrorCode`, and a minimal reproduction with secrets removed.

## Security reports

Do not open public issues for vulnerabilities. Follow [SECURITY.md](SECURITY.md).

## License

By contributing, you agree that your contribution will be licensed under the repository's [MIT License](LICENSE).

