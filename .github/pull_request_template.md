## Summary

Describe the user-visible problem and the focused change that resolves it.

Closes #

## Change type

- [ ] Bug fix
- [ ] Additive feature
- [ ] Tests
- [ ] Documentation
- [ ] Accessibility or UI refinement
- [ ] Repository maintenance

## Validation

List the exact commands run and their results. Automated tests must not call the real GitHub API.

```text
dotnet build UpdateKit.sln --configuration Release --no-restore
dotnet test UpdateKit.sln --configuration Release --no-build --no-restore
```

## Public API and compatibility

Describe any public API, nullability, ownership, cancellation, result/error, file-system, or UI behavior impact. Write `None` when there is no impact.

## Checklist

- [ ] I followed the [contributor development checklist](../docs/CONTRIBUTOR_DEVELOPMENT_CHECKLIST.md).
- [ ] The change is focused and contains no unrelated formatting or generated files.
- [ ] Nullable analysis and the warnings-as-errors build pass.
- [ ] I added or updated deterministic tests for behavioral changes.
- [ ] HTTP tests use a custom `HttpMessageHandler` and make no real network calls.
- [ ] Public API changes include XML documentation and usage documentation where needed.
- [ ] I preserved caller ownership of `HttpClient` and established cancellation/result contracts.
- [ ] I removed credentials, private paths, logs, packages, `bin/`, `obj/`, and test results.
- [ ] `git diff --check` passes.

