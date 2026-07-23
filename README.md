# NDI Viewer

A small Windows app that scans the local network for [NDI](https://ndi.video) sources,
lists them, and opens a live video preview window for whichever one you pick.

## How it works

- **Discovery** (`src/NdiViewer/Ndi/NdiFinder.cs`) continuously polls the NDI find API
  on a background thread and updates the source list shown in the main window.
- **Preview** (`src/NdiViewer/Ndi/NdiReceiver.cs`, `PreviewWindow.xaml`) connects to the
  selected source, requests 8-bit BGRA/BGRX video, and renders incoming frames into a
  `WriteableBitmap`. Audio is not received - this is a video preview tool only.
- The app talks to NDI purely via P/Invoke against `Processing.NDI.Lib.x64.dll`
  (`src/NdiViewer/Ndi/NdiInterop.cs`) - no NDI SDK headers/libs are needed to build it.

## The NDI Runtime dependency

NDI itself is a proprietary technology from Vizrt. This repo does **not** vendor or
build against the NDI SDK, and the installer does **not** bundle NDI's own binaries.
Instead:

- At startup, the app looks for an existing NDI Runtime install (`NdiRuntimeLocator.cs`)
  via the `NDI_RUNTIME_DIR_V*` environment variables and common `Program Files` paths.
- If it's missing, `installer/setup.iss` automatically downloads the official NDI
  Runtime redistributable from https://ndi.link/NDIRedistV6 (Inno Setup's built-in
  `DownloadTemporaryFile`) and runs it silently (`/SP- /VERYSILENT /NORESTART` - the
  NDI redistributable is itself Inno Setup-based). If that download/run fails, it falls
  back to `winget install --id NDI.NDIRuntime`, and if that's unavailable too, it opens
  the download page in the browser as a last resort.
- If the app is launched without the runtime present at all, it shows a message
  explaining what to install rather than crashing.

This product uses NDI®, a registered trademark of Vizrt NDI AB.

## Building locally (Windows only)

```powershell
dotnet publish src/NdiViewer/NdiViewer.csproj -c Release -r win-x64 --self-contained true -o publish
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DMyAppVersion=1.0.0 installer\setup.iss
```

The installer exe is written to `dist\NdiViewer-Setup-1.0.0.exe`.

## Releasing

Releases are built entirely in GitHub Actions - there's no need for NDI's SDK or any
proprietary binaries in CI. To cut a release:

1. Go to the **Actions** tab → **Build and Release** → **Run workflow**.
2. Enter a version number (e.g. `1.2.0`).
3. The workflow publishes the app, compiles the Inno Setup installer, and uploads
   `NdiViewer-Setup-<version>.exe` to a new GitHub Release tagged `v<version>`.
