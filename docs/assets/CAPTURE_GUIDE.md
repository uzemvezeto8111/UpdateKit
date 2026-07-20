# UpdateKit media capture guide

The repository contains three authentic WinForms screenshots. Use this guide when they need to be recaptured. Captures must come from a working build and a genuine GitHub release response—do not construct, composite, or generate replacement UI.

## Final files

Save the finished media directly in this directory with these exact names:

```text
docs/assets/winforms-example-configuration.png
docs/assets/winforms-update-available.png
docs/assets/winforms-download-complete.png
```

Use PNG for all three images. Do not substitute JPEG, WebP, or renamed formats; keeping the names stable prevents README churn.

## 1. Prepare an authentic release

The cleanest public capture uses the real `uzemvezeto8111/UpdateKit` repository after its `v0.1.0` GitHub release is published.

1. Publish `v0.1.0` as a GitHub **pre-release**, not a draft. The API always filters drafts.
2. Attach `UpdateKit.Core.0.1.0.nupkg` and `UpdateKit.WinForms.0.1.0.nupkg` to the release.
3. Use a concise release name such as `UpdateKit v0.1.0` and the real public release notes.
4. Keep the repository public for a credential-free capture. If it is private, use a short-lived token while testing, then make the final capture only after the token field has been cleared.

If the release or asset names differ, substitute the real published values consistently. Never invent release details solely for the screenshot.

## 2. Build and run the example

From the repository root in PowerShell:

```powershell
dotnet restore UpdateKit.sln
dotnet build UpdateKit.sln --configuration Release --no-restore

$captureDirectory = Join-Path $env:PUBLIC "Documents\UpdateKit-Capture"
New-Item -ItemType Directory -Path $captureDirectory -Force

dotnet run `
  --project samples/UpdateKit.Example.WinForms/UpdateKit.Example.WinForms.csproj `
  --configuration Release `
  --no-build
```

The public documents folder avoids exposing a personal account name in the destination field. Close unrelated applications and notifications before recording.

## 3. Configure the sample consistently

Enter these values in the example window:

| Field | Capture value |
| --- | --- |
| Repository owner | `uzemvezeto8111` |
| Repository name | `UpdateKit` |
| Access token | Leave blank for the public repository |
| Current version | `0.0.0` |
| Include prerelease versions | Checked, because `v0.1.0` is a GitHub pre-release |
| Asset selection | `Exact asset name` |
| Asset name | `UpdateKit.WinForms.0.1.0.nupkg` |
| Destination | `C:\Users\Public\Documents\UpdateKit-Capture\UpdateKit.WinForms.0.1.0.nupkg` |
| Verification | `No checksum verification` for the baseline capture |

If the published release includes a real checksum asset, you may instead choose `Checksum-file asset` and enter its exact filename. Do not display a made-up checksum or a token in public media.

Use the Windows light theme, the default system font, 100% or 125% display scaling, and the application's default window sizes. Ensure text is sharp and no control is clipped. Do not add decorative borders, browser chrome, drop shadows, or annotations.

## 4. Capture `winforms-example-configuration.png`

1. Configure the sample exactly as described above.
2. Make sure the access-token field is empty and no validation or error message is visible.
3. Keep the complete application window in frame, including its title bar and action buttons.
4. Capture the window with Windows Snipping Tool window mode or `Alt+PrintScreen`.
5. Crop only transparent or desktop space outside the window. Do not crop any application control.
6. Save as `docs/assets/winforms-example-configuration.png`.

Review the image at 100% zoom. Repository values should be readable, the destination must contain no private username, and no other application or notification should appear.

## 5. Capture `winforms-update-available.png`

1. From the configured sample, choose **Check for updates**.
2. Wait until the dialog displays **An update is available**.
3. Leave the release notes scrolled to the top. Confirm the version, publication date, and selected package name are visible and no error is shown.
4. Capture the entire dialog, including its native title bar and all buttons.
5. Save the original-resolution capture as `docs/assets/winforms-update-available.png`.

The update-available screenshot should be captured before Download is selected. This state communicates the release details without relying on a transient progress frame.

## 6. Capture `winforms-download-complete.png`

1. Choose **Download** and wait until the dialog displays **Update downloaded**.
2. Confirm the saved destination, completed byte count, full progress bar, and release details are visible.
3. Capture the entire dialog, including its native title bar and all buttons.
4. Save the original-resolution capture as `docs/assets/winforms-download-complete.png`.

The completed state must come from a successful real transfer. Do not edit the progress value, byte count, destination, or status text.

## Final review checklist

- [ ] All media comes from the Release build and a real GitHub response.
- [ ] The filenames exactly match the three names at the top of this guide.
- [ ] No token, private username, notification, or unrelated window is visible.
- [ ] The three still images are crisp PNG files at their original capture resolution.
- [ ] The README contains no broken active image reference.
- [ ] Only completed assets have had their README references enabled.
