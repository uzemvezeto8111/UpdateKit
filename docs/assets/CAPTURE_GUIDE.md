# UpdateKit media capture guide

Use this guide to create the three public README visuals from the real UpdateKit applications. The captures must come from a working build and a genuine GitHub release response—do not construct, composite, or generate replacement UI.

## Final files

Save the finished media directly in this directory with these exact names:

```text
docs/assets/update-dialog-hero.png
docs/assets/example-configuration.png
docs/assets/update-flow-demo.gif
```

Use PNG for both still images. Do not substitute JPEG, WebP, or a renamed format; keeping the names stable prevents README churn.

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

## 4. Capture `example-configuration.png`

1. Configure the sample exactly as described above.
2. Make sure the access-token field is empty and no validation or error message is visible.
3. Keep the complete application window in frame, including its title bar and action buttons.
4. Capture the window with Windows Snipping Tool window mode or `Alt+PrintScreen`.
5. Crop only transparent or desktop space outside the window. Do not crop any application control.
6. Save as `docs/assets/example-configuration.png`.

Review the image at 100% zoom. Repository values should be readable, the destination must contain no private username, and no other application or notification should appear.

## 5. Capture `update-dialog-hero.png`

1. From the configured sample, choose **Check for updates**.
2. Wait until the dialog displays **An update is available**.
3. Leave the release notes scrolled to the top. Confirm the version, publication date, and selected package name are visible and no error is shown.
4. Capture the entire dialog, including its native title bar and all buttons.
5. Save the original-resolution capture as `docs/assets/update-dialog-hero.png`.

The hero should show the update-available state before Download is selected. This state communicates the most information without relying on a transient progress frame.

## 6. Record `update-flow-demo.gif`

Use a recorder that can export an animated GIF, such as ScreenToGif. A Windows Snipping Tool screen recording is also suitable if you convert the resulting MP4 afterward.

1. Use a fresh destination filename, or remove only the previous file in `C:\Users\Public\Documents\UpdateKit-Capture` before another take.
2. Position the example so the modal dialog will be centered and fully inside a 960–1200 pixel-wide recording region.
3. Record at 10–12 frames per second with the pointer visible.
4. Start recording immediately before choosing **Check for updates** in the example.
5. Preserve these real states in order:
   - **Checking for updates…**
   - **An update is available**
   - **Downloading update…** after choosing **Download**
   - **Update downloaded** with the completion path visible
6. Pause for roughly one second on update-available and completion states, then stop recording.
7. Trim only idle frames before the check and after completion. You may extend the display delay of an authentic progress or completion frame, but do not alter text, progress values, or UI content.
8. Export as a looping GIF, 10–12 fps, maximum width 960 pixels, using an optimized palette. Aim for 5 MB or less while keeping labels readable.
9. Save as `docs/assets/update-flow-demo.gif`.

The NuGet package may download too quickly for several progress frames. That is acceptable if the authentic **Downloading update…** state is captured. For a longer transfer, use a genuine published UpdateKit asset such as a distributable sample archive; do not pad a file or simulate network output for presentation.

If converting a Snipping Tool MP4 with `ffmpeg`, use a palette-based conversion:

```powershell
ffmpeg -i update-flow-demo.mp4 `
  -vf "fps=10,scale=960:-1:flags=lanczos,palettegen" `
  update-flow-palette.png

ffmpeg -i update-flow-demo.mp4 -i update-flow-palette.png `
  -lavfi "fps=10,scale=960:-1:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer" `
  docs/assets/update-flow-demo.gif
```

Delete the temporary MP4 and palette PNG after confirming the GIF. Do not commit those intermediate files.

## 7. Enable the README media

The root [README](../../README.md) already contains the final image references inside HTML comments. After adding each real file:

1. Remove the surrounding `<!--` and `-->` markers for that image.
2. Remove the matching `Visual preview pending`, `Example screenshot pending`, or `Demo pending` fallback paragraph.
3. Keep the supplied alt text unchanged unless the captured content materially differs.
4. Preview the README at desktop and narrow widths.
5. Verify all links and run `git diff --check` before committing.

Enable only media that actually exists. It is valid to keep a later capture commented with its fallback text while publishing an earlier one.

## Final review checklist

- [ ] All media comes from the Release build and a real GitHub response.
- [ ] The filenames exactly match the three names at the top of this guide.
- [ ] No token, private username, notification, or unrelated window is visible.
- [ ] The still images are crisp PNG files at their original capture resolution.
- [ ] The GIF shows all four required states, loops cleanly, and remains readable.
- [ ] The README contains no broken active image reference.
- [ ] Only completed assets have had their README references enabled.
