# Changelog

## 0.3.0 - 2026-07-21

- Clarified generic release-asset support across Core, reusable dialogs, and all samples, including arbitrary and multi-part extensions such as `.exe`, `.msi`, `.nupkg`, and `.tar.gz`.
- Reusable dialogs and the full WinForms example now accept an existing destination directory and preserve the selected asset's original filename; explicit file paths remain unchanged.
- Added visible no-install/no-execution guidance and expanded deterministic selection, destination, and executable-looking-file safety coverage.

## 0.2.1 - 2026-07-21

- Added a safe **View release** action to the reusable WinForms and WPF update experiences. The action is shown only for validated, credential-free GitHub HTTPS release-page URLs, and browser-launch failures remain nonfatal.
