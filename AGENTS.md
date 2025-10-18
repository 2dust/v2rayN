# Repository Guidelines

## Project Structure & Modules
- Solution: `v2rayN/v2rayN.sln` (C#/.NET 8)
- Apps: `v2rayN/v2rayN` (Windows WPF), `v2rayN/v2rayN.Desktop` (Avalonia cross‑platform)
- Core library: `v2rayN/ServiceLib` (models, services, `Resx` resources)
- Hotkeys: `v2rayN/GlobalHotKeys` (library + examples); tests in `GlobalHotKeys.Test`
- Artifacts: `v2rayN/Release/`, `v2rayN/publish/`, `build-output/`
- Packaging scripts (root): `package-*.sh`

## Build, Test, and Development
- Prerequisites:
  - .NET 8 SDK on all platforms.
  - Building the WPF app (`v2rayN/v2rayN`) requires the Windows Desktop targeting pack. Install via `dotnet workload install windowsdesktop` or use a Windows machine with Visual Studio build tools.
- Build all projects (no publish artifacts): `dotnet build v2rayN/v2rayN.sln -c Release`
- Run Avalonia app (Linux/macOS/Windows): `dotnet run --project v2rayN/v2rayN.Desktop -c Debug`
- Run WPF app (Windows only): `dotnet run --project v2rayN/v2rayN -c Debug`
- Publish (examples):
  - Avalonia Linux x64 self-contained: `dotnet publish v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj -c Release -r linux-x64 --self-contained true -o v2rayN/Release/linux-64`
  - WPF Windows x64 self-contained: `dotnet publish v2rayN/v2rayN/v2rayN.csproj -c Release -r win-x64 -p:EnableWindowsTargeting=true --self-contained true -o v2rayN/Release/windows-64`
  - Use other runtime identifiers (`win-arm64`, `osx-x64`, etc.) similarly.
- Tests (NUnit): `dotnet test v2rayN/GlobalHotKeys/src/GlobalHotKeys.Test -c Release --collect:"XPlat Code Coverage"`

### Quick Linux x64 build (single executable)

Minimal commands to produce a self-contained Linux x64 executable (with normal output):

```
git submodule update --init --recursive
dotnet publish v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj \
  -c Release -r linux-x64 --self-contained true \
  -o v2rayN/Release/linux-64
echo ./v2rayN/Release/linux-64/v2rayN
```

## Coding Style & Naming
- Formatting from `.editorconfig`: UTF-8, CRLF, spaces=4.
- C#: file‑scoped namespaces; System usings first; braces required; prefer `var` when type is apparent; PascalCase for types/members; fields/private locals use camelCase.
- Lint/format: `dotnet format` before pushing.
- XAML/Avalonia: keep views thin; move logic to ViewModels in `ServiceLib`/`*ViewModels`.

## Testing Guidelines
- Framework: NUnit with `GlobalHotKeys.Test`.
- Conventions: classes `*Tests`, methods `[Test]` with Arrange/Act/Assert; keep tests isolated and deterministic.
- Run locally with `dotnet test` and ensure coverage for hotkey registration/ID reuse.

## Commit & Pull Requests
- Commits: short imperative subject (e.g., "Fix…", "Update…"), optional scope, reference issues/PRs (e.g., `(#8123)`).
- PRs must include: problem summary, rationale, user impact, steps to verify, screenshots for UI, target OS/runtime.
- CI parity: changes must pass `dotnet build` and `dotnet test` locally; do not commit artifacts under `Release/` or `publish/`.

## Security & Configuration
- Do not commit secrets or downloaded core binaries; rely on packaging scripts/CI.
- Submodules: clone/update with `--recursive` when needed.
- Keep platform‑specific code isolated (Windows WPF vs Avalonia) to avoid regressions.

## Agent Notes
- Follow this file’s scope for style and layout; keep patches minimal and targeted.
- Prefer simplifying data flow over adding conditionals; avoid deep nesting.
