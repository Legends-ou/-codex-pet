# Pet Desktop

Pet Desktop is a standalone Windows desktop companion compatible with Codex pet packages.
It reads pet resources from the application's own `pets` directory and supports both
Codex v1 and v2 package conventions, including PNG and WebP sprite sheets.

## What is included

- Animated desktop pets, drag movement, scaling, tray controls, light/dark themes, and always-on-top support.
- Notes, scheduled reminders, custom phrases, shared reminder actions, and pet speech bubbles.
- Companion progression, a compact accumulated duration display, and a GitHub-style daily activity grid.
- Local-only wellness prompts based on Windows idle time; no window title, typed text, clipboard, screen, or network data is read.
- Installed and portable Windows releases from the same source tree.

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/PetDesktop.App` | WPF desktop application |
| `src/PetDesktop.Core` | Pet parsing, animation, settings, reminders, progression, and wellness logic |
| `tests/` | Unit and contract tests |
| `assets/` | Application icon and bundled default pets |
| `installer/` | Inno Setup installer definition |
| `scripts/` | Release build and verification scripts |
| `docs/` | Release process documentation |

## Prerequisites

- Windows 10 version 19045 or later
- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Inno Setup 6 or 7 (only required to create an installer)

## Develop

```powershell
dotnet restore
dotnet build PetDesktop.sln -c Release --no-restore
dotnet test PetDesktop.sln -c Release --no-build --no-restore --filter 'FullyQualifiedName!~RealPetContractTests'
```

Open `PetDesktop.sln` in Visual Studio, or run the built app from
`src\PetDesktop.App\bin\Release\net10.0-windows10.0.22621.0\PetDesktop.App.exe`.

## Create a release

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Release.ps1 -Version 2.0.0
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-Release.ps1 -Version 2.0.0
```

The resulting installer, portable archive, and SHA-256 manifest are written to
`artifacts/`, which is intentionally ignored by Git. Upload those files to a GitHub
Release instead of committing them; the portable archive can exceed GitHub's regular
file-size limit.

## Pet resources

Bundled pets live under `assets/pets/`. At runtime, the application checks its own
`pets/` folder, so a portable build can be customized by placing compatible pet folders
beside the executable. See the existing `assets/pets/小宇/` package as a minimal example.

## License

No license has been selected yet. Choose and add a license before accepting external
contributions or distributing source code under defined reuse terms.
